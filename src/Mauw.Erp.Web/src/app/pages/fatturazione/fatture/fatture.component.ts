import { CommonModule } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { Component, OnDestroy, OnInit } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { Subject } from 'rxjs';
import { debounceTime, distinctUntilChanged, finalize, takeUntil } from 'rxjs/operators';
import { Invoice, InvoiceListParams, InvoicePagedResult } from '../../../models/invoice.model';
import { SortChange } from '../../../models/pagination.model';
import { InvoiceService } from '../../../services/invoice.service';
import { PaginationComponent } from '../../../shared/pagination/pagination.component';
import { SortHeaderDirective } from '../../../shared/sort-header/sort-header.directive';

@Component({
  selector: 'app-fatture',
  standalone: true,
  imports: [CommonModule, FormsModule, PaginationComponent, SortHeaderDirective],
  templateUrl: './fatture.component.html'
})
export class FattureComponent implements OnInit, OnDestroy {
  invoices: Invoice[] = [];
  totalCount = 0;
  currentPage = 1;
  pageSize = 20;
  searchTerm = '';
  statusFilter = '';
  dateFrom = '';
  dateTo = '';
  unpaidOnly = false;
  sortBy = 'invoiceDate';
  sortDescending = true;
  loading = false;
  errorMessage = '';

  downloadingPdf: string | null = null;

  private readonly destroy$ = new Subject<void>();
  private readonly search$ = new Subject<string>();

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
    this.dateFrom = '';
    this.dateTo = '';
    this.unpaidOnly = false;
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

  openInvoice(invoiceId: string): void {
    void this.router.navigate(['/fatturazione/fatture', invoiceId]);
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
