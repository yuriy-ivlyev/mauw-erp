import { TestBed } from '@angular/core/testing';
import { of, throwError } from 'rxjs';
import { HttpErrorResponse } from '@angular/common/http';
import { ListiniComponent } from './listini.component';
import { PriceListService } from '../../../services/price-list.service';
import { PriceList } from '../../../models/price-list.model';
import { AuthService } from '../../../services/auth.service';

describe('ListiniComponent', () => {
  let service: jasmine.SpyObj<PriceListService>;
  let component: ListiniComponent;
  let roles: string[];
  const list: PriceList = {
    id: 'list', name: 'Rivenditori', currency: 'EUR', isActive: true,
    rules: [{
      id: 'rule', calculationType: 'PercentageDiscount', value: 15.5, minimumQuantity: 1,
      productId: '12345678-1234-1234-1234-123456789012', priority: 10,
      validFrom: '2026-10-18T08:00:00Z', validTo: '2026-10-19T08:00:00Z'
    }]
  };
  beforeEach(() => {
    service = jasmine.createSpyObj<PriceListService>('PriceListService', ['getAll', 'get', 'create', 'update', 'activate', 'delete']);
    service.getAll.and.returnValue(of([list]));
    service.get.and.returnValue(of(list));
    service.update.and.returnValue(of(list));
    service.create.and.returnValue(of(list));
    service.activate.and.returnValue(of(undefined));
    service.delete.and.returnValue(of(undefined));
    roles = ['Admin'];
    const auth = { canSeeMargins: () => roles.includes('Admin') } as unknown as AuthService;
    TestBed.configureTestingModule({
      providers: [{ provide: PriceListService, useValue: service }, { provide: AuthService, useValue: auth }]
    });
    component = TestBed.runInInjectionContext(() => new ListiniComponent());
  });
  it('loads a detail and preserves IDs, precision and UTC dates on save', () => {
    component.open('list');
    component.save();
    expect(service.get).toHaveBeenCalledWith('list');
    expect(service.update).toHaveBeenCalledWith('list', {
      name: 'Rivenditori', currency: 'EUR', isActive: true,
      rules: [{
        id: 'rule', calculationType: 'PercentageDiscount', value: 15.5, minimumQuantity: 1, priority: 10,
        description: null,
        productId: list.rules[0].productId, variantId: null, categoryId: null,
        validFrom: '2026-10-18T08:00:00.000Z', validTo: '2026-10-19T08:00:00.000Z'
      }]
    });
  });
  it('saves the discount reason of a rule trimmed, and empties it when blank', () => {
    component.edit();
    component.form.controls.name.setValue('Base');
    component.addRule({
      calculationType: 'PercentageDiscount', value: 5, minimumQuantity: 1, priority: 0, description: '  Sconto magliette '
    });
    component.addRule({
      calculationType: 'PercentageDiscount', value: 3, minimumQuantity: 1, priority: 0, description: '   '
    });
    component.save();
    const rules = service.create.calls.mostRecent().args[0].rules;
    expect(rules[0].description).toBe('Sconto magliette');
    expect(rules[1].description).toBeNull();
  });
  it('loads the stored reason into the rule form so it can be edited', () => {
    component.edit({ ...list, rules: [{ ...list.rules[0], description: 'Sconto catalogo' }] });
    expect(component.rules.at(0).controls.description.value).toBe('Sconto catalogo');
  });
  it('rejects a discount reason longer than 200 characters', () => {
    component.edit();
    component.form.controls.name.setValue('Base');
    component.addRule({
      calculationType: 'PercentageDiscount', value: 5, minimumQuantity: 1, priority: 0, description: 'x'.repeat(201)
    });
    component.save();
    expect(service.create).not.toHaveBeenCalled();
    expect(component.rules.at(0).controls.description.errors).toEqual(jasmine.objectContaining({ maxlength: jasmine.anything() }));
  });
  it('creates a global rule without inventing an ID or a target', () => {
    component.edit();
    component.form.controls.name.setValue('  Base  ');
    component.addRule({ calculationType: 'FixedPrice', value: 12.12345678, minimumQuantity: 0.00000001, priority: 0 });
    component.save();
    expect(service.create).toHaveBeenCalledWith(jasmine.objectContaining({
      name: 'Base', rules: [jasmine.objectContaining({
        calculationType: 'FixedPrice', value: 12.12345678, minimumQuantity: 0.00000001,
        productId: null, categoryId: null, variantId: null
      })]
    }));
    expect(service.create.calls.mostRecent().args[0].rules[0].id).toBeUndefined();
  });
  it('does not submit invalid rules or whitespace-only names', () => {
    component.edit();
    component.form.controls.name.setValue('  ');
    component.addRule({ calculationType: 'FixedPrice', value: -1, minimumQuantity: 0, priority: 0.5 });
    component.save();
    expect(service.create).not.toHaveBeenCalled();
    expect(component.rules.at(0).touched).toBeTrue();
  });
  it('shows an Admin cost markup value as a number and sends it back', () => {
    const markup = { ...list, rules: [{ id: 'm', calculationType: 'CostMarkup' as const, value: 40, minimumQuantity: 1, priority: 0 }] };
    const fixture = TestBed.createComponent(ListiniComponent);
    fixture.componentInstance.edit(markup);
    fixture.detectChanges();
    const html: HTMLElement = fixture.nativeElement;
    expect(html.querySelector('.value-reserved')).toBeNull();
    expect((html.querySelector('input[formControlName="value"]') as HTMLInputElement).value).toBe('40');
    fixture.componentInstance.editId = 'list';
    fixture.componentInstance.save();
    expect(service.update.calls.mostRecent().args[1].rules[0].value).toBe(40);
  });
  it('shows "riservato" for a hidden cost markup and lets Sales save the list with value null', () => {
    roles = ['Sales'];
    const hidden = { ...list, rules: [
      { id: 'm', calculationType: 'CostMarkup' as const, value: null, minimumQuantity: 1, priority: 3 },
      { ...list.rules[0], productId: null }
    ] };
    const fixture = TestBed.createComponent(ListiniComponent);
    fixture.componentInstance.edit(hidden);
    fixture.detectChanges();
    const html: HTMLElement = fixture.nativeElement;
    const reserved = html.querySelectorAll('.value-reserved');
    expect(reserved.length).toBe(1);
    expect(reserved[0].textContent).toContain('riservato');
    expect(html.querySelectorAll('input[formControlName="value"]').length).toBe(1);
    expect(html.textContent).not.toMatch(/NaN|\bnull\b/);
    fixture.componentInstance.form.controls.name.setValue('Rivenditori');
    fixture.componentInstance.editId = 'list';
    fixture.componentInstance.save();
    const rules = service.update.calls.mostRecent().args[1].rules;
    expect(rules[0]).toEqual(jasmine.objectContaining({ id: 'm', calculationType: 'CostMarkup', value: null }));
    expect(rules[1].value).toBe(15.5);
  });
  it('offers the cost markup type only to roles that can see margins', () => {
    const options = (role: string, rules: PriceList['rules']) => {
      roles = [role];
      const fixture = TestBed.createComponent(ListiniComponent);
      fixture.componentInstance.edit({ ...list, rules });
      fixture.detectChanges();
      return Array.from((fixture.nativeElement as HTMLElement)
        .querySelectorAll('select[formControlName="calculationType"] option')).map(o => (o as HTMLOptionElement).value);
    };
    const global = [{ ...list.rules[0], productId: null }];
    expect(options('Admin', global)).toContain('CostMarkup');
    expect(options('Sales', global)).toEqual(['FixedPrice', 'PercentageDiscount']);
    const hidden = [{ id: 'm', calculationType: 'CostMarkup' as const, value: null, minimumQuantity: 1, priority: 0 }];
    expect(options('Sales', hidden)).toContain('CostMarkup');
  });
  it('still validates non-reserved rules in a list that contains a hidden cost markup', () => {
    component.edit({ ...list, rules: [
      { id: 'm', calculationType: 'CostMarkup', value: null, minimumQuantity: 1, priority: 0 },
      { ...list.rules[0], value: -1 }
    ] });
    component.save();
    expect(service.update).not.toHaveBeenCalled();
  });
  it('shows API errors and retains unsaved changes', () => {
    service.update.and.returnValue(throwError(() => new HttpErrorResponse({
      status: 400, error: { detail: 'Regole ambigue' }
    })));
    component.edit(list);
    component.save();
    expect(component.error).toBe('Regole ambigue');
    expect(component.editing).toBeTrue();
    expect(component.busy).toBeFalse();
  });
});
