import { CommonModule } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { Component, OnDestroy, OnInit } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { Observable, Subject, of } from 'rxjs';
import { catchError, finalize, map, switchMap, takeUntil } from 'rxjs/operators';
import {
  AvailableCreditNote,
  CreditNoteIssued,
  CreditNoteSummary
} from '../../../models/credit-note.model';
import { DdtDocumentRef, Order } from '../../../models/order.model';
import { Invoice } from '../../../models/invoice.model';
import { CreditNoteService } from '../../../services/credit-note.service';
import { InvoiceService } from '../../../services/invoice.service';
import { OrderService } from '../../../services/order.service';
import { WarehouseService } from '../../../services/warehouse.service';
import { WarehouseSummary } from '../../../models/warehouse.model';
import { BackButtonComponent } from '../../../shared/back-button/back-button.component';
import { PaginationComponent } from '../../../shared/pagination/pagination.component';
import { PageSlicePipe } from '../../../shared/pagination/page-slice.pipe';
import { CreditNoteModalComponent } from './credit-note-modal.component';

@Component({
  selector: 'app-fattura-dettaglio',
  standalone: true,
  imports: [
    CommonModule, FormsModule, RouterLink, BackButtonComponent, PaginationComponent,
    PageSlicePipe, CreditNoteModalComponent
  ],
  templateUrl: './fattura-dettaglio.component.html'
})
export class FatturaDettaglioComponent implements OnInit, OnDestroy {
  invoice: Invoice | null = null;
  relatedOrder: Order | null = null;
  relatedDdts: DdtDocumentRef[] = [];
  loading = true;
  loadingRelatedDocuments = false;
  errorMessage = '';
  relatedDocumentsError = '';
  successMessage = '';
  returnUrl = '/fatturazione/fatture';
  itemsPage = 1;
  itemsPageSize = 20;

  paymentAmount: number | null = null;
  paymentDate = new Date().toISOString().slice(0, 10);
  paymentMethod = 'Bonifico bancario';
  transactionDetails = '';
  availableCreditNotes: AvailableCreditNote[] = [];
  selectedCreditNote: AvailableCreditNote | null = null;
  creditNoteOffsetAmount: number | null = null;
  loadingAvailableCreditNotes = false;
  availableCreditNotesError = '';
  recordingPayment = false;
  downloadingPdf = false;
  creditNoteModalOpen = false;
  loadingCreditNoteWarehouses = false;
  activeWarehouses: WarehouseSummary[] = [];
  creditNoteToast = '';
  creditNoteToastIsWarning = false;

  readonly paymentMethods = ['Bonifico bancario', 'Contanti', 'Carta', 'POS', 'Assegno', 'Altro'];

  private readonly destroy$ = new Subject<void>();
  private invoiceRequestVersion = 0;
  private creditNoteRequestVersion = 0;
  private creditNoteToastTimer: ReturnType<typeof setTimeout> | null = null;

  constructor(
    private readonly route: ActivatedRoute,
    private readonly router: Router,
    private readonly creditNotesApi: CreditNoteService,
    private readonly invoicesApi: InvoiceService,
    private readonly ordersApi: OrderService,
    private readonly warehousesApi: WarehouseService
  ) {}

  ngOnInit(): void {
    this.route.queryParamMap.pipe(
      map(params => params.get('returnTo')),
      takeUntil(this.destroy$)
    ).subscribe(value => {
      this.returnUrl = this.safeReturnUrl(value) ?? '/fatturazione/fatture';
    });

    this.route.paramMap.pipe(takeUntil(this.destroy$)).subscribe(params => {
      const id = params.get('id');
      if (id) {
        this.loadInvoice(id);
      } else {
        this.loading = false;
        this.errorMessage = 'Identificativo fattura non valido.';
      }
    });
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
    if (this.creditNoteToastTimer) {
      clearTimeout(this.creditNoteToastTimer);
    }
  }

  get canIssueCreditNote(): boolean {
    return this.invoice?.sdiStatus === 'Transmitted' || this.invoice?.sdiStatus === 'Accepted';
  }

  get currentUrl(): string {
    return this.router.url;
  }

  get availableCreditTotal(): number {
    const cents = this.availableCreditNotes.reduce(
      (total, note) => total + Math.round(note.remainingCredit * 100),
      0);
    return cents / 100;
  }

