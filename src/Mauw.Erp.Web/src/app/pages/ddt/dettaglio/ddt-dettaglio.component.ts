import { CommonModule } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { Component, OnDestroy, OnInit } from '@angular/core';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { Subject } from 'rxjs';
import { finalize, map, takeUntil } from 'rxjs/operators';
import { Ddt } from '../../../models/ddt.model';
import { Order } from '../../../models/order.model';
import { DdtService } from '../../../services/ddt.service';
import { OrderService } from '../../../services/order.service';
import { BackButtonComponent } from '../../../shared/back-button/back-button.component';
import { PaginationComponent } from '../../../shared/pagination/pagination.component';
import { PageSlicePipe } from '../../../shared/pagination/page-slice.pipe';

@Component({
  selector: 'app-ddt-dettaglio',
  standalone: true,
  imports: [CommonModule, RouterLink, BackButtonComponent, PaginationComponent, PageSlicePipe],
  templateUrl: './ddt-dettaglio.component.html'
})
export class DdtDettaglioComponent implements OnInit, OnDestroy {
  ddt: Ddt | null = null;
  relatedOrder: Order | null = null;
  loading = true;
  loadingOrder = false;
  errorMessage = '';
  relatedOrderError = '';
  returnUrl = '/ddt/elenco';
  itemsPage = 1;
  itemsPageSize = 20;

  private readonly destroy$ = new Subject<void>();

  constructor(
    private readonly route: ActivatedRoute,
    private readonly router: Router,
    private readonly ddtsApi: DdtService,
    private readonly ordersApi: OrderService
  ) {}

  ngOnInit(): void {
    this.route.queryParamMap.pipe(
      map(params => params.get('returnTo')),
      takeUntil(this.destroy$)
    ).subscribe(value => {
      this.returnUrl = this.safeReturnUrl(value) ?? '/ddt/elenco';
    });

    this.route.paramMap.pipe(takeUntil(this.destroy$)).subscribe(params => {
      const id = params.get('id');
      if (!id) {
        this.loading = false;
        this.errorMessage = 'Identificativo DDT non valido.';
        return;
      }

      this.loading = true;
      this.errorMessage = '';
      this.ddtsApi.getById(id).pipe(
        takeUntil(this.destroy$),
        finalize(() => { this.loading = false; })
      ).subscribe({
        next: ddt => {
          this.ddt = ddt;
          this.itemsPage = 1;
          this.loadRelatedOrder(ddt);
        },
        error: error => { this.errorMessage = this.describeError(error, 'Impossibile caricare il DDT.'); }
      });
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

  private loadRelatedOrder(ddt: Ddt): void {
    this.relatedOrder = null;
    this.relatedOrderError = '';
    if (!ddt.orderId) {
      return;
    }

    this.loadingOrder = true;
    this.ordersApi.getById(ddt.orderId).pipe(
      takeUntil(this.destroy$),
      finalize(() => { this.loadingOrder = false; })
    ).subscribe({
      next: order => { this.relatedOrder = order; },
      error: error => {
        this.relatedOrderError = this.describeError(error, 'Impossibile caricare l’ordine collegato.');
      }
    });
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
}
