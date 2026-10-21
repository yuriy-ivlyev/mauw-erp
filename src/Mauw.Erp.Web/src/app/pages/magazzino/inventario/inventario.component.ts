import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ApiService } from '../../../services/api.service';
import { WarehouseService } from '../../../services/warehouse.service';
import { WarehouseSummary } from '../../../models/warehouse.model';
import { PagedResult, SortChange } from '../../../models/pagination.model';
import { PaginationComponent } from '../../../shared/pagination/pagination.component';
import { SortHeaderDirective } from '../../../shared/sort-header/sort-header.directive';

interface StockItemDto {
  id: string;
  warehouseId: string;
  warehouseCode: string;
  warehouseName: string;
  productVariantId: string;
  barcode: string;
  productName: string;
  color: string | null;
  size: string | null;
  quantityOnHand: number;
  quantityReserved: number;
  quantityAvailable: number;
  unitCost: number | null;
  estimatedValue: number | null;
}

@Component({
  selector: 'app-inventario',
  standalone: true,
  imports: [CommonModule, FormsModule, PaginationComponent, SortHeaderDirective],
  templateUrl: './inventario.component.html'
})
export class InventarioComponent implements OnInit {
  stockItems: StockItemDto[] = [];
  warehouses: WarehouseSummary[] = [];

  filterWarehouseId = '';
  search = '';
  page = 1;
  pageSize = 20;
  sortBy = 'warehouseCode';
  sortDescending = false;
  totalCount = 0;
  totalPages = 0;

  loading = false;
  errorMessage = '';

  constructor(private api: ApiService, private warehouseService: WarehouseService) {}

  ngOnInit(): void {
    this.loadWarehouses();
    this.load();
  }

  loadWarehouses(): void {
    this.warehouseService.getList({ page: 1, pageSize: 100 }).subscribe({
      next: r => this.warehouses = r.items,
      error: () => {}
    });
  }

  load(): void {
    this.loading = true;
    let url = `v1/inventory/stock?page=${this.page}&pageSize=${this.pageSize}&sortBy=${this.sortBy}&sortDescending=${this.sortDescending}`;
    if (this.filterWarehouseId) url += `&warehouseId=${this.filterWarehouseId}`;
    if (this.search.trim()) url += `&search=${encodeURIComponent(this.search.trim())}`;

    this.api.get<PagedResult<StockItemDto>>(url).subscribe({
      next: r => {
        this.stockItems = r.items;
        this.totalCount = r.totalCount;
        this.totalPages = r.totalPages;
        if (this.totalPages > 0 && this.page > this.totalPages) {
          this.page = this.totalPages;
          this.load();
          return;
        }
        this.loading = false;
      },
      error: () => {
        this.errorMessage = 'Errore nel caricamento delle giacenze.';
        this.loading = false;
      }
    });
  }

  onFilterChange(): void { this.page = 1; this.load(); }
  onSearch(): void { this.page = 1; this.load(); }
  goToPage(page: number): void {
    if (page >= 1 && page <= this.totalPages && page !== this.page) {
      this.page = page;
      this.load();
    }
  }

  onPageSizeChange(pageSize: number): void {
    this.pageSize = pageSize;
    this.page = 1;
    this.load();
  }

  onSortChange(sort: SortChange): void {
    this.sortBy = sort.sortBy;
    this.sortDescending = sort.sortDescending;
    this.page = 1;
    this.load();
  }

  totalEstimatedValue(): number {
    return this.stockItems.reduce((s, i) => s + (i.estimatedValue ?? 0), 0);
  }

  stockClass(qty: number): string {
    if (qty <= 0) return 'text-red-600 font-semibold';
    if (qty <= 5) return 'text-orange-500 font-semibold';
    return 'text-green-700 font-semibold';
  }
}
