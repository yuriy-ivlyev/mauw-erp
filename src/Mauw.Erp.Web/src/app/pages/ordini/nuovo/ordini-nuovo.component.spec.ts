import { fakeAsync, TestBed, tick } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { of } from 'rxjs';
import { ContactSummary } from '../../../models/contact.model';
import { ProductDto, VariantSearchDto } from '../../../models/product.model';
import { Order } from '../../../models/order.model';
import { ContactService } from '../../../services/contact.service';
import { OrderService } from '../../../services/order.service';
import { ProductService } from '../../../services/product.service';
import { OrdiniNuovoComponent } from './ordini-nuovo.component';

describe('OrdiniNuovoComponent', () => {
  let contacts: jasmine.SpyObj<ContactService>;
  let orders: jasmine.SpyObj<OrderService>;
  let products: jasmine.SpyObj<ProductService>;
  let order: Order;
  let customer: ContactSummary;
  let variant: VariantSearchDto;
  let product: ProductDto;

  beforeEach(async () => {
    customer = {
      id: 'contact-1',
      businessName: 'Cliente B2B',
      tradeName: null,
      vatNumber: '01234567890',
      taxCode: '01234567890',
      city: 'Milano',
      province: 'MI',
      status: 'Active',
      isCustomer: true,
      isSupplier: false,
      isAgent: false,
      createdAt: '2026-10-28'
    };
    variant = {
      variantId: 'variant-1',
      productId: 'product-1',
      productName: 'Abito',
      sku: 'ABITO-M-ROSSO',
      barcode: '800000000001',
      colorName: 'Rosso',
      sizeName: 'M',
      currentStock: 12,
      supplierCode: null
    };
    product = {
      sellingPrice: 49.9,
      id: 'product-1',
      supplierId: 'supplier-1',
      supplierCode: '',
      barcode: '800000000001',
      name: 'Abito',
      vatRate: 22,
      basePurchaseCost: 0,
      hasVariants: true,
      variants: [{
        id: 'variant-1',
        productId: 'product-1',
        size: 'M',
        color: 'Rosso',
        barcode: '800000000001',
        stockQuantity: 12,
        sellingPrice: 49.9,
        listPrice: 49.9,
        finalNetPrice: 49.9,
        vatAmount: 10.98,
        finalGrossPrice: 60.88
      }]
    };
    order = {
      id: 'order-1',
      orderNumber: 'ORD-1',
      contactId: customer.id,
      contactName: customer.businessName,
      orderDate: '2026-10-28',
      customerDiscountRate: 0,
      status: 'Draft',
      statusDescription: 'Bozza',
      isReadOnly: false,
      permittedActions: [],
      taxableTotal: 49.9,
      vatTotal: 10.98,
      grossTotal: 60.88,
      orderType: 'Standard',
      fulfillmentType: 'StandardShipping',
      items: [],
      linkedDocuments: null
    };

    contacts = jasmine.createSpyObj<ContactService>('ContactService', ['getList']);
    orders = jasmine.createSpyObj<OrderService>('OrderService', [
      'createOrder', 'addItem', 'previewDraftItems'
    ]);
    products = jasmine.createSpyObj<ProductService>('ProductService', ['searchVariants', 'getProduct']);
    contacts.getList.and.returnValue(of({
      items: [customer], pageNumber: 1, pageSize: 20, totalCount: 1, totalPages: 1
    }));
    orders.createOrder.and.returnValue(of(order));
    orders.addItem.and.returnValue(of({
      id: 'line-1',
      orderId: order.id,
      variantId: variant.variantId,
      quantity: 2,
      unitNetPrice: 49.9,
      vatRate: 22,
      lineNetTotal: 99.8,
      lineVatTotal: null,
      lineGrossTotal: null,
      natureCode: null,
      discountPercent: 0,
      shippedQuantity: 0,
      remainingQuantity: 2
    }));
    orders.previewDraftItems.and.returnValue(of({
      lines: [{ lineNetTotal: 99.8 }, { lineNetTotal: 5 }],
      totalTaxable: 104.8,
      totalVat: 22.46,
      totalGross: 127.26
    }));
    products.searchVariants.and.returnValue(of([variant]));
    products.getProduct.and.returnValue(of(product));

    await TestBed.configureTestingModule({
      imports: [OrdiniNuovoComponent],
      providers: [
        provideRouter([]),
        { provide: ContactService, useValue: contacts },
        { provide: OrderService, useValue: orders },
        { provide: ProductService, useValue: products }
      ]
    }).compileComponents();
  });

  it('renders the complete draft form with submit disabled until customer and lines are selected', () => {
    const fixture = TestBed.createComponent(OrdiniNuovoComponent);
    fixture.detectChanges();

    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(text).toContain('Tipo evasione');
    expect(text).toContain('Note interne');
    expect(text).toContain('Costo spedizione netto');
    expect(text).toContain('Codice SKU');
    expect(text).toContain('Taglia');
    expect(text).toContain('Colore');
    expect(text).toContain('Totale imponibile');
    expect(text).toContain('Crea Bozza Ordine');
    expect((fixture.nativeElement.querySelector('button[type="submit"]') as HTMLButtonElement).disabled).toBeTrue();
  });

  it('searches contacts and variants, selects garment size/color, and displays the server fiscal preview', fakeAsync(() => {
    const fixture = TestBed.createComponent(OrdiniNuovoComponent);
    fixture.detectChanges();
    const component = fixture.componentInstance;

    component.onCustomerSearch('Cliente');
    tick(260);
    expect(contacts.getList).toHaveBeenCalledWith({
      search: 'Cliente',
      isCustomer: true,
      status: 'Active',
      page: 1,
      pageSize: 20
    });
    component.selectCustomer(customer);
    component.shippingCost = 5;
    component.shippingVatRate = 10;

    component.productSearch = 'ABITO';
    component.searchProducts();
    expect(products.searchVariants).toHaveBeenCalledWith('ABITO', 30, undefined, true);
    component.selectVariant(variant);
    expect(component.selectedVariantDetails?.size).toBe('M');
    expect(component.selectedVariantDetails?.color).toBe('Rosso');
    expect(component.pendingUnitNetPrice).toBe(49.9);

    component.pendingQuantity = 2;
    component.addDraftLine();
    tick(220);
    fixture.detectChanges();

    expect(orders.previewDraftItems).toHaveBeenCalledWith([{
        variantId: 'variant-1',
        quantity: 2,
        unitNetPrice: 49.9,
        vatRate: 22,
        natureCode: null,
        discountPercent: 0
      }],
      5,
      10,
      null
    );
    expect(component.lineSubtotal(0)).toBe(99.8);
    expect(component.totalPieces).toBe(2);
    expect(fixture.nativeElement.textContent).toContain('127,26');
  }));

  it('creates the order header, adds its lines, and navigates to the permitted-action detail', () => {
    const fixture = TestBed.createComponent(OrdiniNuovoComponent);
    fixture.detectChanges();
    const component = fixture.componentInstance;
    component.selectedCustomer = customer;
    component.fulfillmentType = 'CashDesk';
    component.internalNotes = 'Chiamare prima della consegna';
    component.shippingCost = 5;
    component.shippingVatRate = 10;
    component.draftLines.push({
      variantId: variant.variantId,
      sku: variant.sku!,
      description: 'Abito',
      size: 'M',
      color: 'Rosso',
      quantity: 2,
      unitNetPrice: 49.9,
      vatRate: 22,
      natureCode: null
    });
    const router = TestBed.inject(Router);
    spyOn(router, 'navigate').and.returnValue(Promise.resolve(true));

    component.createDraft();

    expect(orders.createOrder).toHaveBeenCalledWith({
      contactId: customer.id,
      orderType: 'CassaSession',
      fulfillmentType: 'CashDesk',
      internalNotes: 'Chiamare prima della consegna',
      shippingCost: 5,
      shippingVatRate: 10,
      shippingNatureCode: null
    });
    expect(orders.addItem).toHaveBeenCalledWith(order.id, {
      variantId: variant.variantId,
      quantity: 2,
      unitNetPrice: 49.9,
      vatRate: 22,
      natureCode: null,
      discountPercent: 0
    });
    expect(router.navigate).toHaveBeenCalledWith(['/vendite/ordini', order.id], {
      replaceUrl: true,
      state: { successMessage: `Bozza ordine ${order.orderNumber} creata correttamente.` }
    });
  });

  it('requires confirmation before removing a draft line', () => {
    const fixture = TestBed.createComponent(OrdiniNuovoComponent);
    fixture.detectChanges();
    const component = fixture.componentInstance;
    component.draftLines.push({
      variantId: variant.variantId,
      sku: variant.sku!,
      description: 'Abito',
      size: 'M',
      color: 'Rosso',
      quantity: 1,
      unitNetPrice: 49.9,
      vatRate: 22,
      natureCode: null
    });

    component.removeDraftLine(0);
    fixture.detectChanges();

    expect(component.draftLines).toHaveSize(1);
    expect(fixture.nativeElement.textContent).toContain('Eliminare la riga dall’ordine?');

    component.confirmRemoveDraftLine();

    expect(component.draftLines).toHaveSize(0);
    expect(component.pendingDraftLineRemovalIndex).toBeNull();
  });
});
