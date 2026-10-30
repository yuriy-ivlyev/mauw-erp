import '../../../locale';
import { TestBed } from '@angular/core/testing';
import { convertToParamMap } from '@angular/router';
import { ActivatedRoute, Router, provideRouter } from '@angular/router';
import { of } from 'rxjs';
import { Invoice } from '../../../models/invoice.model';
import { Order } from '../../../models/order.model';
import { AuthService } from '../../../services/auth.service';
import { InvoiceService } from '../../../services/invoice.service';
import { OrderService } from '../../../services/order.service';
import { ProductService } from '../../../services/product.service';
import { OrderDetailComponent } from './order-detail.component';

describe('OrderDetailComponent', () => {
  let order: Order;
  let service: jasmine.SpyObj<OrderService>;
  let invoiceService: jasmine.SpyObj<InvoiceService>;
  let productService: jasmine.SpyObj<ProductService>;
  let authService: jasmine.SpyObj<AuthService>;

  beforeEach(async () => {
    order = {
      id: 'order-1',
      orderNumber: 'ORD-1',
      contactId: 'contact-1',
      contactName: 'Cliente',
      orderDate: '2026-10-28',
      customerDiscountRate: 0,
      status: 'Confirmed',
      statusDescription: 'Confermato',
      isReadOnly: true,
      permittedActions: [
        {
          action: 'PutOnHold',
          label: 'Metti in Sospeso',
          endpoint: '/api/v1/orders/order-1/hold',
          method: 'POST',
          style: 'warning',
          requiresDialog: true,
          dialogType: 'CancelReason'
        },
        {
          action: 'Complete',
          label: 'Chiudi / Completa Ordine',
          endpoint: '/api/v1/orders/order-1/complete',
          method: 'POST',
          style: 'primary',
          requiresDialog: false,
          dialogType: null
        }
      ],
      taxableTotal: 20,
      vatTotal: 4.4,
      grossTotal: 24.4,
      orderType: 'Standard',
      fulfillmentType: 'StandardShipping',
      shippingCost: 0,
      shippingVatRate: null,
      shippingNatureCode: null,
      items: [{
        id: 'line-1',
        orderId: 'order-1',
        variantId: 'variant-1',
        quantity: 2,
        unitNetPrice: 10,
        vatRate: 22,
        lineNetTotal: 20,
        lineVatTotal: null,
        lineGrossTotal: null,
        natureCode: null,
        discountPercent: 0,
        shippedQuantity: 1,
        remainingQuantity: 1,
        sku: 'SKU-M-ROSSO',
        productName: 'Maglia',
        sizeName: 'M',
        colorName: 'Rosso'
      }],
      linkedDocuments: null
    };
    service = jasmine.createSpyObj<OrderService>('OrderService', [
      'getById', 'postAction', 'addItem', 'removeItem', 'updateItem', 'updateShippingCharge',
      'updateCompletedInvoiceShippingCharge', 'addBillingItem', 'removeBillingItem',
      'downloadOrderPdf', 'downloadInvoicePdf', 'downloadDdtPdf'
    ]);
    invoiceService = jasmine.createSpyObj<InvoiceService>('InvoiceService', ['generateFromOrder']);
    productService = jasmine.createSpyObj<ProductService>('ProductService', ['searchVariants', 'getProduct']);
    authService = jasmine.createSpyObj<AuthService>('AuthService', ['hasRole']);
    authService.hasRole.and.callFake(role => role === 'Admin');
    service.getById.and.returnValue(of(order));
    productService.searchVariants.and.returnValue(of([]));
    service.postAction.and.returnValue(of(order));
    service.addItem.and.returnValue(of(order.items[0]));
    service.removeItem.and.returnValue(of(order));
    service.updateItem.and.returnValue(of(order));
    service.updateShippingCharge.and.returnValue(of({
      ...order,
      shippingCost: 10,
      shippingVatRate: 22,
      taxableTotal: 30,
      grossTotal: 36.6
    }));
    service.updateCompletedInvoiceShippingCharge.and.returnValue(of({
      ...order,
      shippingCost: 10,
      shippingVatRate: 22,
      taxableTotal: 30,
      grossTotal: 36.6
    }));
    service.removeBillingItem.and.returnValue(of(order));
    const invoice: Invoice = {
      id: 'invoice-1',
      invoiceNumber: 'FAT-202610-00001',
      contactId: order.contactId,
      orderId: order.id,
      contactName: order.contactName,
      invoiceDate: order.orderDate,
      taxableTotal: order.taxableTotal,
      vatTotal: order.vatTotal,
      grossTotal: order.grossTotal,
      shippingCost: 0,
      shippingVatRate: null,
      shippingNatureCode: null,
      sdiSubmittedAt: null,
      paidAmount: 0,
      balanceDue: order.grossTotal,
      status: 'Confirmed',
      items: [],
      payments: []
    };
    invoiceService.generateFromOrder.and.returnValue(of(invoice));

    await TestBed.configureTestingModule({
      imports: [OrderDetailComponent],
      providers: [
        provideRouter([]),
        {
          provide: ActivatedRoute,
          useValue: {
            paramMap: of(convertToParamMap({ id: order.id })),
            queryParamMap: of(convertToParamMap({ returnTo: '/fatturazione/fatture/invoice-1' })),
            snapshot: { data: {} }
          }
        },
        { provide: OrderService, useValue: service },
        { provide: InvoiceService, useValue: invoiceService },
        { provide: ProductService, useValue: productService },
        { provide: AuthService, useValue: authService }
      ]
    }).compileComponents();
  });

  it('renders only the backend-provided actions and read-only shipment quantities', () => {
    const fixture = TestBed.createComponent(OrderDetailComponent);
    fixture.detectChanges();

    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(text).toContain('Metti in Sospeso');
    expect(text).toContain('Chiudi / Completa Ordine');
    expect(text).toContain('Q.tà spedita');
    expect(text).toContain('Q.tà residua');
    expect(text).toContain('SKU-M-ROSSO');
    expect(text).toContain('Maglia · M · Rosso');
    expect(text).not.toContain('variant-1');
    expect(text).not.toContain('Conferma Ordine');
    expect(service.getById).toHaveBeenCalledWith(order.id);
  });

  it('uses a back button to return to the originating invoice', () => {
    const fixture = TestBed.createComponent(OrderDetailComponent);
    fixture.detectChanges();

    const navigation = spyOn(TestBed.inject(Router), 'navigateByUrl')
      .and.returnValue(Promise.resolve(true));
    const backButton = fixture.nativeElement.querySelector('button') as HTMLButtonElement;
    expect(backButton.textContent).toContain('Indietro');
    backButton.click();

    expect(navigation).toHaveBeenCalledWith('/fatturazione/fatture/invoice-1');
  });

  it('saves a classified shipping charge on the draft order', () => {
    order.status = 'Draft';
    order.statusDescription = 'Bozza';
    order.isReadOnly = false;
    const fixture = TestBed.createComponent(OrderDetailComponent);
    fixture.detectChanges();
    const component = fixture.componentInstance;
    component.shippingCostEdit = 10;
    component.shippingVatRateEdit = 22;

    component.saveShippingCharge();

    expect(service.updateShippingCharge).toHaveBeenCalledWith(order.id, {
      shippingCost: 10,
      vatRate: 22,
      natureCode: null
    });
    expect(component.order?.shippingCost).toBe(10);
    expect(component.successMessage).toContain('Costo di spedizione aggiornato');
  });

  it('shows and saves a new item on a draft order', () => {
    order.status = 'Draft';
    order.statusDescription = 'Bozza';
    order.isReadOnly = false;
    const fixture = TestBed.createComponent(OrderDetailComponent);
    fixture.detectChanges();
    const component = fixture.componentInstance;
    component.selectedBillingVariant = {
      variantId: 'variant-new',
      productId: 'product-new',
      productName: 'Camicia',
      sku: 'CAM-M',
      barcode: '123456789',
      colorName: 'Blu',
      sizeName: 'M',
      currentStock: 5,
      supplierCode: 'CAM'
    };
    component.selectedBillingProduct = {
      id: 'product-new',
      supplierId: 'supplier-1',
      supplierCode: 'CAM',
      barcode: '123456789',
      name: 'Camicia',
      sellingPrice: 15,
      vatRate: 22,
      hasVariants: true,
      variants: []
    };
    component.billingQuantity = 2;
    component.newItemUnitNetPrice = 15;

    expect((fixture.nativeElement as HTMLElement).textContent).toContain('Aggiungi articolo all’ordine');
    component.addDraftItem();

    expect(service.addItem).toHaveBeenCalledWith(order.id, {
      variantId: 'variant-new',
      quantity: 2,
      unitNetPrice: 15,
      vatRate: 22,
      natureCode: null,
      discountPercent: 0
    });
    expect(component.successMessage).toContain('Articolo aggiunto all’ordine');
  });

  it('limits draft-order item search to available stock', () => {
    order.status = 'Draft';
    order.statusDescription = 'Bozza';
    order.isReadOnly = false;
    const fixture = TestBed.createComponent(OrderDetailComponent);
    fixture.detectChanges();
    const component = fixture.componentInstance;
    component.billingProductSearch = 'camicia';

    component.searchBillingProducts();

    expect(productService.searchVariants).toHaveBeenCalledWith('camicia', 30, undefined, true);
  });

  it('requires confirmation before deleting a draft order item', () => {
    order.status = 'Draft';
    order.statusDescription = 'Bozza';
    order.isReadOnly = false;
    const fixture = TestBed.createComponent(OrderDetailComponent);
    fixture.detectChanges();
    const component = fixture.componentInstance;
    const deleteButton = fixture.nativeElement.querySelector(
      'button[aria-label^="Elimina "]'
    ) as HTMLButtonElement;

    expect(deleteButton).not.toBeNull();
    deleteButton.click();
    fixture.detectChanges();

    expect(component.pendingDraftRemoval).toBe(order.items[0]);
    expect((fixture.nativeElement as HTMLElement).textContent).toContain('Eliminare l’articolo dall’ordine?');
    expect(service.removeItem).not.toHaveBeenCalled();

    component.confirmRemoveDraftItem();

    expect(service.removeItem).toHaveBeenCalledWith(order.id, order.items[0].id);
    expect(component.pendingDraftRemoval).toBeNull();
    expect(component.successMessage).toContain('Articolo eliminato dall’ordine');
  });

  it('generates the invoice from a completed order and navigates to its detail', () => {
    const fixture = TestBed.createComponent(OrderDetailComponent);
    fixture.detectChanges();
    const component = fixture.componentInstance;
    const router = TestBed.inject(Router);
    const navigation = spyOn(router, 'navigate').and.returnValue(Promise.resolve(true));
    component.order = {
    ...order,
    status: 'Completed',
    canEditInvoice: true,
    linkedDocuments: null
    };

    component.generateInvoice();

    expect(invoiceService.generateFromOrder).toHaveBeenCalledWith(order.id);
    expect(navigation).toHaveBeenCalledWith(['/fatturazione/fatture', 'invoice-1'], {
    queryParams: { returnTo: router.url },
    state: { successMessage: jasmine.stringMatching(/FAT-202610-00001/) }
    });
  });

  it('confirms removal of a completed order line before excluding it from the invoice', () => {
    const fixture = TestBed.createComponent(OrderDetailComponent);
    fixture.detectChanges();
    const component = fixture.componentInstance;
    const item = order.items[0];
    component.order = { ...order, status: 'Completed', canEditInvoice: true };

    component.askRemoveBillingItem(item);
    expect(component.pendingBillingRemoval).toBe(item);
    component.confirmRemoveBillingItem();

    expect(service.removeBillingItem).toHaveBeenCalledWith(order.id, item.id);
    expect(component.pendingBillingRemoval).toBeNull();
  });

  it('collects a reason only for actions that request a dialog and posts the server endpoint', () => {
    const fixture = TestBed.createComponent(OrderDetailComponent);
    fixture.detectChanges();
    const component = fixture.componentInstance;
    const holdAction = order.permittedActions[0];

    component.openAction(holdAction);
    expect(component.dialogAction).toBe(holdAction);
    component.reason = 'Verifica disponibilità';
    component.submitDialog();

    expect(service.postAction).toHaveBeenCalledWith(
      holdAction,
      { reason: 'Verifica disponibilità' }
    );
    expect(component.dialogAction).toBeNull();
  });

  it('posts non-dialog actions directly and refreshes the local order', () => {
    const fixture = TestBed.createComponent(OrderDetailComponent);
    fixture.detectChanges();
    const component = fixture.componentInstance;
    const completeAction = order.permittedActions[1];

    component.openAction(completeAction);

    expect(service.postAction).toHaveBeenCalledWith(completeAction, undefined);
    expect(service.getById).toHaveBeenCalledTimes(2);
    expect(component.successMessage).toContain(completeAction.label);
  });
});
