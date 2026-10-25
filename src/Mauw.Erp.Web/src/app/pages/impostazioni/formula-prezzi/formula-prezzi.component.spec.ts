import { ComponentFixture, TestBed } from '@angular/core/testing';
import { HttpErrorResponse } from '@angular/common/http';
import { Subject, of, throwError } from 'rxjs';
import { FormulaPrezziComponent } from './formula-prezzi.component';
import { PriceFormulaService } from '../../../services/price-formula.service';
import { PriceFormula, PriceFormulaCalculation } from '../../../models/price-formula.model';

describe('FormulaPrezziComponent', () => {
  let service: jasmine.SpyObj<PriceFormulaService>;
  let fixture: ComponentFixture<FormulaPrezziComponent>;
  let component: FormulaPrezziComponent;

  const notConfigured: PriceFormula = {
    isConfigured: false, steps: [], roundingStep: 0.1, updatedAt: null, updatedByUserId: null
  };
  const configured: PriceFormula = {
    isConfigured: true,
    steps: [{ label: 'Ricarico', percentage: 40 }, { label: 'Provvigione', percentage: 10 }],
    roundingStep: 0.1,
    updatedAt: '2026-10-25T10:00:00Z',
    updatedByUserId: 'user-1'
  };
  const calculation: PriceFormulaCalculation = {
    purchaseCost: 20,
    steps: [
      { label: 'Ricarico', percentage: 40, runningValue: 28 },
      { label: 'Provvigione', percentage: 10, runningValue: 30.8 },
      { label: 'Spese', percentage: 5, runningValue: 32.34 }
    ],
    unroundedPrice: 32.34, roundingStep: 0.1, finalPrice: 32.4
  };
  const problem = (detail: string) =>
    new HttpErrorResponse({ status: 400, error: { title: 'Bad Request', detail } });

  const create = async (formula: PriceFormula = configured) => {
    service.get.and.returnValue(of(formula));
    await TestBed.configureTestingModule({
      imports: [FormulaPrezziComponent],
      providers: [{ provide: PriceFormulaService, useValue: service }]
    }).compileComponents();
    fixture = TestBed.createComponent(FormulaPrezziComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
  };

  const html = () => fixture.nativeElement as HTMLElement;
  const q = (testId: string) => html().querySelector(`[data-testid="${testId}"]`);
  const labels = () => component.steps().map(step => step.label);

  beforeEach(() => {
    service = jasmine.createSpyObj<PriceFormulaService>('PriceFormulaService', ['get', 'save', 'calculate']);
  });

  it('loads the saved formula without unsaved changes', async () => {
    await create();
    expect(labels()).toEqual(['Ricarico', 'Provvigione']);
    expect(component.roundingStep()).toBe(0.1);
    expect(component.hasUnsavedChanges()).toBeFalse();
    expect(q('unsaved-indicator')).toBeNull();
    expect(q('not-configured')).toBeNull();
    expect(html().querySelectorAll('[data-testid="step-row"]').length).toBe(2);
    expect(html().querySelector('h1')?.textContent).toContain('Formula prezzi');
  });

  it('shows the not configured state with the default rounding step', async () => {
    await create(notConfigured);
    expect(q('not-configured')).not.toBeNull();
    expect(component.steps()).toEqual([]);
    expect(component.roundingStep()).toBe(0.1);
    expect(q('steps-error')?.textContent).toContain('almeno un passaggio');
    expect(component.canSave()).toBeFalse();
  });

  it('shows a load error', async () => {
    service.get.and.returnValue(throwError(() => problem('Accesso negato')));
    await TestBed.configureTestingModule({
      imports: [FormulaPrezziComponent],
      providers: [{ provide: PriceFormulaService, useValue: service }]
    }).compileComponents();
    fixture = TestBed.createComponent(FormulaPrezziComponent);
    fixture.detectChanges();
    expect(q('load-error')?.textContent).toContain('Accesso negato');
  });

  it('adds, removes and moves steps and flags unsaved changes', async () => {
    await create();
    component.addStep();
    component.updateLabel(2, 'Spese');
    component.updatePercentage(2, 5);
    fixture.detectChanges();
    expect(labels()).toEqual(['Ricarico', 'Provvigione', 'Spese']);
    expect(q('unsaved-indicator')).not.toBeNull();

    component.moveStep(2, -1);
    expect(labels()).toEqual(['Ricarico', 'Spese', 'Provvigione']);
    component.moveStep(0, -1);
    component.moveStep(2, 1);
    expect(labels()).toEqual(['Ricarico', 'Spese', 'Provvigione']);
    component.moveStep(0, 1);
    expect(labels()).toEqual(['Spese', 'Ricarico', 'Provvigione']);

    component.removeStep(0);
    component.removeStep(1);
    expect(labels()).toEqual(['Ricarico']);
    component.addStep();
    component.updateLabel(1, 'Provvigione');
    component.updatePercentage(1, 10);
    expect(component.hasUnsavedChanges()).toBeFalse();
  });

  it('adds a step from the DOM and edits it through the inputs', async () => {
    await create(notConfigured);
    (q('add-step') as HTMLButtonElement).click();
    fixture.detectChanges();
    await fixture.whenStable();
    const label = html().querySelector('[aria-label="Nome passaggio 1"]') as HTMLInputElement;
    const percentage = html().querySelector('[aria-label="Percentuale passaggio 1"]') as HTMLInputElement;
    label.value = 'Ricarico';
    label.dispatchEvent(new Event('input'));
    percentage.value = '40';
    percentage.dispatchEvent(new Event('input'));
    fixture.detectChanges();
    expect(component.steps()[0]).toEqual(jasmine.objectContaining({ label: 'Ricarico', percentage: 40 }));
    expect(component.canSave()).toBeTrue();
  });

  it('stops adding at 20 steps', async () => {
    await create(notConfigured);
    for (let i = 0; i < 25; i++) component.addStep();
    expect(component.steps().length).toBe(20);
    expect(component.stepsError()).toBeNull();
  });

  it('validates labels, percentages and the rounding step client-side', async () => {
    await create(notConfigured);
    component.addStep();
    expect(component.stepErrors()[0].label).toContain('nome');
    expect(component.stepErrors()[0].percentage).toContain('percentuale');
    component.updateLabel(0, 'x'.repeat(101));
    expect(component.stepErrors()[0].label).toContain('100');
    component.updateLabel(0, 'Ricarico');
    component.updatePercentage(0, -1);
    expect(component.stepErrors()[0].percentage).toContain('0 e 9999');
    component.updatePercentage(0, 10000);
    expect(component.stepErrors()[0].percentage).toContain('0 e 9999');
    component.updatePercentage(0, 10.123);
    expect(component.stepErrors()[0].percentage).toContain('2 decimali');
    component.updatePercentage(0, 9999);
    expect(component.stepErrors()[0]).toEqual({ label: null, percentage: null });
    component.updatePercentage(0, 0);
    expect(component.isValid()).toBeTrue();

    for (const value of [0, -0.1, 100.01, 0.123, null]) {
      component.updateRoundingStep(value);
      expect(component.roundingError()).not.toBeNull();
      expect(component.isValid()).toBeFalse();
    }
    for (const value of [0.01, 0.05, 100]) {
      component.updateRoundingStep(value);
      expect(component.roundingError()).toBeNull();
    }
  });

  it('does not call save while the form is invalid or unchanged', async () => {
    await create();
    component.save();
    expect(service.save).not.toHaveBeenCalled();
    component.updatePercentage(0, -5);
    component.save();
    expect(service.save).not.toHaveBeenCalled();
  });

  it('saves trimmed steps in order, applies the response and shows success', async () => {
    await create();
    component.updateLabel(0, '  Ricarico lordo ');
    component.moveStep(1, -1);
    component.updateRoundingStep(0.05);
    service.save.and.returnValue(of({
      ...configured,
      steps: [{ label: 'Provvigione', percentage: 10 }, { label: 'Ricarico lordo', percentage: 40 }],
      roundingStep: 0.05
    }));
    component.save();
    fixture.detectChanges();
    expect(service.save).toHaveBeenCalledOnceWith({
      steps: [{ label: 'Provvigione', percentage: 10 }, { label: 'Ricarico lordo', percentage: 40 }],
      roundingStep: 0.05
    });
    expect(q('save-success')).not.toBeNull();
    expect(component.hasUnsavedChanges()).toBeFalse();
    expect(component.saving()).toBeFalse();
    expect(q('not-configured')).toBeNull();
  });

  it('marks a formula as configured after the first successful save', async () => {
    await create(notConfigured);
    component.addStep();
    component.updateLabel(0, 'Ricarico');
    component.updatePercentage(0, 40);
    service.save.and.returnValue(of({ ...configured, steps: [{ label: 'Ricarico', percentage: 40 }] }));
    component.save();
    fixture.detectChanges();
    expect(component.isConfigured()).toBeTrue();
    expect(q('not-configured')).toBeNull();
  });

  it('shows server validation errors, keeps the edits and disables saving only while in flight', async () => {
    await create();
    component.updatePercentage(0, 50);
    const response = new Subject<PriceFormula>();
    service.save.and.returnValue(response);
    component.save();
    expect(component.saving()).toBeTrue();
    expect(component.canSave()).toBeFalse();
    response.error(problem('Il passo di arrotondamento non è valido.'));
    fixture.detectChanges();
    expect(q('save-error')?.textContent).toContain('Il passo di arrotondamento non è valido.');
    expect(component.saving()).toBeFalse();
    expect(component.steps()[0].percentage).toBe(50);
    expect(component.hasUnsavedChanges()).toBeTrue();
    expect(q('save-success')).toBeNull();
  });

  it('simulates with the purchase cost and renders the breakdown and the highlighted final price', async () => {
    await create();
    service.calculate.and.returnValue(of(calculation));
    component.updatePurchaseCost(20);
    component.simulate();
    fixture.detectChanges();
    expect(service.calculate).toHaveBeenCalledOnceWith(20);
    const rows = Array.from(html().querySelectorAll('[data-testid="calculation-row"]'))
      .map(row => Array.from(row.querySelectorAll('td')).map(cell => cell.textContent!.trim()));
    expect(rows).toEqual([['Ricarico', '40%', '28.00'], ['Provvigione', '10%', '30.80'], ['Spese', '5%', '32.34']]);
    expect(q('unrounded-price')?.textContent?.trim()).toBe('32.34');
    expect(q('final-price')?.textContent?.trim()).toBe('32.40');
  });

  it('renders four-decimal running values trimmed to the needed precision', async () => {
    await create();
    service.calculate.and.returnValue(of({
      ...calculation,
      steps: [{ label: 'Ricarico', percentage: 33, runningValue: 26.6 }, { label: 'IVA', percentage: 12.5, runningValue: 29.9249 }],
      unroundedPrice: 29.9249, finalPrice: 30
    }));
    component.updatePurchaseCost(20);
    component.simulate();
    fixture.detectChanges();
    const values = Array.from(html().querySelectorAll('[data-testid="calculation-row"] td:last-child')).map(c => c.textContent!.trim());
    expect(values).toEqual(['26.60', '29.9249']);
    expect(q('final-price')?.textContent?.trim()).toBe('30.00');
  });

  it('validates the purchase cost and does not call the backend when invalid', async () => {
    await create();
    for (const cost of [null, -1, 1.23456]) {
      component.updatePurchaseCost(cost);
      expect(component.costError()).not.toBeNull();
      component.simulate();
    }
    expect(service.calculate).not.toHaveBeenCalled();
    component.updatePurchaseCost(0);
    expect(component.costError()).toBeNull();
    component.updatePurchaseCost(12.3456);
    expect(component.costError()).toBeNull();
  });

  it('hints that the simulation uses the saved formula when there are unsaved changes', async () => {
    await create();
    expect(q('simulator-hint')).toBeNull();
    component.updatePercentage(0, 45);
    fixture.detectChanges();
    expect(q('simulator-hint')?.textContent).toContain('non salvate');
  });

  it('shows the not configured error returned by the simulator', async () => {
    await create(notConfigured);
    service.calculate.and.returnValue(throwError(() =>
      problem('Formula prezzi non configurata: impostarla in Impostazioni > Formula prezzi.')));
    component.updatePurchaseCost(20);
    component.simulate();
    fixture.detectChanges();
    expect(q('calculation-error')?.textContent).toContain('Formula prezzi non configurata');
    expect(q('calculation-table')).toBeNull();
    expect(component.calculating()).toBeFalse();
  });

  it('clears a stale simulation after saving', async () => {
    await create();
    service.calculate.and.returnValue(of(calculation));
    component.updatePurchaseCost(20);
    component.simulate();
    component.updatePercentage(0, 45);
    service.save.and.returnValue(of({ ...configured, steps: [{ label: 'Ricarico', percentage: 45 }, configured.steps[1]] }));
    component.save();
    fixture.detectChanges();
    expect(q('calculation-table')).toBeNull();
  });
});
