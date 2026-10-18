import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ApiService } from '../../../services/api.service';
import { WarehouseService } from '../../../services/warehouse.service';
import { WarehouseSummary } from '../../../models/warehouse.model';

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

interface PagedResult<T> {
  items: T[];
  totalCount: number;
  totalPages: number;
  pageNumber: number;
  pageSize: number;
}

@Component({
  selector: 'app-inventario',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './inventario.component.html'
})
export class InventarioComponent implements OnInit {
  stockItems: StockItemDto[] = [];
  warehouses: WarehouseSummary[] = [];

  filterWarehouseId = '';
  search = '';
  page = 1;
  pageSize = 30;
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
    let url = `v1/inventory/stock?page=${this.page}&pageSize=${this.pageSize}`;
    if (this.filterWarehouseId) url += `&warehouseId=${this.filterWarehouseId}`;
    if (this.search.trim()) url += `&search=${encodeURIComponent(this.search.trim())}`;

    this.api.get<PagedResult<StockItemDto>>(url).subscribe({
      next: r => {
        this.stockItems = r.items;
        this.totalCount = r.totalCount;
        this.totalPages = r.totalPages;
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
  prevPage(): void { if (this.page > 1) { this.page--; this.load(); } }
  nextPage(): void { if (this.page < this.totalPages) { this.page++; this.load(); } }

  totalEstimatedValue(): number {
    return this.stockItems.reduce((s, i) => s + (i.estimatedValue ?? 0), 0);
  }

  stockClass(qty: number): string {
    if (qty <= 0) return 'text-red-600 font-semibold';
    if (qty <= 5) return 'text-orange-500 font-semibold';
    return 'text-green-700 font-semibold';
  }
}