  get canSubmitPayment(): boolean {
    if (!this.invoice || this.recordingPayment) {
      return false;
    }

    const hasCreditOffset = this.selectedCreditNote !== null &&
      this.creditNoteOffsetAmount !== null &&
      this.creditNoteOffsetAmount > 0;
    const cashAmount = this.paymentAmount ?? 0;
    const hasCashPayment = Number.isFinite(cashAmount) && cashAmount > 0 &&
      /^\d+(?:\.\d{1,2})?$/.test(cashAmount.toString());

    return (hasCreditOffset && (cashAmount === 0 || hasCashPayment)) ||
      (!hasCreditOffset && hasCashPayment);
  }

  goBack(): void {
    void this.router.navigateByUrl(this.returnUrl);
  }

  onItemsPageSizeChange(pageSize: number): void {
    this.itemsPageSize = pageSize;
    this.itemsPage = 1;
  }

  loadInvoice(id: string): void {
    const requestVersion = ++this.invoiceRequestVersion;
    this.loading = true;
    this.errorMessage = '';
    this.invoice = null;
    this.creditNoteRequestVersion++;
    this.availableCreditNotes = [];
    this.availableCreditNotesError = '';
    this.loadingAvailableCreditNotes = false;
    this.itemsPage = 1;
    this.relatedOrder = null;
    this.relatedDdts = [];
    this.invoicesApi.getById(id).pipe(
      takeUntil(this.destroy$),
      finalize(() => {
        if (requestVersion === this.invoiceRequestVersion) {
          this.loading = false;
        }
      })
    ).subscribe({
      next: invoice => {
        if (requestVersion !== this.invoiceRequestVersion) {
          return;
        }
        this.invoice = invoice;
        this.resetPaymentEntry();
        this.loadAvailableCreditNotes(invoice.contactId, requestVersion);
        this.loadRelatedOrder(invoice);
      },
      error: error => {
        if (requestVersion === this.invoiceRequestVersion) {
          this.errorMessage = this.describeError(error, 'Impossibile caricare la fattura.');
        }
      }
    });
  }

  applyCreditNote(note: AvailableCreditNote): void {
    const invoice = this.invoice;
    if (!invoice || invoice.balanceDue <= 0 || note.remainingCredit <= 0) {
      return;
    }

    const outstandingCents = Math.max(0, Math.round(invoice.balanceDue * 100));
    const availableCents = Math.max(0, Math.round(note.remainingCredit * 100));
    const appliedCents = Math.min(outstandingCents, availableCents);
    this.selectedCreditNote = note;
    this.creditNoteOffsetAmount = appliedCents / 100;
    this.paymentAmount = (outstandingCents - appliedCents) / 100;
    this.paymentMethod = 'POS';
    this.transactionDetails = '';
    this.errorMessage = '';
  }

  clearCreditNoteOffset(): void {
    this.selectedCreditNote = null;
    this.creditNoteOffsetAmount = null;
    this.paymentAmount = this.invoice?.balanceDue ?? null;
  }

  recordPayment(): void {
    const invoice = this.invoice;
    if (!invoice || !this.canSubmitPayment) {
      return;
    }
    const cashAmount = this.paymentAmount ?? 0;
    const hasCreditOffset = this.selectedCreditNote !== null &&
      this.creditNoteOffsetAmount !== null &&
      this.creditNoteOffsetAmount > 0;
    if (cashAmount < 0 || cashAmount > 0 &&
        (!Number.isFinite(cashAmount) || !/^\d+(?:\.\d{1,2})?$/.test(cashAmount.toString()))) {
      this.errorMessage = 'Inserisci un importo positivo con al massimo due decimali.';
      return;
    }
    if (!this.paymentDate || cashAmount > 0 && !this.paymentMethod.trim()) {
      this.errorMessage = 'Seleziona data e metodo di pagamento.';
      return;
    }
    const details = this.transactionDetails.trim();
    if (cashAmount > 0 && this.paymentMethod === 'Bonifico bancario' && !details) {
      this.errorMessage = 'Inserisci il riferimento del bonifico (CRO/TRN).';
      return;
    }

    const payments = [];
    if (hasCreditOffset && this.selectedCreditNote) {
      payments.push({
        amount: this.creditNoteOffsetAmount!,
        paymentDate: `${this.paymentDate}T00:00:00`,
        paymentMethod: 'CreditNoteOffset',
        transactionDetails: null,
        sourceCreditNoteId: this.selectedCreditNote.id
      });
    }
    if (cashAmount > 0) {
      payments.push({
        amount: cashAmount,
        paymentDate: `${this.paymentDate}T00:00:00`,
        paymentMethod: this.paymentMethod.trim(),
        transactionDetails: details || null
      });
    }

    this.recordingPayment = true;
    this.errorMessage = '';
    this.invoicesApi.recordPayments(invoice.id, { payments }).pipe(
      takeUntil(this.destroy$),
      finalize(() => { this.recordingPayment = false; })
    ).subscribe({
      next: updated => {
        this.invoice = updated;
        this.resetPaymentEntry();
        this.loadAvailableCreditNotes(updated.contactId, this.invoiceRequestVersion);
        this.successMessage = 'Pagamento registrato.';
      },
      error: error => { this.errorMessage = this.describeError(error, 'Impossibile registrare il pagamento.'); }
    });
  }

