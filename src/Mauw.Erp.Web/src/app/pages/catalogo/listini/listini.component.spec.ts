import { TestBed } from '@angular/core/testing';
import { of, throwError } from 'rxjs';
import { HttpErrorResponse } from '@angular/common/http';
import { ListiniComponent } from './listini.component';
import { PriceListService } from '../../../services/price-list.service';
import { PriceList } from '../../../models/price-list.model';

describe('ListiniComponent', () => {
  let service: jasmine.SpyObj<PriceListService>;
  let component: ListiniComponent;
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
    TestBed.configureTestingModule({
      providers: [{ provide: PriceListService, useValue: service }]
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
  it('offers only the fixed price and percentage discount rule types', () => {
    const fixture = TestBed.createComponent(ListiniComponent);
    fixture.componentInstance.edit({ ...list, rules: [{ ...list.rules[0], productId: null }] });
    fixture.detectChanges();
    const options = Array.from((fixture.nativeElement as HTMLElement)
      .querySelectorAll('select[formControlName="calculationType"] option')).map(o => (o as HTMLOptionElement).value);
    expect(options).toEqual(['FixedPrice', 'PercentageDiscount']);
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
