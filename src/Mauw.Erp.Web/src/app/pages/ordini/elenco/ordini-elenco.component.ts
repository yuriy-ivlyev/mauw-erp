import { Component, OnDestroy, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Observable, Subject } from 'rxjs';
import { debounceTime, distinctUntilChanged, finalize, takeUntil } from 'rxjs/operators';
import { Order, OrderFilterParams, PagedResult } from '../../../models/order.model';
import { OrderService } from '../../../services/order.service';

@Component({
  selector: 'app-ordini-elenco',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './ordini-elenco.component.html'
})
export class OrdiniElencoComponent implements OnInit, OnDestroy {
  orders: Order[] = [];
  totalCount = 0;
  totalPages = 0;
  loading = false;
  errorMessage = '';

  searchTerm = '';
  statusFilter = '';
  typeFilter = '';
  currentPage = 1;
  pageSize = 20;

  downloadingPdf: string | null = null;
  pdfErrorMessage = '';

  private readonly searchSubject = new Subject<string>();
  private readonly destroy$ = new Subject<void>();

  constructor(private readonly orderService: OrderService) {}

  ngOnInit(): void {
    this.searchSubject.pipe(
      debounceTime(400),
      distinctUntilChanged(),
      takeUntil(this.destroy$)
    ).subscribe(() => {
      this.currentPage = 1;
      this.loadOrders();
    });

    this.loadOrders();
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  onSearchChange(): void {
    this.searchSubject.next(this.searchTerm);
  }

  onFilterChange(): void {
    this.currentPage = 1;
    this.loadOrders();
  }

  loadOrders(): void {
    this.loading = true;
    this.errorMessage = '';

    const params: OrderFilterParams = {
      search: this.searchTerm || undefined,
      status: this.statusFilter || undefined,
      orderType: this.typeFilter || undefined,
      page: this.currentPage,
      pageSize: this.pageSize
    };

    this.orderService.getList(params).pipe(takeUntil(this.destroy$)).subscribe({
      next: (result: PagedResult<Order>) => {
        this.orders = result.items;
        this.totalCount = result.totalCount;
        this.totalPages = result.totalPages;
        this.loading = false;
      },
      error: () => {
        this.errorMessage = 'Errore nel caricamento degli ordini.';
        this.loading = false;
      }
    });
  }

  goToPage(page: number): void {
    if (page < 1 || page > this.totalPages) {
      return;
    }

    this.currentPage = page;
    this.loadOrders();
  }

  pages(): number[] {
    const delta = 2;
    const start = Math.max(1, this.currentPage - delta);
    const end = Math.min(this.totalPages, this.currentPage + delta);
    return Array.from({ length: end - start + 1 }, (_, i) => start + i);
  }

  downloadPdf(type: 'order' | 'invoice' | 'ddt', id: string, filename: string): void {
    if (this.downloadingPdf !== null) {
      return;
    }

    const key = `${type}-${id}`;
    this.downloadingPdf = key;
    this.pdfErrorMessage = '';

    let obs$: Observable<Blob>;
    if (type === 'order') {
      obs$ = this.orderService.downloadOrderPdf(id);
    } else if (type === 'invoice') {
      obs$ = this.orderService.downloadInvoicePdf(id);
    } else {
      obs$ = this.orderService.downloadDdtPdf(id);
    }

    obs$.pipe(
      takeUntil(this.destroy$),
      finalize(() => { this.downloadingPdf = null; })
    ).subscribe({
      next: (blob: Blob) => {
        const url = URL.createObjectURL(blob);
        const link = document.createElement('a');
        link.href = url;
        link.download = filename;
        link.click();
        URL.revokeObjectURL(url);
      },
      error: () => {
        this.pdfErrorMessage = 'Impossibile scaricare il PDF. Riprova tra poco.';
      }
    });
  }

  statusLabel(status: string): string {
    const map: Record<string, string> = {
      Draft: 'Bozza',
      Confirmed: 'Confermato',
      Suspended: 'Sospeso',
      Terminated: 'Terminato'
    };

    return map[status] ?? status;
  }

  statusClass(status: string): string {
    const map: Record<string, string> = {
      Draft: 'bg-gray-100 text-gray-600',
      Confirmed: 'bg-green-100 text-green-700',
      Suspended: 'bg-yellow-100 text-yellow-700',
      Terminated: 'bg-blue-100 text-blue-700'
    };

    return map[status] ?? 'bg-gray-100 text-gray-600';
  }

  orderTypeLabel(type: string): string {
    const map: Record<string, string> = {
      Standard: 'Ordine', CassaSession: 'Cassa', Preventivo: 'Preventivo', WebOrder: 'Web'
    };
    return map[type] ?? type;
  }

  orderTypeClass(type: string): string {
    const map: Record<string, string> = {
      Standard:     'bg-gray-100 text-gray-700',
      CassaSession: 'bg-purple-100 text-purple-700',
      Preventivo:   'bg-amber-100 text-amber-700',
      WebOrder:     'bg-cyan-100 text-cyan-700',
    };
    return map[type] ?? 'bg-gray-100 text-gray-700';
  }
}