  downloadPdf(): void {
    const invoice = this.invoice;
    if (!invoice || this.downloadingPdf) {
      return;
    }
    this.downloadingPdf = true;
    this.errorMessage = '';
    this.invoicesApi.downloadPdf(invoice.id).pipe(
      takeUntil(this.destroy$),
      finalize(() => { this.downloadingPdf = false; })
    ).subscribe({
      next: blob => {
        const url = URL.createObjectURL(blob);
        const link = document.createElement('a');
        link.href = url;
        link.download = `fattura-${invoice.invoiceNumber}.pdf`;
        link.click();
        URL.revokeObjectURL(url);
      },
      error: error => {
        if (error instanceof HttpErrorResponse && error.error instanceof Blob) {
          void error.error.text().then(
            body => { this.errorMessage = body.trim() || 'Impossibile scaricare il PDF.'; },
            () => { this.errorMessage = 'Impossibile scaricare il PDF.'; }
          );
          return;
        }
        this.errorMessage = this.describeError(error, 'Impossibile scaricare il PDF.');
      }
    });
  }

  openCreditNoteModal(): void {
    if (!this.invoice || !this.canIssueCreditNote || this.loadingCreditNoteWarehouses) {
      return;
    }

    this.loadingCreditNoteWarehouses = true;
    this.errorMessage = '';
    this.loadActiveWarehouses().pipe(
      takeUntil(this.destroy$),
      finalize(() => { this.loadingCreditNoteWarehouses = false; })
    ).subscribe({
      next: warehouses => {
        this.activeWarehouses = warehouses;
        this.creditNoteModalOpen = true;
      },
      error: error => {
        this.errorMessage = this.describeError(error, 'Impossibile caricare i magazzini attivi.');
      }
    });
  }

  onCreditNoteCompleted(result: CreditNoteIssued): void {
    this.creditNoteModalOpen = false;
    this.showCreditNoteToast(
      result.pdfGenerationError
        ? `Nota ${result.creditNote.creditNoteNumber} emessa; PDF non generato: ${result.pdfGenerationError}`
        : `Nota di credito ${result.creditNote.creditNoteNumber} emessa.`,
      !!result.pdfGenerationError);
    const invoiceId = this.invoice?.id;
    if (invoiceId) {
      this.loadInvoice(invoiceId);
    }
  }

  downloadCreditNotePdf(note: CreditNoteSummary): void {
    this.invoicesApi.downloadPdf(note.id).pipe(
      takeUntil(this.destroy$)
    ).subscribe({
      next: blob => {
        const url = URL.createObjectURL(blob);
        const link = document.createElement('a');
        link.href = url;
        link.download = `nota-di-credito-${note.creditNoteNumber}.pdf`;
        link.click();
        URL.revokeObjectURL(url);
      },
      error: error => {
        this.errorMessage = this.describeError(error, 'Impossibile scaricare il PDF della nota di credito.');
      }
    });
  }

  creditNoteReasonLabel(reason: string | null): string {
    switch (reason) {
      case 'GoodsReturn': return 'Reso merce';
      case 'PriceAdjustment': return 'Rettifica prezzo';
      case 'CommercialAllowance': return 'Abbuono commerciale';
      case 'FullCancellation': return 'Storno totale';
      default: return reason ?? 'Causale non specificata';
    }
  }

