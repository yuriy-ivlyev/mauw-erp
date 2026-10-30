import { CommonModule } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { Component, OnDestroy, OnInit } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { Subject, of } from 'rxjs';
import { catchError, finalize, map, takeUntil } from 'rxjs/operators';
import { DdtDocumentRef, Order } from '../../../models/order.model';
import { Invoice } from '../../../models/invoice.model';
import { InvoiceService } from '../../../services/invoice.service';
import { OrderService } from '../../../services/order.service';
import { BackButtonComponent } from '../../../shared/back-button/back-button.component';
import { PaginationComponent } from '../../../shared/pagination/pagination.component';
import { PageSlicePipe } from '../../../shared/pagination/page-slice.pipe';

@Component({
  selector: 'app-fattura-dettaglio',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink, BackButtonComponent, PaginationComponent, PageSlicePipe],
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
  recordingPayment = false;
  downloadingPdf = false;

  readonly paymentMethods = ['Bonifico bancario', 'Contanti', 'Carta', 'Assegno', 'Altro'];

  private readonly destroy$ = new Subject<void>();
  private invoiceRequestVersion = 0;

  constructor(
    private readonly route: ActivatedRoute,
    private readonly router: Router,
    private readonly invoicesApi: InvoiceService,
    private readonly ordersApi: OrderService
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
  }

  get currentUrl(): string {
    return this.router.url;
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
        this.loadRelatedOrder(invoice);
      },
      error: error => {
        if (requestVersion === this.invoiceRequestVersion) {
          this.errorMessage = this.describeError(error, 'Impossibile caricare la fattura.');
        }
      }
    });
  }

  recordPayment(): void {
    const invoice = this.invoice;
    if (!invoice || this.recordingPayment || this.paymentAmount === null) {
      return;
    }
    if (!Number.isFinite(this.paymentAmount) || this.paymentAmount <= 0 ||
        !/^\d+(?:\.\d{1,2})?$/.test(this.paymentAmount.toString())) {
      this.errorMessage = 'Inserisci un importo positivo con al massimo due decimali.';
      return;
    }
    if (!this.paymentDate || !this.paymentMethod.trim()) {
      this.errorMessage = 'Seleziona data e metodo di pagamento.';
      return;
    }
    const details = this.transactionDetails.trim();
    if (this.paymentMethod === 'Bonifico bancario' && !details) {
      this.errorMessage = 'Inserisci il riferimento del bonifico (CRO/TRN).';
      return;
    }

    this.recordingPayment = true;
    this.errorMessage = '';
    this.invoicesApi.recordPayment(invoice.id, {
      amount: this.paymentAmount,
      paymentDate: `${this.paymentDate}T00:00:00`,
      paymentMethod: this.paymentMethod.trim(),
      transactionDetails: details || null
    }).pipe(
      takeUntil(this.destroy$),
      finalize(() => { this.recordingPayment = false; })
    ).subscribe({
      next: updated => {
        this.invoice = updated;
        this.paymentAmount = null;
        this.transactionDetails = '';
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

  private resetPaymentEntry(): void {
    this.paymentAmount = null;
    this.paymentDate = new Date().toISOString().slice(0, 10);
    this.paymentMethod = 'Bonifico bancario';
    this.transactionDetails = '';
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
