import '../../../locale';
import { TestBed } from '@angular/core/testing';
import { convertToParamMap } from '@angular/router';
import { ActivatedRoute, Router, provideRouter } from '@angular/router';
import { of } from 'rxjs';
import { Invoice } from '../../../models/invoice.model';
import { Order, OrderItem, OrderPermittedAction } from '../../../models/order.model';
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
      'updateCompletedInvoiceShippingCharge', 'addBillingItem', 'removeBillingItem', 'updateAdjustmentLines',
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
    service.updateAdjustmentLines.and.returnValue(of(order));
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

  describe('pre-SDI line adjustment', () => {
    const reopenAction: OrderPermittedAction = {
      action: 'ReopenForAdjustment',
      label: 'Modifica Righe (Pre-SDI)',
      endpoint: '/api/v1/orders/order-1/reopen-adjustment',
      method: 'POST',
      style: 'warning',
      requiresDialog: false,
      dialogType: null
    };
    const syncAction: OrderPermittedAction = {
      action: 'SyncAndFinalizeInvoice',
      label: 'Aggiorna Fattura e Chiudi',
      endpoint: '/api/v1/orders/order-1/sync-invoice',
      method: 'POST',
      style: 'primary',
      requiresDialog: false,
      dialogType: null
    };
    const banner = 'Attenzione: Modifica in corso per fattura non ancora trasmessa allo SDI';
    const syncHint = 'Salva le modifiche alle righe prima di aggiornare la fattura';

    function createLine(id: string, quantity: number, overrides: Partial<OrderItem> = {}): OrderItem {
      return {
        ...order.items[0],
        id,
        variantId: `variant-${id}`,
        quantity,
        lineNetTotal: quantity * 10,
        shippedQuantity: Math.max(quantity, 0),
        remainingQuantity: 0,
        sku: `SKU-${id}`,
        productName: `Articolo ${id}`,
        ...overrides
      };
    }

    function completedOrder(overrides: Partial<Order> = {}): Order {
      return {
        ...order,
        status: 'Completed',
        statusDescription: 'Completato',
        isReadOnly: true,
        canEditInvoice: false,
        isUnderAdjustment: false,
        invoiceSdiStatus: 'NotTransmitted',
        permittedActions: [reopenAction],
        linkedDocuments: {
          invoiceId: 'invoice-1',
          invoiceNumber: 'FAT-1',
          ddtId: null,
          ddtNumber: null,
          creditNotes: []
        },
        items: [createLine('line-1', 2), createLine('line-2', 1)],
        ...overrides
      };
    }

    function adjustingOrder(overrides: Partial<Order> = {}): Order {
      return completedOrder({
        isReadOnly: false,
        isUnderAdjustment: true,
        permittedActions: [syncAction],
        items: [
          createLine('line-1', 2),
          createLine('line-2', 1),
          createLine('line-return', -1, { pricingSnapshot: { isCustomerReturn: true } }),
          createLine('line-excluded', 4, { isExcludedFromInvoice: true })
        ],
        ...overrides
      });
    }

    function buttonByText(fixture: { nativeElement: unknown }, text: string): HTMLButtonElement | undefined {
      return Array.from((fixture.nativeElement as HTMLElement).querySelectorAll('button'))
        .find(button => button.textContent?.includes(text));
    }

    function quantityInputs(fixture: { nativeElement: unknown }): HTMLInputElement[] {
      return Array.from((fixture.nativeElement as HTMLElement)
        .querySelectorAll<HTMLInputElement>('input[aria-label^="Quantità riga"]'));
    }

    function typeQuantity(input: HTMLInputElement, value: string): void {
      input.value = value;
      input.dispatchEvent(new Event('input'));
    }

    async function render(current: Order) {
      service.getById.and.returnValue(of(current));
      const fixture = TestBed.createComponent(OrderDetailComponent);
      fixture.detectChanges();
      await fixture.whenStable();
      fixture.detectChanges();
      return fixture;
    }

    it('offers the reopen action on a completed order and reloads it after posting', async () => {
      const completed = completedOrder();
      const reopened = adjustingOrder();
      service.postAction.and.returnValue(of(reopened));
      service.getById.and.returnValues(of(completed), of(reopened));
      const fixture = TestBed.createComponent(OrderDetailComponent);
      fixture.detectChanges();
      await fixture.whenStable();
      fixture.detectChanges();

      const reopenButton = buttonByText(fixture, 'Modifica Righe (Pre-SDI)');
      expect(reopenButton).toBeDefined();
      expect(reopenButton!.className).toContain('bg-amber-500');
      expect(quantityInputs(fixture).length).toBe(0);
      expect((fixture.nativeElement as HTMLElement).textContent).not.toContain(banner);

      reopenButton!.click();
      fixture.detectChanges();
      await fixture.whenStable();
      fixture.detectChanges();

      expect(service.postAction).toHaveBeenCalledOnceWith(reopenAction, undefined);
      expect(service.getById).toHaveBeenCalledTimes(2);
      expect(fixture.componentInstance.successMessage).toBe('Ordine riaperto per modifica righe pre-SDI.');
      expect((fixture.nativeElement as HTMLElement).textContent).toContain(banner);
      expect(quantityInputs(fixture).length).toBe(2);
    });

    it('shows the banner and editable quantities only for lines that can be adjusted', async () => {
      const fixture = await render(adjustingOrder());
      const root = fixture.nativeElement as HTMLElement;

      expect(root.textContent).toContain(banner);
      expect(quantityInputs(fixture).map(input => input.value)).toEqual(['2', '1']);
      expect(root.querySelectorAll('button[aria-label^="Rimuovi "]').length).toBe(2);
      expect(root.textContent).toContain('Esclusa dalla fattura');
      expect(root.textContent).toContain('Reso cliente, non modificabile');
      expect(buttonByText(fixture, 'Salva modifiche righe')!.disabled).toBeTrue();
      expect(root.querySelector('app-pagination')).not.toBeNull();
      expect(root.textContent).toContain('Aggiungi articolo alle righe in modifica');
    });

    it('keeps shipping cost and legacy billing lines unavailable while under adjustment', async () => {
      const fixture = await render(adjustingOrder({ canEditInvoice: true }));
      const component = fixture.componentInstance;
      const root = fixture.nativeElement as HTMLElement;

      expect(root.querySelector('input[aria-label="Costo spedizione netto"]')).toBeNull();
      expect(root.querySelector('button[aria-label="Salva costo di spedizione"]')).toBeNull();
      expect(root.querySelector('button[aria-label="Salva modifiche riga"]')).toBeNull();
      expect(component.canEditCompletedInvoice(component.order!)).toBeFalse();

      component.shippingCostEdit = 10;
      component.shippingVatRateEdit = 22;
      component.saveShippingCharge();
      component.saveItem(component.order!.items[0]);

      expect(service.updateShippingCharge).not.toHaveBeenCalled();
      expect(service.updateCompletedInvoiceShippingCharge).not.toHaveBeenCalled();
      expect(service.updateItem).not.toHaveBeenCalled();
    });

    it('asks for confirmation before removing an adjustment line', async () => {
      const fixture = await render(adjustingOrder());
      const root = fixture.nativeElement as HTMLElement;

      (root.querySelector('button[aria-label^="Rimuovi "]') as HTMLButtonElement).click();
      fixture.detectChanges();

      expect(fixture.componentInstance.pendingAdjustmentRemoval?.orderItemId).toBe('line-1');
      expect(root.textContent).toContain('Rimuovere la riga dall’ordine?');
      expect(quantityInputs(fixture).length).toBe(2);

      (root.querySelector('[role="alertdialog"] button') as HTMLButtonElement).click();
      fixture.detectChanges();
      expect(fixture.componentInstance.pendingAdjustmentRemoval).toBeNull();
      expect(quantityInputs(fixture).length).toBe(2);
      expect(fixture.componentInstance.hasPendingAdjustments()).toBeFalse();

      (root.querySelector('button[aria-label^="Rimuovi "]') as HTMLButtonElement).click();
      fixture.detectChanges();
      buttonByText(fixture, 'Conferma rimozione')!.click();
      fixture.detectChanges();

      expect(quantityInputs(fixture).length).toBe(1);
      expect(fixture.componentInstance.hasPendingAdjustments()).toBeTrue();
      expect(service.updateAdjustmentLines).not.toHaveBeenCalled();
    });

    it('saves the complete desired set of lines and reloads the order', async () => {
      const saved = adjustingOrder({
        items: [createLine('line-1', 3), createLine('line-return', -1), createLine('line-new', 2)]
      });
      service.updateAdjustmentLines.and.returnValue(of(saved));
      const fixture = await render(adjustingOrder());
      const component = fixture.componentInstance;
      const root = fixture.nativeElement as HTMLElement;

      typeQuantity(quantityInputs(fixture)[0], '3');
      fixture.detectChanges();
      (root.querySelectorAll('button[aria-label^="Rimuovi "]')[1] as HTMLButtonElement).click();
      fixture.detectChanges();
      buttonByText(fixture, 'Conferma rimozione')!.click();
      fixture.detectChanges();
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
      component.addAdjustmentLine();
      fixture.detectChanges();

      expect(root.textContent).toContain('Nuova riga, da salvare');
      expect(service.updateAdjustmentLines).not.toHaveBeenCalled();

      buttonByText(fixture, 'Salva modifiche righe')!.click();
      fixture.detectChanges();

      expect(service.updateAdjustmentLines).toHaveBeenCalledOnceWith(order.id, {
        lines: [
          { orderItemId: 'line-1', variantId: null, quantity: 3 },
          { orderItemId: 'line-return', variantId: null, quantity: -1 },
          { orderItemId: null, variantId: 'variant-new', quantity: 2, natureCode: null }
        ]
      });
      expect(service.getById).toHaveBeenCalledTimes(2);
      expect(component.hasPendingAdjustments()).toBeFalse();
      expect(component.successMessage).toContain('Righe ordine salvate');
    });

    it('requires the Natura IVA for a new zero-rate line and validates quantities', async () => {
      const fixture = await render(adjustingOrder());
      const component = fixture.componentInstance;
      component.selectedBillingVariant = {
        variantId: 'variant-exempt',
        productId: 'product-exempt',
        productName: 'Servizio',
        sku: 'SRV',
        barcode: '987654321',
        colorName: null,
        sizeName: null,
        currentStock: 0,
        supplierCode: null
      };
      component.selectedBillingProduct = {
        id: 'product-exempt',
        supplierId: 'supplier-1',
        supplierCode: 'SRV',
        barcode: '987654321',
        name: 'Servizio',
        sellingPrice: 15,
        vatRate: 0,
        hasVariants: false,
        variants: []
      };

      component.addAdjustmentLine();
      expect(component.errorMessage).toContain('Natura IVA');
      expect(component.hasPendingAdjustments()).toBeFalse();

      component.billingNatureCode = 'N3.2';
      component.addAdjustmentLine();
      expect(component.hasPendingAdjustments()).toBeTrue();

      typeQuantity(quantityInputs(fixture)[0], '0');
      component.saveAdjustmentLines();

      expect(component.errorMessage).toContain('numero intero maggiore di zero');
      expect(service.updateAdjustmentLines).not.toHaveBeenCalled();
    });

    it('disables the sync action while line edits are unsaved and enables it after saving', async () => {
      service.updateAdjustmentLines.and.returnValue(of(adjustingOrder()));
      const fixture = await render(adjustingOrder());
      const component = fixture.componentInstance;

      expect(buttonByText(fixture, 'Aggiorna Fattura e Chiudi')!.disabled).toBeFalse();

      typeQuantity(quantityInputs(fixture)[0], '5');
      fixture.detectChanges();
      const syncButton = buttonByText(fixture, 'Aggiorna Fattura e Chiudi')!;

      expect(syncButton.disabled).toBeTrue();
      expect(syncButton.title).toBe(syncHint);
      expect((fixture.nativeElement as HTMLElement).textContent).toContain(syncHint);
      component.openAction(syncAction);
      expect(service.postAction).not.toHaveBeenCalled();

      buttonByText(fixture, 'Salva modifiche righe')!.click();
      fixture.detectChanges();

      expect(service.updateAdjustmentLines).toHaveBeenCalledTimes(1);
      expect(buttonByText(fixture, 'Aggiorna Fattura e Chiudi')!.disabled).toBeFalse();
      expect(buttonByText(fixture, 'Aggiorna Fattura e Chiudi')!.title).toBe('');
    });

    it('discards unsaved line edits and restores the stored quantities', async () => {
      const fixture = await render(adjustingOrder());

      typeQuantity(quantityInputs(fixture)[0], '9');
      fixture.detectChanges();
      expect(fixture.componentInstance.hasPendingAdjustments()).toBeTrue();

      buttonByText(fixture, 'Annulla modifiche')!.click();
      fixture.detectChanges();
      await fixture.whenStable();
      fixture.detectChanges();

      expect(fixture.componentInstance.hasPendingAdjustments()).toBeFalse();
      expect(quantityInputs(fixture)[0].value).toBe('2');
    });

    it('returns to read-only after syncing the invoice and still downloads a fresh invoice PDF', async () => {
      const adjusting = adjustingOrder();
      const synced = completedOrder({ permittedActions: [] });
      service.postAction.and.returnValue(of(synced));
      service.getById.and.returnValues(of(adjusting), of(synced));
      service.downloadInvoicePdf.and.callFake(() => of(new Blob(['pdf'], { type: 'application/pdf' })));
      const open = spyOn(window, 'open');
      spyOn(URL, 'createObjectURL').and.returnValue('blob:invoice');
      spyOn(URL, 'revokeObjectURL');
      spyOn(HTMLAnchorElement.prototype, 'click');
      const fixture = TestBed.createComponent(OrderDetailComponent);
      fixture.detectChanges();
      await fixture.whenStable();
      fixture.detectChanges();

      buttonByText(fixture, 'Aggiorna Fattura e Chiudi')!.click();
      fixture.detectChanges();
      await fixture.whenStable();
      fixture.detectChanges();
      const root = fixture.nativeElement as HTMLElement;

      expect(service.postAction).toHaveBeenCalledOnceWith(syncAction, undefined);
      expect(fixture.componentInstance.successMessage).toBe('Fattura aggiornata e rigenerata con successo');
      expect(root.textContent).toContain('Fattura aggiornata e rigenerata con successo');
      expect(root.textContent).not.toContain(banner);
      expect(root.textContent).toContain('Sola lettura');
      expect(quantityInputs(fixture).length).toBe(0);
      expect(buttonByText(fixture, 'Salva modifiche righe')).toBeUndefined();
      expect(root.querySelector('button[aria-label^="Rimuovi "]')).toBeNull();
      expect(open).not.toHaveBeenCalled();

      buttonByText(fixture, 'PDF Fattura FAT-1')!.click();
      buttonByText(fixture, 'PDF Fattura FAT-1')!.click();

      expect(service.downloadInvoicePdf).toHaveBeenCalledTimes(2);
      expect(service.downloadInvoicePdf).toHaveBeenCalledWith('invoice-1');
      expect(open).toHaveBeenCalledTimes(2);
    });
  });
});