  private loadActiveWarehouses(page = 1, collected: WarehouseSummary[] = []): Observable<WarehouseSummary[]> {
    return this.warehousesApi.getList({ page, pageSize: 100, isActive: true }).pipe(
      switchMap(result => {
        const warehouses = [...collected, ...result.items];
        return page < result.totalPages
          ? this.loadActiveWarehouses(page + 1, warehouses)
          : of(warehouses);
      })
    );
  }

  private showCreditNoteToast(message: string, warning: boolean): void {
    this.creditNoteToast = message;
    this.creditNoteToastIsWarning = warning;
    if (this.creditNoteToastTimer) {
      clearTimeout(this.creditNoteToastTimer);
    }
    this.creditNoteToastTimer = setTimeout(() => {
      this.creditNoteToast = '';
      this.creditNoteToastTimer = null;
    }, 5000);
  }

  private loadRelatedOrder(invoice: Invoice): void {
    this.relatedDocumentsError = '';
    if (!invoice.orderId) {
      return;
    }
    this.loadingRelatedDocuments = true;
    this.ordersApi.getById(invoice.orderId).pipe(
      catchError(error => {
        this.relatedDocumentsError = this.describeError(error, 'Impossibile caricare i documenti collegati.');
        return of(null);
      }),
      finalize(() => { this.loadingRelatedDocuments = false; }),
      takeUntil(this.destroy$)
    ).subscribe(order => {
      this.relatedOrder = order;
      const documents = order?.linkedDocuments;
      this.relatedDdts = documents?.ddts?.length
        ? documents.ddts
        : documents?.ddtId && documents.ddtNumber
          ? [{ id: documents.ddtId, ddtNumber: documents.ddtNumber, ddtDate: '' }]
          : [];
    });
  }

  private loadAvailableCreditNotes(contactId: string, requestVersion: number): void {
    const creditNoteRequestVersion = ++this.creditNoteRequestVersion;
    this.loadingAvailableCreditNotes = true;
    this.availableCreditNotes = [];
    this.availableCreditNotesError = '';
    this.creditNotesApi.getAvailableForContact(contactId).pipe(
      takeUntil(this.destroy$),
      finalize(() => {
        if (requestVersion === this.invoiceRequestVersion &&
            creditNoteRequestVersion === this.creditNoteRequestVersion) {
          this.loadingAvailableCreditNotes = false;
        }
      })
    ).subscribe({
      next: notes => {
        if (requestVersion === this.invoiceRequestVersion &&
            creditNoteRequestVersion === this.creditNoteRequestVersion) {
          this.availableCreditNotes = notes;
        }
      },
      error: error => {
        if (requestVersion === this.invoiceRequestVersion &&
            creditNoteRequestVersion === this.creditNoteRequestVersion) {
          this.availableCreditNotesError = this.describeError(
            error, 'Impossibile caricare i crediti disponibili dalle note di credito.');
        }
      }
    });
  }

  private resetPaymentEntry(): void {
    this.paymentAmount = null;
    this.paymentDate = new Date().toISOString().slice(0, 10);
    this.paymentMethod = 'Bonifico bancario';
    this.transactionDetails = '';
    this.selectedCreditNote = null;
    this.creditNoteOffsetAmount = null;
  }

  private safeReturnUrl(value: string | null): string | null {
    if (!value || !value.startsWith('/') || value.startsWith('//') || value.includes('\\')) {
      return null;
    }
    return value;
  }

  private describeError(error: unknown, fallback: string): string {
    if (error instanceof HttpErrorResponse) {
      if (typeof error.error === 'string' && error.error.trim()) {
        return error.error.trim();
      }
      if (error.error && typeof error.error === 'object' && typeof error.error.detail === 'string') {
        return error.error.detail;
      }
    }
    return fallback;
  }

  statusLabel(status: string): string {
    const labels: Record<string, string> = {
      Confirmed: 'Confermata',
      PartiallyPaid: 'Parzialmente pagata',
      Paid: 'Pagata'
    };
    return labels[status] ?? status;
  }

  statusClass(status: string): string {
    const classes: Record<string, string> = {
      Confirmed: 'bg-blue-100 text-blue-800',
      PartiallyPaid: 'bg-orange-100 text-orange-800',
      Paid: 'bg-green-100 text-green-800'
    };
    return classes[status] ?? 'bg-slate-100 text-slate-700';
  }
}
