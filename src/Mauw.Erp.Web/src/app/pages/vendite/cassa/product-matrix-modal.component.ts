import { Component, Input, Output, EventEmitter, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatrixCellDto, MatrixColorDto, MatrixSizeDto, ProductMatrixDto } from '../../../models/product.model';
import { AddActionButtonComponent } from '../../../shared/add-action-button/add-action-button.component';

export interface MatrixSelection {
  variantId: string;
  barcode: string;
  productName: string;
  colorName: string | null;
  sizeName: string | null;
  unitNetPrice: number;
  quantity: number;
  availableStock: number;
  vatRate: number;
  weightGrams?: number | null;
}

@Component({
  selector: 'app-product-matrix-modal',
  standalone: true,
  imports: [CommonModule, AddActionButtonComponent],
  template: `
<div class="fixed inset-0 z-50 flex items-center justify-center bg-black/50 p-4" (click)="onBackdropClick($event)">
  <div class="bg-white rounded-2xl shadow-2xl w-full max-w-5xl max-h-[90vh] flex flex-col" (click)="$event.stopPropagation()">

    <!-- Header -->
    <div class="px-6 py-4 border-b flex items-center justify-between flex-shrink-0">
      <div>
        <h2 class="text-lg font-bold text-gray-800">Seleziona Taglia / Colore</h2>
        <p class="text-sm text-gray-500 mt-0.5">
          <span class="font-semibold text-blue-700">[{{ matrix.supplierCode }}]</span>
          {{ matrix.productName }}
        </p>
      </div>
      <button (click)="cancel.emit()" class="text-gray-400 hover:text-gray-700 text-2xl leading-none transition">&times;</button>
    </div>

    <!-- Matrix grid -->
    <div class="flex-1 overflow-auto p-4">
      <div *ngIf="matrix.sizes.length === 0 && matrix.colors.length === 0 && matrix.cells.length === 1">
        <!-- Single-variant product -->
        <div class="flex items-center justify-center p-8">
          <div class="text-center">
            <p class="text-gray-500 mb-4">Prodotto senza varianti taglia/colore</p>
            <div class="inline-flex items-center gap-3 bg-gray-50 border rounded-xl px-6 py-4">
              <span class="text-sm font-mono text-gray-600">{{ matrix.cells[0].barcode }}</span>
              <span class="text-lg font-bold text-blue-700">{{ matrix.cells[0].unitNetPrice | number:'1.2-2' }} € + IVA</span>
              <span [class]="stockBadgeClass(matrix.cells[0].availableStock)">
                {{ matrix.cells[0].availableStock }} pz
              </span>
            </div>
          </div>
        </div>
      </div>

      <div *ngIf="matrix.sizes.length > 0 || matrix.colors.length > 0" class="overflow-x-auto">
        <table class="w-full text-sm border-collapse">
          <thead>
            <tr>
              <!-- Corner -->
              <th class="sticky left-0 z-10 bg-gray-100 border border-gray-200 px-3 py-2 text-left text-xs font-semibold text-gray-600 min-w-[100px]">
                Colore / Taglia
              </th>
              <th *ngFor="let size of matrix.sizes"
                class="border border-gray-200 px-3 py-2 text-center text-xs font-semibold text-gray-700 bg-gray-100 whitespace-nowrap min-w-[90px]">
                {{ size.name }}
              </th>
            </tr>
          </thead>
          <tbody>
            <tr *ngFor="let color of matrix.colors" class="hover:bg-blue-50/30 transition-colors">
              <!-- Color label -->
              <td class="sticky left-0 z-10 bg-white border border-gray-200 px-3 py-2 font-semibold text-gray-700 text-xs whitespace-nowrap">
                <div class="flex items-center gap-2">
                  <span class="w-3 h-3 rounded-full inline-block border border-gray-300 flex-shrink-0"
                    [style.background-color]="color.htmlColor"></span>
                  {{ color.name }}
                </div>
              </td>
              <!-- Cells per size -->
              <td *ngFor="let size of matrix.sizes"
                class="border border-gray-200 p-1.5 text-center">
                <div *ngIf="hasCell(color.id, size.id); else emptyCell" class="flex flex-col items-center gap-1">
                  <!-- Stock badge -->
                  <span [class]="'text-xs font-medium px-1.5 py-0.5 rounded ' + stockBadgeClass(getCellStock(color.id, size.id))">
                    {{ getCellStock(color.id, size.id) > 0 ? (getCellStock(color.id, size.id) + ' pz') : '—' }}
                  </span>
                  <!-- Qty stepper -->
                  <div class="flex items-center gap-1" *ngIf="getCellStock(color.id, size.id) > 0">
                    <button (click)="decrementByIds(color.id, size.id)"
                      class="w-6 h-6 rounded bg-gray-100 hover:bg-gray-200 text-gray-700 font-bold text-sm transition flex items-center justify-center">
                      –
                    </button>
                    <span class="w-7 text-center font-bold text-gray-800 text-sm">{{ getQtyByIds(color.id, size.id) }}</span>
                    <button (click)="incrementByIds(color.id, size.id)"
                      [disabled]="getQtyByIds(color.id, size.id) >= getCellStock(color.id, size.id)"
                      class="w-6 h-6 rounded bg-blue-600 hover:bg-blue-700 disabled:bg-gray-300 text-white font-bold text-sm transition flex items-center justify-center">
                      +
                    </button>
                  </div>
                  <div *ngIf="getCellStock(color.id, size.id) <= 0" class="text-xs text-gray-400">Esaurito</div>
                </div>
                <ng-template #emptyCell>
                  <span class="text-gray-300 text-xs">—</span>
                </ng-template>
              </td>
            </tr>
          </tbody>
        </table>
      </div>
    </div>

    <!-- Footer -->
    <div class="px-6 py-4 border-t bg-gray-50 rounded-b-2xl flex items-center justify-between flex-shrink-0">
      <div class="text-sm text-gray-600">
        <span *ngIf="totalSelected > 0" class="font-semibold text-blue-700">
          {{ totalSelected }} pz selezionati
        </span>
        <span *ngIf="totalSelected === 0" class="text-gray-400">Nessuna quantità selezionata</span>
      </div>
      <div class="flex gap-3">
        <button (click)="cancel.emit()"
          class="px-4 py-2 border border-gray-300 hover:bg-gray-100 text-gray-700 font-medium rounded-lg text-sm transition">
          Annulla
        </button>
        <app-add-action-button [label]="'Aggiungi al carrello (' + totalSelected + ' pz)'"
          [disabled]="totalSelected === 0" (activated)="addToCart()">
        </app-add-action-button>
      </div>
    </div>
  </div>
</div>
  `,
  styles: [`
    :host { display: contents; }
  `]
})
export class ProductMatrixModalComponent implements OnInit {
  @Input() matrix!: ProductMatrixDto;
  @Output() cancel = new EventEmitter<void>();
  @Output() confirmed = new EventEmitter<MatrixSelection[]>();

