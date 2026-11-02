import '../../../locale';
import { fakeAsync, TestBed, tick } from '@angular/core/testing';
import { of } from 'rxjs';
import { CreditNotePreview } from '../../../models/credit-note.model';
import { Invoice } from '../../../models/invoice.model';
import { CreditNoteService } from '../../../services/credit-note.service';
import { CreditNoteModalComponent } from './credit-note-modal.component';

describe('CreditNoteModalComponent', () => {
  let service: jasmine.SpyObj<CreditNoteService>;
  let invoice: Invoice;
  let preview: CreditNotePreview;

  beforeEach(async () => {
    invoice = {
      id: 'invoice-1',
      invoiceNumber: 'FAT-202610-00001',
      contactId: 'contact-1',
      orderId: null,
      contactName: 'Cliente',
      invoiceDate: '2026-10-30T00:00:00Z',
      taxableTotal: 20,
      vatTotal: 4.4,
      grossTotal: 24.4,
      shippingCost: 0,
      shippingVatRate: null,
      shippingNatureCode: null,
      sdiSubmittedAt: '2026-10-30T10:00:00Z',
      sdiStatus: 'Transmitted',
      paidAmount: 0,
      balanceDue: 24.4,
      status: 'Confirmed',
      items: [],
      payments: []
    };
    preview = {
      invoiceId: invoice.id,
      taxableTotal: 20,
      vatTotal: 4.4,
      grossTotal: 24.4,
      vatSummary: [{ vatRate: 22, natureCode: null, taxableAmount: 20, vatAmount: 4.4 }],
      availableLines: [{
        invoiceItemId: 'invoice-item-1',
        isShippingCharge: false,
        description: 'Articolo prova',
        sku: 'SKU-1',
        originalQuantity: 2,
        creditedQuantity: 0,
        remainingQuantity: 2,
        unitNetPrice: 10,
        discountPercent: 0,
        vatRate: 22,
        natureCode: null,
        lineNetTotal: 20
      }],
      selectedLines: []
    };
    service = jasmine.createSpyObj<CreditNoteService>(
      'CreditNoteService', ['previewCreditNote', 'createCreditNote']);
    service.previewCreditNote.and.returnValue(of(preview));

    await TestBed.configureTestingModule({
      imports: [CreditNoteModalComponent],
      providers: [{ provide: CreditNoteService, useValue: service }]
    }).compileComponents();
  });

  it('selects every remaining row and uses the full-cancellation reason in total mode', fakeAsync(() => {
    const fixture = TestBed.createComponent(CreditNoteModalComponent);
    fixture.componentInstance.invoice = invoice;
    fixture.detectChanges();
    tick(200);
    fixture.detectChanges();

    fixture.componentInstance.setMode('full');
    tick(200);
    fixture.detectChanges();

    const line = fixture.componentInstance.lines[0];
    expect(line.selected).toBeTrue();
    expect(line.quantity).toBe(2);
    expect(fixture.componentInstance.reason).toBe('FullCancellation');
    expect(fixture.componentInstance.canIssue).toBeTrue();
    expect(service.previewCreditNote.calls.mostRecent().args[0].lines).toEqual([{
      invoiceItemId: 'invoice-item-1',
      isShippingCharge: false,
      quantity: 2
    }]);
  }));

  it('sends selected partial quantities to the backend preview and issuance endpoints', fakeAsync(() => {
    service.createCreditNote.and.returnValue(of({
      creditNote: {
        id: 'credit-note-1',
        creditNoteNumber: 'NC-202610-00001',
        creditNoteDate: '2026-10-30T12:00:00Z',
        taxableTotal: 10,
        vatTotal: 2.2,
        grossTotal: 12.2,
        reason: 'GoodsReturn',
        sdiStatus: 'NotTransmitted'
      },
      courtesyPdf: null,
      pdfGenerationError: null
    }));
    const fixture = TestBed.createComponent(CreditNoteModalComponent);
    fixture.componentInstance.invoice = invoice;
    fixture.detectChanges();
    tick(200);
    fixture.detectChanges();

    const line = fixture.componentInstance.lines[0];
    fixture.componentInstance.onLineSelectionChange(line, true);
    fixture.componentInstance.onQuantityChange(line, 1);
    tick(200);
    fixture.detectChanges();

    expect(service.previewCreditNote.calls.mostRecent().args[0].lines[0].quantity).toBe(1);
    expect(fixture.componentInstance.canIssue).toBeTrue();
    fixture.componentInstance.submit();
    expect(service.createCreditNote.calls.mostRecent().args[0].lines[0].quantity).toBe(1);
  }));
});
