import '../../../locale';
import { TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { of } from 'rxjs';
import { Ddt } from '../../../models/ddt.model';
import { Invoice } from '../../../models/invoice.model';
import { Order } from '../../../models/order.model';
import { AuthService } from '../../../services/auth.service';
import { DdtService } from '../../../services/ddt.service';
import { InvoiceService } from '../../../services/invoice.service';
import { OrderService } from '../../../services/order.service';
import { ProductService } from '../../../services/product.service';
import { FattureComponent } from './fatture.component';
import { FatturaDettaglioComponent } from './fattura-dettaglio.component';
import { DdtDettaglioComponent } from '../../ddt/dettaglio/ddt-dettaglio.component';
import { OrderDetailComponent } from '../../ordini/dettaglio/order-detail.component';

describe('FatturaDettaglioComponent', () => {
  let invoiceService: jasmine.SpyObj<InvoiceService>;
  let orderService: jasmine.SpyObj<OrderService>;
  let ddtService: jasmine.SpyObj<DdtService>;
  let productService: jasmine.SpyObj<ProductService>;
  let authService: jasmine.SpyObj<AuthService>;
  let invoice: Invoice;
  let order: Order;
  let ddt: Ddt;
  let harness: RouterTestingHarness;

  beforeEach(async () => {
    invoice = {
      id: 'invoice-1',
      invoiceNumber: 'INV-102026-001',
      contactId: 'contact-1',
      orderId: 'order-1',
      contactName: 'Cliente',
      invoiceDate: '2026-10-29T00:00:00Z',
      taxableTotal: 100,
      vatTotal: 22,
      grossTotal: 122,
      shippingCost: 0,
      shippingVatRate: null,
      shippingNatureCode: null,
      sdiSubmittedAt: null,
      paidAmount: 0,
      balanceDue: 122,
      status: 'Confirmed',
      items: [],
      payments: []
    };
    order = {
      id: 'order-1',
      orderNumber: 'ORD-1',
      contactId: 'contact-1',
      contactName: 'Cliente',
      orderDate: '2026-10-28T00:00:00Z',
      customerDiscountRate: 0,
      status: 'Completed',
      taxableTotal: 100,
      vatTotal: 22,
      grossTotal: 122,
      orderType: 'Standard',
      fulfillmentType: 'StandardShipping',
      items: [],
      linkedDocuments: {
        invoiceId: invoice.id,
        invoiceNumber: invoice.invoiceNumber,
        ddtId: 'ddt-1',
        ddtNumber: 'DDT-1',
        creditNotes: [],
        ddts: [{ id: 'ddt-1', ddtNumber: 'DDT-1', ddtDate: '2026-10-28T00:00:00Z' }]
      },
      statusDescription: 'Completato',
      isReadOnly: true,
      permittedActions: []
    };
    ddt = {
      id: 'ddt-1',
      ddtNumber: 'DDT-1',
      contactId: 'contact-1',
      orderId: 'order-1',
      contactName: 'Cliente',
      warehouseId: null,
      ddtDate: '2026-10-28T00:00:00Z',
      taxableTotal: 100,
      vatTotal: 22,
      grossTotal: 122,
      items: []
    };

    invoiceService = jasmine.createSpyObj<InvoiceService>('InvoiceService', [
      'getList', 'getById', 'generateFromOrder', 'recordPayment', 'downloadPdf'
    ]);
    orderService = jasmine.createSpyObj<OrderService>('OrderService', [
      'getById', 'postAction', 'updateItem', 'updateShippingCharge',
      'downloadOrderPdf', 'downloadInvoicePdf', 'downloadDdtPdf'
    ]);
    ddtService = jasmine.createSpyObj<DdtService>('DdtService', ['getById']);
    productService = jasmine.createSpyObj<ProductService>('ProductService', ['searchVariants', 'getProduct']);
    authService = jasmine.createSpyObj<AuthService>('AuthService', ['hasRole']);
    authService.hasRole.and.returnValue(false);
    invoiceService.getList.and.returnValue(of({
      items: [invoice], pageNumber: 1, pageSize: 20, totalCount: 1, totalPages: 1
    }));
    invoiceService.getById.and.returnValue(of(invoice));
    invoiceService.recordPayment.and.returnValue(of({
      ...invoice,
      paidAmount: 25.5,
      balanceDue: 96.5,
      status: 'PartiallyPaid',
      payments: [{
        id: 'payment-1',
        amount: 25.5,
        paymentDate: '2026-10-29T00:00:00Z',
        paymentMethod: 'Bonifico bancario',
        transactionDetails: 'CRO 123456'
      }]
    }));
    orderService.getById.and.returnValue(of(order));
    ddtService.getById.and.returnValue(of(ddt));

    await TestBed.configureTestingModule({
      providers: [
        provideRouter([
          { path: 'fatturazione/fatture', component: FattureComponent },
          { path: 'fatturazione/fatture/:id', component: FatturaDettaglioComponent },
          { path: 'ddt/:id', component: DdtDettaglioComponent },
          { path: 'ordini/:id', component: OrderDetailComponent }
        ]),
        { provide: InvoiceService, useValue: invoiceService },
        { provide: OrderService, useValue: orderService },
        { provide: DdtService, useValue: ddtService },
        { provide: ProductService, useValue: productService },
        { provide: AuthService, useValue: authService }
      ],
      imports: [
        FattureComponent,
        FatturaDettaglioComponent,
        DdtDettaglioComponent,
        OrderDetailComponent
      ]
    }).compileComponents();
    harness = await RouterTestingHarness.create();
  });

  async function navigateToInvoice(): Promise<FatturaDettaglioComponent> {
    const component = await harness.navigateByUrl(
      '/fatturazione/fatture/invoice-1?returnTo=%2Ffatturazione%2Ffatture',
      FatturaDettaglioComponent
    );
    harness.detectChanges();
    return component;
  }

  it('renders a separate detail screen with links back to the associated order and DDT', async () => {
    await navigateToInvoice();
    const text = harness.routeNativeElement?.textContent ?? '';
    const header = harness.routeNativeElement?.querySelector('header');
    const links = Array.from(harness.routeNativeElement?.querySelectorAll('a') ?? []);
    const orderLink = links.find(link => link.href.includes('/ordini/order-1'));
    const ddtLink = links.find(link => link.href.includes('/ddt/ddt-1'));

    expect(text).toContain('INV-102026-001');
    expect(orderLink).toBeDefined();
    expect(ddtLink).toBeDefined();
    expect(orderLink?.textContent?.trim()).toBe('ORD');
    expect(ddtLink?.textContent?.trim()).toBe('DDT');
    expect(header?.contains(orderLink!)).toBeTrue();
    expect(header?.contains(ddtLink!)).toBeTrue();
    expect(new URL(orderLink!.href).searchParams.get('returnTo')).toBe(TestBed.inject(Router).url);
    expect(new URL(ddtLink!.href).searchParams.get('returnTo')).toBe(TestBed.inject(Router).url);
    expect(text).not.toContain('ORDINE COLLEGATO');
    expect(text).not.toContain('DDT COLLEGATI');
    const headerButtons = Array.from(header?.querySelectorAll('button') ?? []);
    expect(headerButtons.some(button => button.textContent?.trim().includes('PDF'))).toBeTrue();
    const backButton = harness.routeNativeElement?.querySelector('main app-back-button button') as HTMLButtonElement;
    expect(backButton.tagName).toBe('BUTTON');
    expect(backButton.textContent).toContain('Indietro');
  });

  it('returns from a linked DDT to the invoice that opened it', async () => {
    await navigateToInvoice();
    const ddtLink = harness.routeNativeElement?.querySelector('a[href*="/ddt/ddt-1"]') as HTMLAnchorElement;
    ddtLink.click();
    await harness.fixture.whenStable();
    harness.detectChanges();

    expect(harness.routeDebugElement?.componentInstance).toBeInstanceOf(DdtDettaglioComponent);
    const backButton = harness.routeNativeElement?.querySelector('button') as HTMLButtonElement;
    backButton.click();
    await harness.fixture.whenStable();
    harness.detectChanges();

    expect(TestBed.inject(Router).url).toContain('/fatturazione/fatture/invoice-1');
    expect(harness.routeDebugElement?.componentInstance).toBeInstanceOf(FatturaDettaglioComponent);
  });

  it('returns from a linked order to the invoice that opened it', async () => {
    await navigateToInvoice();
    const orderLink = harness.routeNativeElement?.querySelector('a[href*="/ordini/order-1"]') as HTMLAnchorElement;
    orderLink.click();
    await harness.fixture.whenStable();
    harness.detectChanges();

    expect(harness.routeDebugElement?.componentInstance).toBeInstanceOf(OrderDetailComponent);
    const backButton = harness.routeNativeElement?.querySelector('button') as HTMLButtonElement;
    backButton.click();
    await harness.fixture.whenStable();
    harness.detectChanges();

    expect(TestBed.inject(Router).url).toContain('/fatturazione/fatture/invoice-1');
    expect(harness.routeDebugElement?.componentInstance).toBeInstanceOf(FatturaDettaglioComponent);
  });

  it('records bank transfer payments with their CRO/TRN reference', async () => {
    const component = await navigateToInvoice();
    component.invoice = { ...invoice, status: 'Confirmed' };
    component.paymentAmount = 25.5;
    component.paymentDate = '2026-10-29';
    component.paymentMethod = 'Bonifico bancario';
    component.transactionDetails = 'CRO 123456';

    component.recordPayment();

    expect(invoiceService.recordPayment).toHaveBeenCalledWith(invoice.id, {
      amount: 25.5,
      paymentDate: '2026-10-29T00:00:00',
      paymentMethod: 'Bonifico bancario',
      transactionDetails: 'CRO 123456'
    });
    expect(component.invoice?.balanceDue).toBe(96.5);
  });

  it('flags the invoice while the related order is under pre-SDI adjustment and stays read-only', async () => {
    invoiceService.getById.and.returnValue(of({
      ...invoice,
      sdiStatus: 'NotTransmitted',
      isOrderUnderAdjustment: true
    }));
    await navigateToInvoice();
    const root = harness.routeNativeElement as HTMLElement;
    const header = root.querySelector('header') as HTMLElement;
    const badge = root.querySelector('[data-testid="invoice-adjustment-badge"]');

    expect(badge?.textContent?.trim()).toBe('Rettifica in corso dall\'ordine correlato');
    expect(header.contains(badge)).toBeTrue();
    expect(root.querySelectorAll('input[aria-label^="Quantità"]').length).toBe(0);
    expect(root.textContent).not.toContain('Salva modifiche righe');
  });

  it('hides the adjustment badge when the related order is not under adjustment', async () => {
    invoiceService.getById.and.returnValue(of({
      ...invoice,
      sdiStatus: 'NotTransmitted',
      isOrderUnderAdjustment: false
    }));
    await navigateToInvoice();

    expect(harness.routeNativeElement?.querySelector('[data-testid="invoice-adjustment-badge"]')).toBeNull();
    expect(harness.routeNativeElement?.textContent).not.toContain('Rettifica in corso');
  });

  it('hides the adjustment badge when the payload does not carry the adjustment flag', async () => {
    await navigateToInvoice();

    expect(harness.routeNativeElement?.textContent).not.toContain('Rettifica in corso');
  });

  it('returns to the originating invoice list when the back button is used', async () => {
    const component = await navigateToInvoice();
    const navigation = spyOn(TestBed.inject(Router), 'navigateByUrl')
      .and.returnValue(Promise.resolve(true));

    component.goBack();

    expect(navigation).toHaveBeenCalledWith('/fatturazione/fatture');
  });
});
