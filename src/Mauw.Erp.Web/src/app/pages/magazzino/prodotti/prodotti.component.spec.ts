import { TestBed } from '@angular/core/testing';
import { HttpErrorResponse } from '@angular/common/http';
import { provideRouter } from '@angular/router';
import { of, Subject, throwError } from 'rxjs';
import { ProdottiComponent } from './prodotti.component';
import { ProductService } from '../../../services/product.service';
import { PriceFormulaService } from '../../../services/price-formula.service';
import { AuthService } from '../../../services/auth.service';
import { PriceFormulaCalculation } from '../../../models/price-formula.model';
import { ProductDto, ProductVariantDto } from '../../../models/product.model';

describe('ProdottiComponent stored selling prices', () => {
  let service: jasmine.SpyObj<ProductService>;
  let formulaService: jasmine.SpyObj<PriceFormulaService>;
  let isAdmin: boolean;
  let auth: AuthService;
  let component: ProdottiComponent;
  const calculation: PriceFormulaCalculation = {
    purchaseCost: 20,
    steps: [
      { label: 'Ricarico', percentage: 40, runningValue: 28 },
      { label: 'Provvigione', percentage: 10, runningValue: 30.8 },
      { label: 'Spese', percentage: 5, runningValue: 32.34 }
    ],
    unroundedPrice: 32.34,
    roundingStep: 0.1,
    finalPrice: 32.4
  };
  const problem = (detail: string, status = 400) =>
    new HttpErrorResponse({ status, error: { title: 'Bad Request', detail } });
  const providers = () => [
    provideRouter([]),
    { provide: ProductService, useValue: service },
    { provide: PriceFormulaService, useValue: formulaService },
    { provide: AuthService, useValue: auth }
  ];
  const variant: ProductVariantDto = {
    id: 'variant', productId: 'product', barcode: 'SKU', purchaseCost: 10,
    supplierCode: 'SUP-M',
    colorId: 'RED', sizeId: 'M', size: 'M', color: 'Rosso', stockQuantity: 0,
    listPrice: 18.12345678, finalNetPrice: 18.12345678, vatAmount: 0, finalGrossPrice: 18.12345678,
    sellingPrice: 18.12345678
  };
  const product: ProductDto = {
    id: 'product', supplierId: 'supplier', supplierCode: 'SUP', barcode: 'P', name: 'Articolo',
    sellingPrice: 23.12345678, vatRate: 22, basePurchaseCost: 10,
    hasVariants: true, variants: [variant]
  };

  beforeEach(() => {
    service = jasmine.createSpyObj<ProductService>('ProductService',
      ['getProducts', 'getProductVariants', 'getColors', 'getSizes', 'getCategories', 'getCollections', 'getMaterials',
        'createProduct', 'updateProduct', 'createProductVariant', 'updateProductVariant']);
    formulaService = jasmine.createSpyObj<PriceFormulaService>('PriceFormulaService', ['calculate']);
    isAdmin = true;
    auth = { hasRole: (role: string) => isAdmin && role === 'Admin' } as unknown as AuthService;
    service.getProducts.and.returnValue(of({ items: [product], pageNumber: 1, pageSize: 20, totalCount: 1, totalPages: 1 }));
    service.getProductVariants.and.returnValue(of([variant]));
    service.getColors.and.returnValue(of([]));
    service.getSizes.and.returnValue(of([]));
    service.getCategories.and.returnValue(of([]));
    service.getCollections.and.returnValue(of([]));
    service.getMaterials.and.returnValue(of([]));
    service.createProduct.and.returnValue(of(product));
    service.updateProduct.and.returnValue(of(product));
    service.createProductVariant.and.returnValue(of(variant));
    service.updateProductVariant.and.returnValue(of(variant));
    formulaService.calculate.and.returnValue(of(calculation));
    component = new ProdottiComponent(service, formulaService, auth);
  });

  afterEach(() => component.ngOnDestroy());

  it('restores only the saved price and never recalculates it on edit or cost change', () => {
    component.editProduct({ ...product, hasVariants: false });
    expect(component.formData.sellingPrice).toBe(23.12345678);
    component.formData.basePurchaseCost = 999;
    component.saveProduct();
    const payload = service.updateProduct.calls.mostRecent().args[1];
    expect(payload.sellingPrice).toBe(23.12345678);
    expect(Object.keys(payload)).not.toContain('markupRate');
    expect(Object.keys(payload)).not.toContain('defaultMarkupRate');
    expect(formulaService.calculate).not.toHaveBeenCalled();
  });

  it('calculates the selling price from cost only on request and submits just its resulting price', () => {
    component.openNewProductModal();
    component.formData = { ...component.formData, barcode: 'NEW', name: 'Nuovo', supplierCode: 'SUP',
      basePurchaseCost: 20, hasVariants: false };
    component.calculateFromCost('product');
    expect(formulaService.calculate).toHaveBeenCalledOnceWith(20);
    expect(component.formData.sellingPrice).toBe(32.4);
    expect(component.priceFeedback?.breakdown).toBe('20.00 → 28.00 → 30.80 → 32.34 → arrotondato 32.40');
    component.saveProduct();
    const payload = service.createProduct.calls.mostRecent().args[0];
    expect(payload.sellingPrice).toBe(32.4);
    expect(Object.keys(payload)).not.toContain('markupRate');
    expect(Object.keys(payload)).not.toContain('defaultMarkupRate');
  });

  it('allows direct eight-decimal and zero prices and rejects missing or invalid prices', () => {
    for (const price of [18.12345678, 0, 0.00000001]) {
      component.editProduct({ ...product, hasVariants: false });
      component.formData.sellingPrice = price;
      component.saveProduct();
    }
    expect(service.updateProduct).toHaveBeenCalledTimes(3);
    for (const price of [null, -1, 1.123456789]) {
      component.editProduct({ ...product, hasVariants: false });
      component.formData.sellingPrice = price;
      component.saveProduct();
      expect(component.formError).toBeTruthy();
    }
    expect(service.updateProduct).toHaveBeenCalledTimes(3);
  });

  it('does not copy any parent price into a variant and saves an independent scalar', () => {
    component.editProduct(product);
    expect(component.newVariantForm.sellingPrice).toBeNull();
    component.newVariantForm = { ...component.newVariantForm, barcode: 'NEW', colorId: 'RED', sizeId: 'M',
      supplierCode: 'SUP-M', sellingPrice: 15.12345678 };
    component.addVariant();
    const payload = service.createProductVariant.calls.mostRecent().args[0];
    expect(payload.sellingPrice).toBe(15.12345678);
    expect(payload.supplierCode).toBe('SUP-M');
    expect(Object.keys(payload)).not.toContain('markupRate');
    expect(Object.keys(payload)).not.toContain('pricingMode');
  });

  it('keeps the variant price when cost changes and recalculates it only when explicitly requested', () => {
    component.editProduct(product);
    component.startEditVariant(variant);
    component.editingVariantForm.purchaseCost = 999;
    component.saveVariantEdit();
    expect(service.updateProductVariant.calls.mostRecent().args[2].sellingPrice).toBe(18.12345678);
    expect(service.updateProductVariant.calls.mostRecent().args[2].supplierCode).toBe('SUP-M');
    expect(formulaService.calculate).not.toHaveBeenCalled();
    component.startEditVariant(variant);
    component.editingVariantForm.purchaseCost = 20;
    component.calculateFromCost('editVariant');
    expect(formulaService.calculate).toHaveBeenCalledOnceWith(20);
    expect(component.editingVariantForm.sellingPrice).toBe(32.4);
    component.saveVariantEdit();
    const payload = service.updateProductVariant.calls.mostRecent().args[2];
    expect(payload.sellingPrice).toBe(32.4);
    expect(Object.keys(payload)).not.toContain('markupRate');
  });

  it('fills the new variant price from its own cost', () => {
    component.editProduct(product);
    component.newVariantForm.purchaseCost = 20;
    component.calculateFromCost('newVariant');
    expect(component.newVariantForm.sellingPrice).toBe(32.4);
    expect(component.priceFeedback?.target).toBe('newVariant');
  });

  it('blocks invalid variants and surfaces calculation failures without replacing the price', () => {
    component.editProduct(product);
    component.startEditVariant(variant);
    component.editingVariantForm.sellingPrice = null;
    component.saveVariantEdit();
    expect(service.updateProductVariant).not.toHaveBeenCalled();
    expect(component.variantError).toBeTruthy();
    formulaService.calculate.and.returnValue(throwError(() => problem('Calcolo non valido')));
    component.calculateFromCost('product');
    expect(component.formData.sellingPrice).toBeNull();
    expect(component.priceFeedback).toEqual(jasmine.objectContaining({
      target: 'product', error: 'Calcolo non valido', notConfigured: false
    }));
  });

  it('rejects invalid costs client-side without calling the backend', () => {
    component.editProduct({ ...product, hasVariants: false });
    for (const cost of [-1, 1.234, null as unknown as number]) {
      component.formData.basePurchaseCost = cost;
      component.calculateFromCost('product');
      expect(component.priceFeedback?.error).toContain('costo di acquisto valido');
    }
    expect(formulaService.calculate).not.toHaveBeenCalled();
    expect(component.formData.sellingPrice).toBe(23.12345678);
  });

  it('never calls the calculation for non-admin roles', () => {
    isAdmin = false;
    component.editProduct({ ...product, hasVariants: false });
    expect(component.canCalculateFromCost).toBeFalse();
    component.calculateFromCost('product');
    expect(formulaService.calculate).not.toHaveBeenCalled();
  });

  it('blocks saving during calculation and ignores results after closing the form', () => {
    const response = new Subject<PriceFormulaCalculation>();
    formulaService.calculate.and.returnValue(response);
    component.editProduct({ ...product, hasVariants: false });
    component.formData.sellingPrice = null;
    component.calculateFromCost('product');
    expect(component.calculatingPrice).toBeTrue();
    component.saveProduct();
    expect(service.updateProduct).not.toHaveBeenCalled();
    component.closeModal();
    response.next(calculation);
    expect(component.formData.sellingPrice).toBeNull();
    expect(component.calculatingPrice).toBeFalse();
  });

  it('shows "Calcola da costo" only to Admin, fills the price and shows the breakdown', async () => {
    await TestBed.configureTestingModule({ imports: [ProdottiComponent], providers: providers() }).compileComponents();
    const fixture = TestBed.createComponent(ProdottiComponent);
    fixture.componentInstance.editProduct({ ...product, hasVariants: false, basePurchaseCost: 20 });
    fixture.detectChanges();
    await fixture.whenStable();
    const button: HTMLButtonElement = fixture.nativeElement.querySelector('[aria-label="Calcola prezzo prodotto da costo"]');
    expect(button.textContent).toContain('Calcola da costo');
    button.click();
    fixture.detectChanges();
    await fixture.whenStable();
    const input: HTMLInputElement = fixture.nativeElement.querySelector('[aria-label="Prezzo vendita prodotto netto"]');
    expect(input.value).toBe('32.4');
    expect(fixture.nativeElement.querySelector('[data-testid="price-breakdown"]').textContent)
      .toContain('20.00 → 28.00 → 30.80 → 32.34 → arrotondato 32.40');
    input.value = '40.5';
    input.dispatchEvent(new Event('input'));
    expect(fixture.componentInstance.formData.sellingPrice).toBe(40.5);
    fixture.destroy();
  });

  it('shows a link to the formula settings when the formula is not configured', async () => {
    formulaService.calculate.and.returnValue(throwError(() =>
      problem('Formula prezzi non configurata: impostarla in Impostazioni > Formula prezzi.')));
    await TestBed.configureTestingModule({ imports: [ProdottiComponent], providers: providers() }).compileComponents();
    const fixture = TestBed.createComponent(ProdottiComponent);
    fixture.componentInstance.editProduct({ ...product, hasVariants: false });
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.nativeElement.querySelector('[aria-label="Calcola prezzo prodotto da costo"]').click();
    fixture.detectChanges();
    const message: HTMLElement = fixture.nativeElement.querySelector('[data-testid="price-error"]');
    expect(message.textContent).toContain('Formula prezzi non configurata');
    expect(message.querySelector('a')?.getAttribute('href')).toBe('/impostazioni/formula-prezzi');
    expect(fixture.componentInstance.formData.sellingPrice).toBe(23.12345678);
    fixture.destroy();
  });

  it('hides the calculation buttons from non-admin users and has no markup inputs', async () => {
    isAdmin = false;
    await TestBed.configureTestingModule({ imports: [ProdottiComponent], providers: providers() }).compileComponents();
    const fixture = TestBed.createComponent(ProdottiComponent);
    fixture.componentInstance.editProduct({ ...product, hasVariants: false });
    fixture.detectChanges();
    await fixture.whenStable();
    const html: HTMLElement = fixture.nativeElement;
    expect(html.querySelector('[aria-label="Prezzo vendita prodotto netto"]')).not.toBeNull();
    expect(html.textContent).not.toContain('Calcola da costo');
    expect(html.textContent).not.toContain('ricarico');
    fixture.destroy();
  });

  it('offers the calculation on the new-variant form and the edit-variant row, without markup inputs', async () => {
    await TestBed.configureTestingModule({ imports: [ProdottiComponent], providers: providers() }).compileComponents();
    const fixture = TestBed.createComponent(ProdottiComponent);
    fixture.componentInstance.editProduct(product);
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.componentInstance.activeTab = 'variants';
    fixture.componentInstance.startEditVariant(variant);
    fixture.detectChanges();
    const html: HTMLElement = fixture.nativeElement;
    expect(html.querySelector('[aria-label="Calcola prezzo nuova variante da costo"]')).not.toBeNull();
    expect(html.querySelector('[aria-label="Calcola prezzo variante da costo"]')).not.toBeNull();
    expect(html.querySelector('[aria-label*="Ricarico"]')).toBeNull();
    expect(html.textContent).not.toContain('Applica ricarico');
    expect(html.textContent).not.toContain('Aiuto al calcolo');
    fixture.destroy();
  });

  it('always displays an editable final price, even without any calculation', async () => {
    await TestBed.configureTestingModule({ imports: [ProdottiComponent], providers: providers() }).compileComponents();
    const fixture = TestBed.createComponent(ProdottiComponent);
    fixture.componentInstance.editProduct({ ...product, hasVariants: false });
    fixture.detectChanges();
    await fixture.whenStable();
    const input: HTMLInputElement = fixture.nativeElement.querySelector('[aria-label="Prezzo vendita prodotto netto"]');
    expect(input.value).toBe('23.12345678');
    expect(fixture.nativeElement.querySelector('[aria-label="Ricarico prodotto (%)"]')).toBeNull();
    input.value = '20.12345678';
    input.dispatchEvent(new Event('input'));
    fixture.detectChanges();
    fixture.componentInstance.saveProduct();
    expect(service.updateProduct.calls.mostRecent().args[1].sellingPrice).toBe(20.12345678);
    fixture.destroy();
  });

  it('creates a variant parent without a price and hides its price editor', async () => {
    component.openNewProductModal();
    expect(component.formData.hasVariants).toBeTrue();
    component.formData = { ...component.formData, barcode: 'P', name: 'Scarpa', supplierCode: 'S' };
    component.saveProduct();
    expect(service.createProduct.calls.mostRecent().args[0].sellingPrice).toBeNull();
    await TestBed.configureTestingModule({
      imports: [ProdottiComponent], providers: providers()
    }).compileComponents();
    const fixture = TestBed.createComponent(ProdottiComponent);
    fixture.componentInstance.editProduct(product);
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('[aria-label="Prezzo vendita prodotto netto"]')).toBeNull();
    expect(fixture.nativeElement.querySelector('[aria-label="Prodotto con varianti"]')).not.toBeNull();
    fixture.destroy();
  });

  it('summarizes equal, different, missing, zero and empty variant prices without using the parent', () => {
    expect(component.sellingPriceRange(product)).toEqual({
      min: 18.12345678, max: 18.12345678, missing: false, empty: false
    });
    expect(component.sellingPriceRange({ ...product, variants: [variant, { ...variant, sellingPrice: 30 }] }))
      .toEqual({ min: 18.12345678, max: 30, missing: false, empty: false });
    expect(component.sellingPriceRange({ ...product, variants: [{ ...variant, sellingPrice: 0 }] }))
      .toEqual({ min: 0, max: 0, missing: false, empty: false });
    expect(component.sellingPriceRange({ ...product, variants: [variant, { ...variant, sellingPrice: null }] }).missing).toBeTrue();
    expect(component.sellingPriceRange({ ...product, variants: [] }))
      .toEqual({ min: null, max: null, missing: false, empty: true });
    expect(component.sellingPriceRange({ ...product, variants: [{ ...variant, sellingPrice: null }] }))
      .toEqual({ min: null, max: null, missing: true, empty: false });
    expect(component.sellingPriceRange({ ...product, hasVariants: false }).min).toBe(product.sellingPrice);
  });

  it('renders a common price or a range from actual SKU prices in the product list', async () => {
    await TestBed.configureTestingModule({
      imports: [ProdottiComponent], providers: providers()
    }).compileComponents();
    const fixture = TestBed.createComponent(ProdottiComponent);
    fixture.detectChanges();
    fixture.componentInstance.products = [{ ...product, variants: [
      { ...variant, sellingPrice: 20 }, { ...variant, id: 'other', sellingPrice: 30 }
    ] }];
    fixture.detectChanges();
    let priceCell: HTMLTableCellElement = fixture.nativeElement.querySelector('tbody tr td:nth-child(7)');
    expect(priceCell.textContent).toContain('Da');
    expect(priceCell.textContent).toContain('20.00');
    expect(priceCell.textContent).toContain('30.00');
    fixture.componentInstance.products = [{ ...product, variants: [
      { ...variant, sellingPrice: 20 }, { ...variant, id: 'other', sellingPrice: 20 }
    ] }];
    fixture.detectChanges();
    priceCell = fixture.nativeElement.querySelector('tbody tr td:nth-child(7)');
    expect(priceCell.textContent).not.toContain('Da');
    expect(priceCell.textContent).toContain('20.00');
    fixture.destroy();
  });
});
