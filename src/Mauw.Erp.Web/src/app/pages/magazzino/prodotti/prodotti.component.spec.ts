import { TestBed } from '@angular/core/testing';
import { of, Subject, throwError } from 'rxjs';
import { ProdottiComponent } from './prodotti.component';
import { ProductService } from '../../../services/product.service';
import { ProductDto, ProductVariantDto } from '../../../models/product.model';

describe('ProdottiComponent stored selling prices', () => {
  let service: jasmine.SpyObj<ProductService>;
  let component: ProdottiComponent;
  const variant: ProductVariantDto = {
    id: 'variant', productId: 'product', barcode: 'SKU', purchaseCost: 10,
    colorId: 'RED', sizeId: 'M', size: 'M', color: 'Rosso', stockQuantity: 0,
    listPrice: 18.12345678, finalNetPrice: 18.12345678, vatAmount: 0, finalGrossPrice: 18.12345678,
    sellingPrice: 18.12345678
  };
  const product: ProductDto = {
    id: 'product', supplierId: 'supplier', supplierCode: 'SUP', internalCode: 'P', name: 'Articolo',
    sellingPrice: 23.12345678, vatRate: 22, basePurchaseCost: 10,
    hasVariants: true, variants: [variant], images: []
  };

  beforeEach(() => {
    service = jasmine.createSpyObj<ProductService>('ProductService',
      ['getProducts', 'getProductVariants', 'getColors', 'getSizes', 'getCategories', 'getCollections', 'getMaterials',
        'createProduct', 'updateProduct', 'createProductVariant', 'updateProductVariant', 'calculateSellingPrice']);
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
    service.calculateSellingPrice.and.returnValue(of(13));
    component = new ProdottiComponent(service);
  });

  afterEach(() => component.ngOnDestroy());

  it('restores only the saved price and never recalculates it on edit or cost change', () => {
    component.editProduct({ ...product, hasVariants: false });
    expect(component.formData.sellingPrice).toBe(23.12345678);
    expect(component.productMarkupRate).toBe(0);
    component.formData.basePurchaseCost = 999;
    component.saveProduct();
    const payload = service.updateProduct.calls.mostRecent().args[1];
    expect(payload.sellingPrice).toBe(23.12345678);
    expect(Object.keys(payload)).not.toContain('markupRate');
    expect(Object.keys(payload)).not.toContain('defaultMarkupRate');
    expect(service.calculateSellingPrice).not.toHaveBeenCalled();
  });

  it('uses decimal backend calculation only on request and submits just its resulting price', () => {
    component.openNewProductModal();
    component.formData = { ...component.formData, internalCode: 'NEW', name: 'Nuovo', supplierCode: 'SUP',
      basePurchaseCost: 10, hasVariants: false };
    component.productMarkupRate = 30;
    component.applyMarkup('product');
    expect(service.calculateSellingPrice).toHaveBeenCalledWith(10, 30);
    expect(component.formData.sellingPrice).toBe(13);
    component.saveProduct();
    const payload = service.createProduct.calls.mostRecent().args[0];
    expect(payload.sellingPrice).toBe(13);
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
      sellingPrice: 15.12345678 };
    component.addVariant();
    const payload = service.createProductVariant.calls.mostRecent().args[0];
    expect(payload.sellingPrice).toBe(15.12345678);
    expect(Object.keys(payload)).not.toContain('markupRate');
    expect(Object.keys(payload)).not.toContain('pricingMode');
  });

  it('keeps the variant price when cost changes and uses markup only when explicitly applied', () => {
    component.editProduct(product);
    component.startEditVariant(variant);
    expect(component.editingVariantForm.markupRate).toBe(0);
    component.editingVariantForm.purchaseCost = 999;
    component.saveVariantEdit();
    expect(service.updateProductVariant.calls.mostRecent().args[2].sellingPrice).toBe(18.12345678);
    component.startEditVariant(variant);
    component.editingVariantForm.markupRate = 30;
    component.applyMarkup('editVariant');
    expect(component.editingVariantForm.sellingPrice).toBe(13);
    component.saveVariantEdit();
    const payload = service.updateProductVariant.calls.mostRecent().args[2];
    expect(payload.sellingPrice).toBe(13);
    expect(Object.keys(payload)).not.toContain('markupRate');
  });

  it('blocks invalid variants and surfaces calculation failures without replacing the price', () => {
    component.editProduct(product);
    component.startEditVariant(variant);
    component.editingVariantForm.sellingPrice = null;
    component.saveVariantEdit();
    expect(service.updateProductVariant).not.toHaveBeenCalled();
    expect(component.variantError).toBeTruthy();
    service.calculateSellingPrice.and.returnValue(throwError(() => ({ error: 'Calcolo non valido' })));
    component.applyMarkup('product');
    expect(component.formData.sellingPrice).toBeNull();
    expect(component.formError).toBe('Calcolo non valido');
    component.productMarkupRate = -1;
    component.applyMarkup('product');
    expect(service.calculateSellingPrice).toHaveBeenCalledTimes(1);
  });

  it('blocks saving during calculation and ignores results after closing the form', () => {
    const response = new Subject<number>();
    service.calculateSellingPrice.and.returnValue(response);
    component.editProduct(product);
    component.applyMarkup('product');
    component.saveProduct();
    expect(service.updateProduct).not.toHaveBeenCalled();
    component.closeModal();
    response.next(500);
    expect(component.formData.sellingPrice).toBeNull();
    expect(component.calculatingPrice).toBeFalse();
  });

  it('always displays an editable final price, even without any markup', async () => {
    await TestBed.configureTestingModule({
      imports: [ProdottiComponent],
      providers: [{ provide: ProductService, useValue: service }]
    }).compileComponents();
    const fixture = TestBed.createComponent(ProdottiComponent);
    fixture.componentInstance.editProduct({ ...product, hasVariants: false });
    fixture.detectChanges();
    await fixture.whenStable();
    const input: HTMLInputElement = fixture.nativeElement.querySelector('[aria-label="Prezzo vendita prodotto netto"]');
    expect(input.value).toBe('23.12345678');
    expect(fixture.nativeElement.querySelector('[aria-label="Ricarico prodotto (%)"]')).not.toBeNull();
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
    component.formData = { ...component.formData, internalCode: 'P', name: 'Scarpa', supplierCode: 'S' };
    component.saveProduct();
    expect(service.createProduct.calls.mostRecent().args[0].sellingPrice).toBeNull();
    await TestBed.configureTestingModule({
      imports: [ProdottiComponent], providers: [{ provide: ProductService, useValue: service }]
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
      imports: [ProdottiComponent], providers: [{ provide: ProductService, useValue: service }]
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
