import { CommonModule } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { Component, OnDestroy, OnInit } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Subject } from 'rxjs';
import { finalize, takeUntil } from 'rxjs/operators';
import {
  Order,
  OrderItem
} from '../../../models/order.model';
import { OrderService } from '../../../services/order.service';
import { PaginationComponent } from '../../../shared/pagination/pagination.component';
import { PageSlicePipe } from '../../../shared/pagination/page-slice.pipe';

@Component({
  selector: 'app-shipment-queue',
  standalone: true,
  imports: [CommonModule, FormsModule, PaginationComponent, PageSlicePipe],
  templateUrl: './shipment-queue.component.html',
  styleUrl: './shipment-queue.component.scss'
})
export class ShipmentQueueComponent implements OnInit, OnDestroy {
  orders: Order[] = [];
  totalCount = 0;
  loading = false;
  preparingOrderId: string | null = null;
  submitting = false;
  selectedOrder: Order | null = null;
  errorMessage = '';
  successMessage = '';

  readonly carriers = ['Ritiro Cliente', 'Vettore Proprio', 'GLS', 'BRT', 'DHL'];
  carrierName = 'GLS';
  trackingCode = '';
  boxCount = 1;
  weightKg: number | null = null;
  shipmentQuantities: Record<string, number> = {};
  page = 1;
  pageSize = 50;
  detailItemsPage = 1;
  detailItemsPageSize = 20;

  private readonly destroy$ = new Subject<void>();

  constructor(private readonly orderService: OrderService) {}

