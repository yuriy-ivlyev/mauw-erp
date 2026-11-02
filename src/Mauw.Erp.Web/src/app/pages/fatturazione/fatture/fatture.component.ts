import { CommonModule } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { Component, OnDestroy, OnInit } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { Subject } from 'rxjs';
import { debounceTime, distinctUntilChanged, finalize, takeUntil } from 'rxjs/operators';
import {
  Invoice,
  InvoiceListParams,
  InvoicePagedResult
} from '../../../models/invoice.model';
import {
  BulkFiscalTransmissionResult,
  CreditNoteSummary,
  FiscalDocumentTransmissionRequest
} from '../../../models/credit-note.model';
import { SortChange } from '../../../models/pagination.model';
import { InvoiceService } from '../../../services/invoice.service';
import { ConfirmDeleteModalComponent } from '../../../shared/confirm-delete-modal/confirm-delete-modal.component';
import { PaginationComponent } from '../../../shared/pagination/pagination.component';
import { SortHeaderDirective } from '../../../shared/sort-header/sort-header.directive';

@Component({
  selector: 'app-fatture',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    ConfirmDeleteModalComponent,
    PaginationComponent,
    SortHeaderDirective
  ],
  templateUrl: './fatture.component.html'
})
export class FattureComponent implements OnInit, OnDestroy {
  invoices: Invoice[] = [];
  totalCount = 0;
  currentPage = 1;
  pageSize = 20;
  searchTerm = '';
  statusFilter = '';
  sdiStatusFilter = '';
  dateFrom = '';
  dateTo = '';
  unpaidOnly = false;
  sortBy = 'invoiceDate';
  sortDescending = true;
  loading = false;
  errorMessage = '';

  downloadingPdf: string | null = null;
  selectedInvoiceIds = new Set<string>();
  selectedCreditNoteIds = new Set<string>();
  confirmBulkTransmitOpen = false;
  transmittingSdi = false;
  bulkResult: BulkFiscalTransmissionResult | null = null;

  private readonly destroy$ = new Subject<void>();
  private readonly search$ = new Subject<string>();

  get selectedInvoices(): string[] {
    return [...this.selectedInvoiceIds];
  }

  get selectableInvoices(): Invoice[] {
    return this.invoices.filter(invoice => this.canTransmitToSdi(invoice));
  }

  get selectableCreditNotes(): CreditNoteSummary[] {
    return this.invoices.flatMap(invoice => invoice.creditNotes ?? [])
      .filter(note => this.canTransmitCreditNote(note));
  }

  get selectableFiscalDocuments(): FiscalDocumentTransmissionRequest[] {
    return [
      ...this.selectableInvoices.map(invoice => ({ id: invoice.id, documentType: 'Invoice' as const })),
      ...this.selectableCreditNotes.map(note => ({ id: note.id, documentType: 'CreditNote' as const }))
    ];
  }

  get selectedDocuments(): FiscalDocumentTransmissionRequest[] {
    return [
      ...[...this.selectedInvoiceIds].map(id => ({ id, documentType: 'Invoice' as const })),
      ...[...this.selectedCreditNoteIds].map(id => ({ id, documentType: 'CreditNote' as const }))
    ];
  }

  get allSelectableInvoicesSelected(): boolean {
    return this.selectableFiscalDocuments.length > 0 &&
      this.selectableFiscalDocuments.every(document => this.isDocumentSelected(document));
  }

  get someSelectableInvoicesSelected(): boolean {
    return this.selectableFiscalDocuments.some(document => this.isDocumentSelected(document)) &&
      !this.allSelectableInvoicesSelected;
  }

  get transmittedInvoiceCount(): number {
    return this.bulkResult?.results.filter(item => item.success && item.documentType === 'Invoice').length ?? 0;
  }

  get transmittedCreditNoteCount(): number {
    return this.bulkResult?.results.filter(item => item.success && item.documentType === 'CreditNote').length ?? 0;
  }

