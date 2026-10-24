import { ComponentFixture, TestBed, fakeAsync, tick } from '@angular/core/testing';
import { HttpErrorResponse } from '@angular/common/http';
import { of, Subject, throwError } from 'rxjs';
import { PriceRuleTargetComponent } from './price-rule-target.component';
import { ProductService } from '../../../services/product.service';
import { VariantSearchDto } from '../../../models/product.model';

describe('PriceRuleTargetComponent', () => {
  let fixture: ComponentFixture<PriceRuleTargetComponent>;
  let component: PriceRuleTargetComponent;
  let service: jasmine.SpyObj<ProductService>;
  const variants: VariantSearchDto[] = [
    { variantId: 'sku-red-40', productId: 'product', productName: 'Scarpa saguaro',
      sku: 'RED40', barcode: 'RED40', colorName: 'Rosso', sizeName: '40', currentStock: 0,
      supplierCode: null, purchaseCost: 0 },
    { variantId: 'sku-blue-41', productId: 'product', productName: 'Scarpa saguaro',
      sku: 'BLUE41', barcode: 'BLUE41', colorName: 'Blu', sizeName: '41', currentStock: 0,
      supplierCode: null, purchaseCost: 0 }
  ];

  beforeEach(() => {
    service = jasmine.createSpyObj<ProductService>('ProductService',
      ['getCategories', 'searchParents', 'searchVariants', 'getProductMatrix']);
    service.getCategories.and.returnValue(of([]));
    service.searchVariants.and.returnValue(of(variants));
    service.searchParents.and.returnValue(of([
      { productId: 'product', barcode: 'P', supplierCode: 'S', name: 'Scarpa saguaro', hasVariants: true, variantCount: 2 }
    ]));
    TestBed.configureTestingModule({
      imports: [PriceRuleTargetComponent],
      providers: [{ provide: ProductService, useValue: service }]
    });
    fixture = TestBed.createComponent(PriceRuleTargetComponent);
    component = fixture.componentInstance;
    fixture.componentRef.setInput('target', 'variantId');
    fixture.detectChanges();
  });

  afterEach(() => fixture.destroy());

  it('searches and selects a specific SKU by product name without loading pricing or matrix', fakeAsync(() => {
    spyOn(component.selected, 'emit');
    component.queries.next('Scarpa saguaro');
    tick(200);
    fixture.detectChanges();
    expect(service.searchVariants).toHaveBeenCalledWith('Scarpa saguaro');
    expect(service.searchParents).not.toHaveBeenCalled();
    expect(service.getProductMatrix).not.toHaveBeenCalled();
    const buttons: NodeListOf<HTMLButtonElement> = fixture.nativeElement.querySelectorAll('button');
    expect(buttons.length).toBe(2);
    expect(buttons[1].textContent).toContain('BLUE41');
    expect(buttons[1].textContent).toContain('Blu');
    expect(buttons[1].textContent).toContain('41');
    buttons[1].click();
    expect(component.selected.emit).toHaveBeenCalledOnceWith('sku-blue-41');
    expect(component.search).toContain('BLUE41');
  }));

  it('keeps product target search separate from variant search', fakeAsync(() => {
    fixture.componentRef.setInput('target', 'productId');
    fixture.detectChanges();
    spyOn(component.selected, 'emit');
    component.queries.next('Scarpa');
    tick(200);
    fixture.detectChanges();
    expect(service.searchParents).toHaveBeenCalledWith('Scarpa');
    expect(service.searchVariants).not.toHaveBeenCalled();
    fixture.nativeElement.querySelector('button').click();
    expect(component.selected.emit).toHaveBeenCalledOnceWith('product');
  }));

  it('cancels stale searches when changing target and clears the previous results', fakeAsync(() => {
    const pending = new Subject<VariantSearchDto[]>();
    service.searchVariants.and.returnValue(pending);
    component.queries.next('Scarpa');
    tick(200);
    fixture.componentRef.setInput('target', 'productId');
    fixture.detectChanges();
    pending.next(variants);
    expect(component.variants).toEqual([]);
    expect(component.products).toEqual([]);
    expect(component.search).toBe('');
  }));

  it('surfaces lookup errors and allows a subsequent barcode search', fakeAsync(() => {
    service.searchVariants.and.returnValue(throwError(() =>
      new HttpErrorResponse({ status: 500, error: { detail: 'Ricerca non disponibile' } })));
    component.queries.next('Scarpa');
    tick(200);
    expect(component.error).toBe('Ricerca non disponibile');
    service.searchVariants.and.returnValue(of([variants[0]]));
    component.queries.next('RED40');
    tick(200);
    expect(component.error).toBe('');
    expect(component.variants).toEqual([variants[0]]);
  }));
});