  ngOnInit(): void {
    this.loadQueue();
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  loadQueue(): void {
    this.loading = true;
    this.errorMessage = '';
    this.orderService.getFulfillmentQueue(this.page, this.pageSize).pipe(
      takeUntil(this.destroy$),
      finalize(() => { this.loading = false; })
    ).subscribe({
      next: result => {
        this.orders = result.items;
        this.totalCount = result.totalCount;
      },
      error: error => this.showError(error, 'Impossibile caricare la coda di spedizione.')
    });
  }

  prepareShipment(order: Order): void {
    this.errorMessage = '';
    this.successMessage = '';
    if (order.status !== 'Confirmed') {
      this.selectedOrder = order;
      this.loadOrder(order.id);
      return;
    }

    this.selectedOrder = null;
    const startPicking = order.permittedActions.find(action => action.action === 'StartPicking');
    if (!startPicking) {
      this.selectedOrder = null;
      this.errorMessage = 'L’ordine non consente l’avvio del prelievo.';
      return;
    }

    this.preparingOrderId = order.id;
    this.orderService.postAction(startPicking).pipe(
      takeUntil(this.destroy$),
      finalize(() => { this.preparingOrderId = null; })
    ).subscribe({
      next: () => this.loadOrder(order.id),
      error: error => {
        this.selectedOrder = null;
        this.showError(error, 'Impossibile prendere in carico l’ordine.');
      }
    });
  }

  closeShipment(): void {
    if (!this.submitting) {
      this.selectedOrder = null;
    }
  }

  quantityFor(item: OrderItem): number {
    return this.shipmentQuantities[item.id] ?? 0;
  }

  onQuantityChange(item: OrderItem): void {
    const entered = Number(this.shipmentQuantities[item.id]);
    this.shipmentQuantities[item.id] = Number.isFinite(entered)
      ? Math.min(item.remainingQuantity, Math.max(0, Math.trunc(entered)))
      : 0;
  }

  get totalToShip(): number {
    return Object.values(this.shipmentQuantities).reduce((total, quantity) => total + quantity, 0);
  }

  get remainingPieces(): number {
    return this.selectedOrder?.items.reduce((total, item) => total + item.remainingQuantity, 0) ?? 0;
  }

  remainingForOrder(order: Order): number {
    return order.items.reduce((total, item) => total + item.remainingQuantity, 0);
  }

  get totalPages(): number {
    return Math.max(1, Math.ceil(this.totalCount / this.pageSize));
  }

  changePage(page: number): void {
    if (page < 1 || page > this.totalPages || page === this.page) {
      return;
    }
    this.page = page;
    this.loadQueue();
  }

  changePageSize(pageSize: number): void {
    this.pageSize = pageSize;
    this.page = 1;
    this.loadQueue();
  }

  changeDetailItemsPageSize(pageSize: number): void {
    this.detailItemsPageSize = pageSize;
    this.detailItemsPage = 1;
  }

  get isShipmentAtBalance(): boolean {
    const lines = this.selectedOrder?.items.filter(item => item.remainingQuantity > 0) ?? [];
    return lines.length > 0 && lines.every(item => this.quantityFor(item) === item.remainingQuantity);
  }

  shipmentTypeLabel(): string {
    return this.isShipmentAtBalance ? 'Spedizione Totale / A Saldo' : 'Spedizione Parziale';
  }

  shipmentTypeClass(): string {
    return this.isShipmentAtBalance ? 'shipment-badge shipment-badge--full' : 'shipment-badge shipment-badge--partial';
  }

  normalizeBoxCount(): void {
    this.boxCount = Number.isFinite(this.boxCount) ? Math.max(1, Math.trunc(this.boxCount)) : 1;
  }

  statusClass(status: string): string {
    return ({
      Confirmed: 'status-badge status-badge--confirmed',
      Picking: 'status-badge status-badge--processing',
      Packing: 'status-badge status-badge--processing',
      PartiallyShipped: 'status-badge status-badge--partial'
    } as Record<string, string>)[status] ?? 'status-badge';
  }

  submitShipment(): void {
    const order = this.selectedOrder;
    if (!order || this.submitting || this.totalToShip <= 0) {
      return;
    }

    this.errorMessage = '';
    this.submitting = true;
    this.orderService.createShipment(order.id, {
      carrierName: this.carrierName,
      trackingCode: this.trackingCode.trim() || null,
      boxCount: this.boxCount,
      weightKg: this.weightKg,
      items: order.items
        .map(item => ({ orderItemId: item.id, quantity: this.quantityFor(item) }))
        .filter(item => item.quantity > 0)
    }).pipe(
      takeUntil(this.destroy$),
      finalize(() => { this.submitting = false; })
    ).subscribe({
      next: result => {
        const ddtNumber = result.shipment?.ddtNumber ?? result.order.linkedDocuments?.ddtNumber;
        this.successMessage = ddtNumber
          ? `DDT ${ddtNumber} emesso correttamente.`
          : 'Spedizione e DDT emessi correttamente.';
        this.openPdf(result.documentBytes, result.contentType, result.documentFileName);
        this.selectedOrder = null;
        this.loadQueue();
      },
      error: error => this.showError(error, 'Impossibile confermare il prelievo ed emettere il DDT.')
    });
  }

  private loadOrder(orderId: string): void {
    this.orderService.getById(orderId).pipe(
      takeUntil(this.destroy$)
    ).subscribe({
      next: order => {
        this.selectedOrder = order;
        this.detailItemsPage = 1;
        this.carrierName = 'GLS';
        this.trackingCode = '';
        this.boxCount = 1;
        this.weightKg = null;
        this.shipmentQuantities = Object.fromEntries(
          order.items.map(item => [item.id, item.remainingQuantity])
        );
      },
      error: error => {
        this.selectedOrder = null;
        this.showError(error, 'Impossibile caricare le righe dell’ordine.');
      }
    });
  }

  private openPdf(encodedPdf: string, contentType: string, fileName: string): void {
    const binary = window.atob(encodedPdf);
    const bytes = new Uint8Array(binary.length);
    for (let index = 0; index < binary.length; index++) {
      bytes[index] = binary.charCodeAt(index);
    }

    const url = URL.createObjectURL(new Blob([bytes], { type: contentType }));
    window.open(url, '_blank', 'noopener');
    const link = document.createElement('a');
    link.href = url;
    link.download = fileName;
    link.click();
    window.setTimeout(() => URL.revokeObjectURL(url), 60_000);
  }

  private showError(error: unknown, fallback: string): void {
    this.errorMessage = fallback;
    if (error instanceof HttpErrorResponse) {
      if (typeof error.error === 'string' && error.error.trim()) {
        this.errorMessage = error.error.trim();
      } else if (error.error instanceof Blob) {
        void error.error.text().then(text => {
          if (text.trim()) {
            this.errorMessage = text.trim();
          }
        });
      }
    }
  }
}
