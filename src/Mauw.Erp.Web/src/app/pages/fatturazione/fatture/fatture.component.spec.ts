import '../../../locale';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { of } from 'rxjs';
import { Invoice } from '../../../models/invoice.model';
import { InvoiceService } from '../../../services/invoice.service';
import { FattureComponent } from './fatture.component';

describe('FattureComponent', () => {
  let fixture: ComponentFixture<FattureComponent>;
  let invoiceService: jasmine.SpyObj<InvoiceService>;
  let invoice: Invoice;

  beforeEach(async () => {
    invoice = {
      id: 'invoice-1',
      invoiceNumber: 'INV-102026-001',
      contactId: 'contact-1',
      orderId: null,
      contactName: 'Cliente',
      invoiceDate: '2026-10-29T00:00:00Z',
      taxableTotal: 100,
      vatTotal: 22,
      grossTotal: 122,
      shippingCost: 0,
      shippingVatRate: null,
      shippingNatureCode: null,
      sdiSubmittedAt: null,
      sdiStatus: 'NotTransmitted',
      isOrderUnderAdjustment: false,
      canTransmitToSdi: true,
      paidAmount: 0,
      balanceDue: 122,
      status: 'Confirmed',
      items: [],
      payments: []
    };
    invoiceService = jasmine.createSpyObj<InvoiceService>('InvoiceService', [
      'getList', 'generateFromOrder', 'downloadPdf', 'bulkTransmitToSdi',
      'bulkTransmitFiscalDocuments'
    ]);
    invoiceService.getList.and.returnValue(of({
      items: [invoice], pageNumber: 1, pageSize: 20, totalCount: 1, totalPages: 1
    }));
    await TestBed.configureTestingModule({
      imports: [FattureComponent],
      providers: [
        provideRouter([{ path: 'fatturazione/fatture/:id', component: FattureComponent }]),
        { provide: InvoiceService, useValue: invoiceService }
      ]
    }).compileComponents();
    fixture = TestBed.createComponent(FattureComponent);
  });

  it('renders the default newest-first list and uses only the detail button for navigation', () => {
    fixture.detectChanges();

    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';
    const firstCell = fixture.nativeElement.querySelector('tbody tr td:first-child') as HTMLElement;
    const actionButtons = Array.from(fixture.nativeElement.querySelectorAll('tbody td:last-child button')) as HTMLButtonElement[];
    const detailButton = actionButtons[0];
    expect(text).toContain('INV-102026-001');
    expect(text).toContain('Cliente');
    expect(text).toContain('122,00');
    expect(firstCell.querySelector('a')).toBeNull();
    expect(detailButton.textContent).toContain('Dettaglio');
    expect(actionButtons.map(button => button.textContent?.trim())).toEqual(['Dettaglio', 'PDF']);
    expect(actionButtons.every(button => button.classList.contains('h-7'))).toBeTrue();
    expect(invoiceService.getList).toHaveBeenCalledWith({
      page: 1,
      pageSize: 20,
      search: undefined,
      status: undefined,
      sdiStatus: undefined,
      dateFrom: undefined,
      dateTo: undefined,
      unpaidOnly: false,
      sortBy: 'invoiceDate',
      sortDescending: true
    });
  });

  it('passes status, date range and unpaid filter to the server', () => {
    fixture.detectChanges();
    const component = fixture.componentInstance;
    component.statusFilter = 'Confirmed';
    component.dateFrom = '2026-10-01';
    component.dateTo = '2026-10-31';
    component.unpaidOnly = true;

    component.onFilterChange();

    expect(invoiceService.getList).toHaveBeenCalledWith(jasmine.objectContaining({
      status: 'Confirmed',
      sdiStatus: undefined,
      dateFrom: '2026-10-01',
      dateTo: '2026-10-31',
      unpaidOnly: true,
      sortBy: 'invoiceDate',
      sortDescending: true
    }));
  });

  it('passes the SDI status quick filter to the server', () => {
    fixture.detectChanges();
    const component = fixture.componentInstance;
    component.sdiStatusFilter = 'NotTransmitted';

    component.onFilterChange();

    expect(invoiceService.getList).toHaveBeenCalledWith(jasmine.objectContaining({
      sdiStatus: 'NotTransmitted'
    }));
  });

  it('selects only invoices which the server marks as transmittable', () => {
    fixture.detectChanges();
    const component = fixture.componentInstance;
    const underAdjustment: Invoice = {
      ...invoice,
      id: 'invoice-adjustment',
      isOrderUnderAdjustment: true,
      canTransmitToSdi: false
    };
    component.invoices = [invoice, underAdjustment];

    component.toggleAllSelectableInvoices(true);

    expect(component.selectedInvoices).toEqual([invoice.id]);
    expect(component.selectableInvoices).toEqual([invoice]);
  });

  it('requires confirmation, submits fiscal document types and shows the batch result dialog', () => {
    fixture.detectChanges();
    const component = fixture.componentInstance;
    component.selectedInvoiceIds.add(invoice.id);
    invoiceService.bulkTransmitFiscalDocuments.and.returnValue(of({
      processedCount: 1,
      successCount: 1,
      failedCount: 0,
      results: [{
        id: invoice.id,
        documentType: 'Invoice',
        success: true,
        documentNumber: invoice.invoiceNumber,
        errorMessage: null
      }]
    }));

    component.openBulkTransmitConfirmation();
    fixture.detectChanges();

    expect(invoiceService.bulkTransmitFiscalDocuments).not.toHaveBeenCalled();
    const buttons = Array.from(
      fixture.nativeElement.querySelectorAll('app-confirm-delete-modal button') as NodeListOf<HTMLButtonElement>
    );
    const confirmButton = buttons.find(button => button.textContent?.includes('Invia a SDI'))!;
    confirmButton.click();
    fixture.detectChanges();

    expect(invoiceService.bulkTransmitFiscalDocuments).toHaveBeenCalledWith([
      { id: invoice.id, documentType: 'Invoice' }
    ]);
    expect(component.selectedInvoices).toEqual([]);
    expect(fixture.nativeElement.textContent).toContain('1 fatture e 0 note di credito trasmesse');
    expect(fixture.nativeElement.querySelector('[role="dialog"]')).not.toBeNull();
  });

  it('selects and batches a credit note alongside its source invoice with distinct type badges', () => {
    fixture.detectChanges();
    const component = fixture.componentInstance;
    const note = {
      id: 'credit-note-1',
      creditNoteNumber: 'NC-202610-00001',
      creditNoteDate: '2026-10-30T00:00:00Z',
      taxableTotal: 10,
      vatTotal: 2.2,
      grossTotal: 12.2,
      reason: 'GoodsReturn',
      sdiStatus: 'NotTransmitted'
    };
    component.invoices = [{ ...invoice, creditNotes: [note] }];
    invoiceService.getList.and.returnValue(of({
      items: [{ ...invoice, creditNotes: [note] }],
      pageNumber: 1,
      pageSize: 20,
      totalCount: 1,
      totalPages: 1
    }));
    invoiceService.bulkTransmitFiscalDocuments.and.returnValue(of({
      processedCount: 2,
      successCount: 2,
      failedCount: 0,
      results: [
        {
          id: invoice.id,
          documentType: 'Invoice',
          success: true,
          documentNumber: invoice.invoiceNumber,
          errorMessage: null
        },
        {
          id: note.id,
          documentType: 'CreditNote',
          success: true,
          documentNumber: note.creditNoteNumber,
          errorMessage: null
        }
      ]
    }));

    component.toggleAllSelectableInvoices(true);
    fixture.detectChanges();
    expect(component.selectedDocuments).toEqual([
      { id: invoice.id, documentType: 'Invoice' },
      { id: note.id, documentType: 'CreditNote' }
    ]);
    expect(fixture.nativeElement.textContent).toContain('1 fatture e 1 note di credito selezionate');
    component.confirmBulkTransmission();

    expect(invoiceService.bulkTransmitFiscalDocuments).toHaveBeenCalledWith([
      { id: invoice.id, documentType: 'Invoice' },
      { id: note.id, documentType: 'CreditNote' }
    ]);
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('Fattura TD01');
    expect(fixture.nativeElement.textContent).toContain('Nota di Credito TD04');
    expect(fixture.nativeElement.textContent).toContain(note.creditNoteNumber);
  });

  it('does not request an invalid date range and displays a validation error', () => {
    fixture.detectChanges();
    const component = fixture.componentInstance;
    component.dateFrom = '2026-10-31';
    component.dateTo = '2026-10-01';
    const callsBefore = invoiceService.getList.calls.count();

    component.onFilterChange();
    fixture.detectChanges();

    expect(invoiceService.getList.calls.count()).toBe(callsBefore);
    expect(component.errorMessage).toContain('data iniziale');
  });

  it('updates server sorting when a column header is selected', () => {
    fixture.detectChanges();
    fixture.componentInstance.onSortChange({ sortBy: 'grossTotal', sortDescending: false });

    expect(invoiceService.getList).toHaveBeenCalledWith(jasmine.objectContaining({
      sortBy: 'grossTotal',
      sortDescending: false
    }));
  });

});
