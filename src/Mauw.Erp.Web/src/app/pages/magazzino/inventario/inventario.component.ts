import { ChangeDetectionStrategy, Component, OnDestroy, OnInit, computed, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { FormsModule } from '@angular/forms';
import { EMPTY, Subject, catchError, debounceTime, finalize, switchMap, takeUntil } from 'rxjs';
import { WarehouseService } from '../../../services/warehouse.service';
import { InventoryStockService } from '../../../services/inventory-stock.service';
import { ProductService } from '../../../services/product.service';
import { AuthService } from '../../../services/auth.service';
import {
  InventoryStock,
  InventoryStockFilters,
  InventoryStockMovement,
  InventoryStockTotals,
  ValuationMethod
} from '../../../models/inventory-stock.model';
import { WarehouseSummary } from '../../../models/warehouse.model';
import { CategoryDto } from '../../../models/product.model';
import { SortChange } from '../../../models/pagination.model';
import { PaginationComponent } from '../../../shared/pagination/pagination.component';
import { SortHeaderDirective } from '../../../shared/sort-header/sort-header.directive';

@Component({
  selector: 'app-inventario',
  standalone: true,
  imports: [CommonModule, FormsModule, PaginationComponent, SortHeaderDirective],
  templateUrl: './inventario.component.html',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class InventarioComponent implements OnInit, OnDestroy {
  readonly canFilterByDate: boolean;
  // Costi e valorizzazioni sono omessi dal server per i ruoli diversi da Admin e Accounting.
  readonly canSeeCosts: boolean;
  readonly stockItems = signal<InventoryStock[]>([]);
  readonly warehouses = signal<WarehouseSummary[]>([]);
  readonly categories = signal<CategoryDto[]>([]);
  readonly totals = signal<InventoryStockTotals>({
    totalPhysicalQuantity: 0,
    totalReservedQuantity: 0,
    totalIncomingQuantity: 0,
    totalAvailableQuantity: 0,
    totalValue: 0
  });
  readonly hasLoadedSummary = signal(false);
  readonly hasLoadedStocks = signal(false);
  readonly allocationNote = signal('');

  readonly filterWarehouseId = signal('');
  readonly filterCategoryId = signal('');
  readonly search = signal('');
  readonly filterAtDate = signal('');
  readonly onlyInStock = signal(false);
  readonly valuationMethod = signal<ValuationMethod>('WeightedAverageCost');
  readonly page = signal(1);
  readonly pageSize = signal(20);
  readonly sortBy = signal('warehouseCode');
  readonly sortDescending = signal(false);
  readonly totalCount = signal(0);
  readonly totalPages = signal(0);
  readonly loading = signal(false);
  readonly exporting = signal<'csv' | 'pdf' | null>(null);
  readonly errorMessage = signal('');

  readonly selectedStock = signal<InventoryStock | null>(null);
  readonly movements = signal<InventoryStockMovement[]>([]);
  readonly movementsLoading = signal(false);
  readonly movementsError = signal('');
  readonly movementPage = signal(1);
  readonly movementPageSize = signal(20);
  readonly movementTotalCount = signal(0);
  readonly movementTotalPages = signal(0);

  readonly valuationMethodLabel = computed(() =>
    this.valuationMethod() === 'LastPurchaseCost'
      ? 'Ultimo costo'
      : 'Costo medio ponderato'
  );
  readonly filters = computed<InventoryStockFilters>(() => ({
    search: this.search().trim() || undefined,
    warehouseId: this.filterWarehouseId() || undefined,
    categoryId: this.filterCategoryId() || undefined,
    atDate: this.atDateIso(),
    onlyInStock: this.onlyInStock(),
    valuationMethod: this.valuationMethod(),
    page: this.page(),
    pageSize: this.pageSize(),
    sortBy: this.sortBy(),
    sortDescending: this.sortDescending()
  }));

  private readonly searchChanges = new Subject<void>();
  private readonly loadRequests = new Subject<void>();
  private readonly summaryRequests = new Subject<void>();
  private readonly movementRequests = new Subject<{
    stock: InventoryStock;
    page: number;
    pageSize: number;
    atDate?: string;
  }>();
  private readonly destroyed = new Subject<void>();

  constructor(
    private readonly inventoryStockService: InventoryStockService,
    private readonly warehouseService: WarehouseService,
    private readonly productService: ProductService,
    private readonly auth: AuthService
  ) {
    // Il server risponde 403 alle richieste storiche (atDate) per ruoli diversi da Admin e Accounting.
    this.canFilterByDate = this.auth.hasRole('Admin') || this.auth.hasRole('Accounting');
    this.canSeeCosts = this.auth.canSeeCosts();
  }

  ngOnInit(): void {
    this.searchChanges.pipe(
      debounceTime(300),
      takeUntil(this.destroyed)
    ).subscribe(() => {
      this.page.set(1);
      this.load();
      this.loadSummary();
    });

    this.loadRequests.pipe(
      switchMap(() => {
        this.loading.set(true);
        this.errorMessage.set('');
        return this.inventoryStockService.getStocks(this.filters()).pipe(
          catchError(error => {
            this.errorMessage.set(this.getErrorMessage(error, 'Errore nel caricamento delle giacenze.'));
            return EMPTY;
          }),
          finalize(() => this.loading.set(false))
        );
      }),
      takeUntil(this.destroyed)
    ).subscribe(response => {
      this.stockItems.set(response.items);
      this.hasLoadedStocks.set(true);
      this.allocationNote.set(response.allocationNote ?? '');
    });

    this.summaryRequests.pipe(
      switchMap(() => {
        this.hasLoadedSummary.set(false);
        return this.inventoryStockService.getSummary(this.filters()).pipe(
          catchError(error => {
            this.errorMessage.set(this.getErrorMessage(error, 'Errore nel caricamento dei totali inventario.'));
            return EMPTY;
          })
        );
      }),
      takeUntil(this.destroyed)
    ).subscribe(summary => {
      this.totals.set(summary.totals);
      this.totalCount.set(summary.totalCount);
      const totalPages = Math.ceil(summary.totalCount / this.pageSize());
      this.totalPages.set(totalPages);
      this.hasLoadedSummary.set(true);
      if (this.page() > totalPages) {
        this.page.set(Math.max(1, totalPages));
        this.load();
      }
    });

    this.movementRequests.pipe(
      switchMap(request => {
        this.movementsLoading.set(true);
        this.movementsError.set('');
        return this.inventoryStockService.getMovements(
          request.stock.warehouseId,
          request.stock.productVariantId,
          request.atDate,
          request.page,
          request.pageSize
        ).pipe(
          catchError(() => {
            this.movementsError.set('Errore nel caricamento del mastrino.');
            return EMPTY;
          }),
          finalize(() => this.movementsLoading.set(false))
        );
      }),
      takeUntil(this.destroyed)
    ).subscribe(response => {
      this.movements.set(response.items);
      this.movementPage.set(response.page);
      this.movementTotalCount.set(response.totalCount);
      this.movementTotalPages.set(response.totalPages);
    });

    this.warehouseService.getList({ page: 1, pageSize: 100, isActive: true }).subscribe({
      next: response => this.warehouses.set(response.items),
      error: () => this.errorMessage.set('Errore nel caricamento dei magazzini.')
    });
    this.productService.getCategories().subscribe({
      next: categories => this.categories.set(categories),
      error: () => this.errorMessage.set('Errore nel caricamento delle categorie.')
    });
    this.load();
    this.loadSummary();
  }

  ngOnDestroy(): void {
    this.destroyed.next();
    this.destroyed.complete();
    this.searchChanges.complete();
    this.loadRequests.complete();
    this.summaryRequests.complete();
    this.movementRequests.complete();
  }

  load(): void {
    this.loadRequests.next();
  }

  private loadSummary(): void {
    this.summaryRequests.next();
  }

  onSearchChange(): void {
    this.searchChanges.next();
  }

  onFilterChange(): void {
    this.page.set(1);
    this.load();
    this.loadSummary();
  }

  clearDateFilter(): void {
    this.filterAtDate.set('');
    this.onFilterChange();
  }

  goToPage(page: number): void {
    if (page >= 1 && page <= this.totalPages() && page !== this.page()) {
      this.page.set(page);
      this.load();
    }
  }

  onPageSizeChange(pageSize: number): void {
    const normalizedPageSize = Math.max(1, pageSize);
    this.pageSize.set(normalizedPageSize);
    this.totalPages.set(Math.ceil(this.totalCount() / normalizedPageSize));
    this.page.set(1);
    this.load();
  }

  onSortChange(sort: SortChange): void {
    this.sortBy.set(sort.sortBy);
    this.sortDescending.set(sort.sortDescending);
    this.page.set(1);
    this.load();
  }

  openMovements(stock: InventoryStock): void {
    this.selectedStock.set(stock);
    this.movements.set([]);
    this.movementPage.set(1);
    this.loadMovements();
  }

  goToMovementPage(page: number): void {
    if (page >= 1 && page <= this.movementTotalPages() && page !== this.movementPage()) {
      this.movementPage.set(page);
      this.loadMovements();
    }
  }

  closeMovements(): void {
    this.selectedStock.set(null);
    this.movements.set([]);
    this.movementTotalCount.set(0);
    this.movementTotalPages.set(0);
  }

  exportCsv(): void {
    this.download('csv');
  }

  exportPdf(): void {
    this.download('pdf');
  }

  stockClass(quantity: number): string {
    if (quantity <= 0) return 'bg-red-100 text-red-800';
    if (quantity <= 5) return 'bg-orange-100 text-orange-800';
    return 'bg-green-100 text-green-800';
  }

  movementLabel(movement: InventoryStockMovement): string {
    if (movement.quantityDelta == null) return 'Direzione non disponibile';
    if (movement.quantityDelta > 0) return `+${movement.quantityDelta}`;
    return String(movement.quantityDelta);
  }

  costSourceLabel(stock: InventoryStock): string {
    return stock.costSource === 'PurchaseCost'
      ? 'Costo da anagrafica acquisto (fallback)'
      : 'Costo da movimenti di carico';
  }

  private loadMovements(): void {
    const stock = this.selectedStock();
    if (!stock) return;

    this.movementRequests.next({
      stock,
      page: this.movementPage(),
      pageSize: this.movementPageSize(),
      atDate: this.atDateIso()
    });
  }

  private atDateIso(): string | undefined {
    if (!this.canFilterByDate) return undefined;
    const date = this.filterAtDate();
    if (!date) return undefined;
    const [year, month, day] = date.split('-').map(Number);
    return new Date(year, month - 1, day, 23, 59, 59, 999).toISOString();
  }

  private download(format: 'csv' | 'pdf'): void {
    this.exporting.set(format);
    this.errorMessage.set('');
    const request = format === 'csv'
      ? this.inventoryStockService.exportCsv(this.filters())
      : this.inventoryStockService.exportPdf(this.filters());

    request.pipe(
      finalize(() => this.exporting.set(null))
    ).subscribe({
      next: blob => {
        const url = URL.createObjectURL(blob);
        const link = document.createElement('a');
        link.href = url;
        link.download = `giacenze-${new Date().toISOString().slice(0, 10)}.${format}`;
        link.click();
        URL.revokeObjectURL(url);
      },
      error: error => this.errorMessage.set(
        this.getErrorMessage(error, `Errore nell'esportazione ${format.toUpperCase()}.`)
      )
    });
  }

  private getErrorMessage(error: unknown, fallback: string): string {
    if (!(error instanceof HttpErrorResponse)) return fallback;

    const body: unknown = error.error;
    if (typeof body === 'string' && body.trim()) return body;
    if (body && typeof body === 'object') {
      if ('detail' in body && typeof body.detail === 'string') return body.detail;
      if ('title' in body && typeof body.title === 'string') return body.title;
      if ('error' in body && typeof body.error === 'string') return body.error;
    }
    return fallback;
  }
}