  quantities: Map<string, number> = new Map();

  ngOnInit(): void {
    this.quantities.clear();
  }

  get totalSelected(): number {
    let total = 0;
    this.quantities.forEach(q => { total += q; });
    return total;
  }

  getCell(colorId: string, sizeId: string): MatrixCellDto | undefined {
    return this.matrix.cells.find(c => c.colorId === colorId && c.sizeId === sizeId);
  }

  getQty(variantId: string): number {
    return this.quantities.get(variantId) ?? 0;
  }

  increment(cell: MatrixCellDto): void {
    const current = this.getQty(cell.variantId);
    if (current < cell.availableStock) {
      this.quantities.set(cell.variantId, current + 1);
    }
  }

  decrement(cell: MatrixCellDto): void {
    const current = this.getQty(cell.variantId);
    if (current > 0) {
      if (current === 1) {
        this.quantities.delete(cell.variantId);
      } else {
        this.quantities.set(cell.variantId, current - 1);
      }
    }
  }

  stockBadgeClass(stock: number): string {
    if (stock <= 0) return 'bg-red-100 text-red-600';
    if (stock <= 3) return 'bg-orange-100 text-orange-600';
    return 'bg-green-100 text-green-700';
  }

  hasCell(colorId: string, sizeId: string): boolean {
    return this.matrix.cells.some(c => c.colorId === colorId && c.sizeId === sizeId);
  }

  getCellStock(colorId: string, sizeId: string): number {
    return this.getCell(colorId, sizeId)?.availableStock ?? 0;
  }

  getQtyByIds(colorId: string, sizeId: string): number {
    const cell = this.getCell(colorId, sizeId);
    return cell ? (this.quantities.get(cell.variantId) ?? 0) : 0;
  }

  incrementByIds(colorId: string, sizeId: string): void {
    const cell = this.getCell(colorId, sizeId);
    if (cell) this.increment(cell);
  }

  decrementByIds(colorId: string, sizeId: string): void {
    const cell = this.getCell(colorId, sizeId);
    if (cell) this.decrement(cell);
  }

  addToCart(): void {
    const selections: MatrixSelection[] = [];
    this.quantities.forEach((qty, variantId) => {
      if (qty <= 0) return;
      const cell = this.matrix.cells.find(c => c.variantId === variantId);
      if (!cell) return;

      const colorName = this.matrix.colors.find(c => c.id === cell.colorId)?.name ?? null;
      const sizeName = this.matrix.sizes.find(s => s.id === cell.sizeId)?.name ?? null;

      selections.push({
        variantId,
        barcode: cell.barcode,
        productName: this.matrix.productName,
        colorName,
        sizeName,
        unitNetPrice: cell.unitNetPrice,
        quantity: qty,
        availableStock: cell.availableStock,
        vatRate: cell.vatRate,
        weightGrams: cell.weightGrams
      });
    });

    this.confirmed.emit(selections);
  }

  onBackdropClick(event: MouseEvent): void {
    this.cancel.emit();
  }
}