  constructor(
    private readonly invoicesApi: InvoiceService,
    private readonly router: Router
  ) {}

  ngOnInit(): void {
    this.search$.pipe(
      debounceTime(350),
      distinctUntilChanged(),
      takeUntil(this.destroy$)
    ).subscribe(() => {
      this.currentPage = 1;
      this.loadInvoices();
    });

    this.loadInvoices();
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  onSearchChange(): void {
    this.selectedInvoiceIds.clear();
    this.selectedCreditNoteIds.clear();
    this.search$.next(this.searchTerm);
  }

  loadInvoices(): void {
    if (this.dateFrom && this.dateTo && this.dateFrom > this.dateTo) {
      this.loading = false;
      this.errorMessage = 'La data iniziale non può essere successiva alla data finale.';
      return;
    }

    this.loading = true;
    this.errorMessage = '';
    const filters: InvoiceListParams = {
      page: this.currentPage,
      pageSize: this.pageSize,
      search: this.searchTerm.trim() || undefined,
      status: this.statusFilter || undefined,
      sdiStatus: this.sdiStatusFilter || undefined,
      dateFrom: this.dateFrom || undefined,
      dateTo: this.dateTo || undefined,
      unpaidOnly: this.unpaidOnly,
      sortBy: this.sortBy,
      sortDescending: this.sortDescending
    };
    this.invoicesApi.getList(filters).pipe(
      takeUntil(this.destroy$),
      finalize(() => { this.loading = false; })
    ).subscribe({
      next: (result: InvoicePagedResult) => {
        this.invoices = result.items;
        for (const invoice of result.items) {
          if (!this.canTransmitToSdi(invoice)) {
            this.selectedInvoiceIds.delete(invoice.id);
          }
          for (const note of invoice.creditNotes ?? []) {
            if (!this.canTransmitCreditNote(note)) {
              this.selectedCreditNoteIds.delete(note.id);
            }
          }
        }
        this.totalCount = result.totalCount;
        if (result.totalPages > 0 && this.currentPage > result.totalPages) {
          this.currentPage = result.totalPages;
          this.loadInvoices();
        }
      },
      error: error => { this.errorMessage = this.describeError(error, 'Impossibile caricare le fatture.'); }
    });
  }

  goToPage(page: number): void {
    if (page < 1 || page > Math.max(1, Math.ceil(this.totalCount / this.pageSize))) {
      return;
    }
    this.currentPage = page;
    this.loadInvoices();
  }

  onPageSizeChange(pageSize: number): void {
    this.pageSize = pageSize;
    this.currentPage = 1;
    this.loadInvoices();
  }

  onFilterChange(): void {
    this.selectedInvoiceIds.clear();
    this.selectedCreditNoteIds.clear();
    this.currentPage = 1;
    this.loadInvoices();
  }

  onSortChange(sort: SortChange): void {
    this.sortBy = sort.sortBy;
    this.sortDescending = sort.sortDescending;
    this.currentPage = 1;
    this.loadInvoices();
  }

  clearFilters(): void {
    this.searchTerm = '';
    this.statusFilter = '';
    this.sdiStatusFilter = '';
    this.dateFrom = '';
    this.dateTo = '';
    this.unpaidOnly = false;
    this.selectedInvoiceIds.clear();
    this.selectedCreditNoteIds.clear();
    this.currentPage = 1;
    this.loadInvoices();
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

  sdiStatusLabel(status: string | undefined): string {
    const labels: Record<string, string> = {
      NotTransmitted: 'Non trasmessa',
      Pending: 'In coda',
      Transmitted: 'Trasmessa',
      Accepted: 'Accettata'
    };
    return status ? labels[status] ?? status : 'Non disponibile';
  }

  sdiStatusClass(status: string): string {
    const classes: Record<string, string> = {
      NotTransmitted: 'bg-slate-100 text-slate-700',
      Pending: 'bg-amber-100 text-amber-800',
      Transmitted: 'bg-blue-100 text-blue-800',
      Accepted: 'bg-green-100 text-green-800'
    };
    return classes[status] ?? 'bg-slate-100 text-slate-700';
  }

  canTransmitToSdi(invoice: Invoice): boolean {
    return invoice.canTransmitToSdi ??
      (invoice.sdiStatus === 'NotTransmitted' && invoice.isOrderUnderAdjustment !== true);
  }

  canTransmitCreditNote(note: CreditNoteSummary): boolean {
    return note.sdiStatus === 'NotTransmitted';
  }

  isInvoiceSelected(invoiceId: string): boolean {
    return this.selectedInvoiceIds.has(invoiceId);
  }

  isCreditNoteSelected(creditNoteId: string): boolean {
    return this.selectedCreditNoteIds.has(creditNoteId);
  }

  isDocumentSelected(document: FiscalDocumentTransmissionRequest): boolean {
    return document.documentType === 'Invoice'
      ? this.selectedInvoiceIds.has(document.id)
      : this.selectedCreditNoteIds.has(document.id);
  }

  setInvoiceSelected(invoice: Invoice, selected: boolean): void {
    if (!this.canTransmitToSdi(invoice)) {
      return;
    }

    if (selected) {
      this.selectedInvoiceIds.add(invoice.id);
    } else {
      this.selectedInvoiceIds.delete(invoice.id);
    }
  }

  setCreditNoteSelected(note: CreditNoteSummary, selected: boolean): void {
    if (!this.canTransmitCreditNote(note)) {
      return;
    }

    if (selected) {
      this.selectedCreditNoteIds.add(note.id);
    } else {
      this.selectedCreditNoteIds.delete(note.id);
    }
  }

  toggleAllSelectableInvoices(selected: boolean): void {
    for (const invoice of this.selectableInvoices) {
      this.setInvoiceSelected(invoice, selected);
    }
    for (const note of this.selectableCreditNotes) {
      this.setCreditNoteSelected(note, selected);
    }
  }

  openBulkTransmitConfirmation(): void {
    if (this.selectedDocuments.length > 0 && !this.transmittingSdi) {
      this.confirmBulkTransmitOpen = true;
    }
  }

  confirmBulkTransmission(): void {
    this.confirmBulkTransmitOpen = false;
    const documents = this.selectedDocuments;
    if (documents.length === 0 || this.transmittingSdi) {
      return;
    }

    this.errorMessage = '';
    this.transmittingSdi = true;
    this.invoicesApi.bulkTransmitFiscalDocuments(documents).pipe(
      takeUntil(this.destroy$),
      finalize(() => { this.transmittingSdi = false; })
    ).subscribe({
      next: result => {
        this.bulkResult = result;
        this.selectedInvoiceIds.clear();
        this.selectedCreditNoteIds.clear();
        this.loadInvoices();
      },
      error: error => {
        this.errorMessage = this.describeError(error, 'Impossibile completare l\'invio batch a SDI.');
      }
    });
  }

  closeBulkResult(): void {
    this.bulkResult = null;
  }

  openInvoice(invoiceId: string): void {
    void this.router.navigate(['/fatturazione/fatture', invoiceId]);
  }

  openCreditNote(noteId: string): void {
    void this.router.navigate(['/fatturazione/nota-credito', noteId]);
  }

  documentTypeLabel(documentType: string): string {
    return documentType === 'CreditNote' ? 'Nota di Credito TD04' : 'Fattura TD01';
  }

  downloadPdf(invoice: Invoice): void {
    if (this.downloadingPdf !== null) {
      return;
    }
    this.downloadingPdf = invoice.id;
    this.errorMessage = '';
    this.invoicesApi.downloadPdf(invoice.id).pipe(
      takeUntil(this.destroy$),
      finalize(() => { this.downloadingPdf = null; })
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
}
