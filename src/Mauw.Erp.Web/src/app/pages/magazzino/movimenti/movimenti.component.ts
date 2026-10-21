import { Component, OnInit, OnDestroy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule, ReactiveFormsModule, FormBuilder, FormGroup, Validators } from '@angular/forms';
import { Subject } from 'rxjs';
import { debounceTime, distinctUntilChanged, takeUntil } from 'rxjs/operators';
import { ApiService } from '../../../services/api.service';
import { WarehouseService } from '../../../services/warehouse.service';
import { WarehouseSummary } from '../../../models/warehouse.model';
import { PagedResult, SortChange } from '../../../models/pagination.model';
import { PaginationComponent } from '../../../shared/pagination/pagination.component';
import { SortHeaderDirective } from '../../../shared/sort-header/sort-header.directive';

interface StockMovementDetailDto {
  id: string;
  variantId: string;
  barcode: string;
  productName: string;
  warehouseId: string | null;
  warehouseCode: string | null;
  reasonLabel: string | null;
  quantity: number;
  movementDateUtc: string;
  notes: string | null;
  documentReference: string | null;
}

interface VariantSearchDto {
  variantId: string;
  productId: string;
  productName: string;
  sku: string | null;
  barcode: string;
  colorName: string | null;
  sizeName: string | null;
  currentStock: number;
}

const REASON_LABELS: Record<number, string> = {
  1: 'Carico Fornitore', 2: 'Vendita Cassa', 3: 'Spedizione Ordine',
  4: 'Trasferimento', 5: 'Reso Cliente', 6: 'Reso Fornitore', 7: 'Rettifica Inventario'
};

@Component({
  selector: 'app-movimenti',
  standalone: true,
  imports: [CommonModule, FormsModule, ReactiveFormsModule, PaginationComponent, SortHeaderDirective],
  templateUrl: './movimenti.component.html'
})
export class MovimentiComponent implements OnInit, OnDestroy {
  movements: StockMovementDetailDto[] = [];
  warehouses: WarehouseSummary[] = [];

  page = 1; pageSize = 20; totalCount = 0; totalPages = 0;
  sortBy = 'movementDateUtc';
  sortDescending = true;
  loading = false;
  errorMessage = ''; successMessage = '';
  showForm = false;

  // Variant autocomplete state
  variantSearchText = '';            // testo visibile nel campo
  variantSuggestions: VariantSearchDto[] = [];
  variantSearchLoading = false;
  showSuggestions = false;
  selectedVariantLabel = '';         // etichetta leggibile dopo selezione

  private variantSearch$ = new Subject<string>();
  private destroy$ = new Subject<void>();

  readonly reasonTypes = Object.entries(REASON_LABELS).map(([k, v]) => ({ value: +k, label: v }));

  form!: FormGroup;

  constructor(
    private api: ApiService,
    private warehouseService: WarehouseService,
    private fb: FormBuilder
  ) {
    this.form = this.fb.group({
      warehouseId:      ['', Validators.required],
      productVariantId: ['', Validators.required],
      reason:           [1, Validators.required],
      quantity:         [0, [Validators.required]],
      notes:            [''],
      documentReference:['']
    });
  }

  ngOnInit(): void {
    this.warehouseService.getList({ page: 1, pageSize: 100 }).subscribe({
      next: r => this.warehouses = r.items, error: () => {}
    });
    this.load();

    // Debounce variant search with 250ms
    this.variantSearch$.pipe(
      debounceTime(250),
      distinctUntilChanged(),
      takeUntil(this.destroy$)
    ).subscribe(q => this.runVariantSearch(q));
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  // ── Variant autocomplete ─────────────────────────────────────────────────

  onVariantInput(): void {
    const q = this.variantSearchText.trim();
    this.form.get('productVariantId')!.setValue(''); // invalidate until selection
    this.selectedVariantLabel = '';
    if (q.length < 1) { this.variantSuggestions = []; this.showSuggestions = false; return; }
    this.variantSearch$.next(q);
  }

  private runVariantSearch(q: string): void {
    this.variantSearchLoading = true;
    const whId = this.form.get('warehouseId')!.value;
    const whParam = whId ? `&warehouseId=${whId}` : '';
    this.api.get<VariantSearchDto[]>(
      `v1/products/variants/search?q=${encodeURIComponent(q)}&limit=10${whParam}`
    ).subscribe({
      next: results => {
        this.variantSuggestions = results;
        this.showSuggestions = results.length > 0;
        this.variantSearchLoading = false;
      },
      error: () => { this.variantSearchLoading = false; }
    });
  }

  selectVariant(v: VariantSearchDto): void {
    this.form.get('productVariantId')!.setValue(v.variantId);
    const parts = [v.productName];
    if (v.colorName) parts.push(v.colorName);
    if (v.sizeName)  parts.push(v.sizeName);
    parts.push(`(${v.barcode})`);
    this.selectedVariantLabel = parts.join(' — ');
    this.variantSearchText = this.selectedVariantLabel;
    this.showSuggestions = false;
    this.variantSuggestions = [];
  }

  clearVariant(): void {
    this.variantSearchText = '';
    this.selectedVariantLabel = '';
    this.form.get('productVariantId')!.setValue('');
    this.variantSuggestions = [];
    this.showSuggestions = false;
  }

  hideSuggestionsDelayed(): void {
    // Delay to allow click on suggestion to register before blur closes dropdown
    setTimeout(() => { this.showSuggestions = false; }, 200);
  }

  variantLabel(v: VariantSearchDto): string {
    const parts = [v.productName];
    if (v.colorName) parts.push(v.colorName);
    if (v.sizeName)  parts.push(v.sizeName);
    return parts.join(' — ');
  }

  stockBadgeClass(stock: number): string {
    if (stock <= 0) return 'bg-red-100 text-red-700';
    if (stock <= 5) return 'bg-orange-100 text-orange-700';
    return 'bg-green-100 text-green-700';
  }

  // ── List & form ──────────────────────────────────────────────────────────

  load(): void {
    this.loading = true;
    this.api.get<PagedResult<StockMovementDetailDto>>(
      `v1/inventory/movements-paged?page=${this.page}&pageSize=${this.pageSize}&sortBy=${this.sortBy}&sortDescending=${this.sortDescending}`
    ).subscribe({
      next: r => {
        this.movements = r.items;
        this.totalCount = r.totalCount;
        this.totalPages = r.totalPages;
        if (this.totalPages > 0 && this.page > this.totalPages) {
          this.page = this.totalPages;
          this.load();
          return;
        }
        this.loading = false;
      },
      error: () => { this.errorMessage = 'Errore caricamento movimenti.'; this.loading = false; }
    });
  }

  openForm(): void {
    this.showForm = true;
    this.form.reset({ reason: 1 });
    this.clearVariant();
    this.errorMessage = '';
  }

  closeForm(): void {
    this.showForm = false;
    this.clearVariant();
  }

  onSubmit(): void {
    if (this.form.invalid) return;
    this.loading = true;
    this.api.post<any>('v1/inventory/movements', this.form.value).subscribe({
      next: () => {
        this.successMessage = 'Movimento registrato con successo.';
        this.closeForm(); this.load();
        setTimeout(() => this.successMessage = '', 4000);
      },
      error: err => { this.errorMessage = err.error ?? 'Errore nella registrazione.'; this.loading = false; }
    });
  }

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

  qtyClass(qty: number): string {
    return qty > 0 ? 'text-green-700 font-bold' : 'text-red-600 font-bold';
  }
}
