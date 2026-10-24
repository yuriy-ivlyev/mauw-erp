import { Component, EventEmitter, Input, OnChanges, Output, SimpleChanges, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { Subject, catchError, map, of, switchMap, timer } from 'rxjs';
import { ProductService } from '../../../services/product.service';
import { CategoryDto, ProductParentDto, VariantSearchDto } from '../../../models/product.model';
import { priceError } from '../../../services/price-list.service';

@Component({
  selector: 'app-price-rule-target',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <div *ngIf="target === 'categoryId'">
      <label>Categoria<select aria-label="Categoria della regola" [ngModel]="targetId" (ngModelChange)="selected.emit($event)" class="border p-2 w-full">
        <option value="">Seleziona categoria</option>
        <option *ngFor="let category of categories" [value]="category.id">{{ category.name }}</option>
      </select></label>
    </div>
    <div *ngIf="target === 'productId' || target === 'variantId'">
      <label>{{ target === 'variantId' ? 'Cerca variante (SKU)' : 'Cerca prodotto' }}<input aria-label="Cerca bersaglio per nome o codice" [ngModel]="search"
        (ngModelChange)="search = $event; queries.next($event)" class="border p-2 w-full"
        [placeholder]="target === 'variantId' ? 'Barcode, nome prodotto, colore o taglia' : 'Nome o codice'"></label>
      <div *ngFor="let product of products">
        <button type="button" (click)="chooseProduct(product)" class="text-blue-700 text-left border-b p-2 w-full">
          {{ product.barcode }} · {{ product.name }}
        </button>
      </div>
      <div *ngFor="let variant of variants">
        <button type="button" (click)="chooseVariant(variant)" class="text-blue-700 text-left border-b p-2 w-full">
          {{ variant.productName }} · {{ variant.barcode }} · {{ variant.colorName || 'N/A' }} · {{ variant.sizeName || 'N/A' }}
        </button>
      </div>
      <p *ngIf="target === 'variantId'" class="text-sm text-gray-500">Seleziona la variante specifica dai risultati (massimo 30); usa il barcode per una ricerca precisa.</p>
    </div>
    <p *ngIf="error" role="alert" class="text-red-600 text-sm">{{ error }}</p>
  `
})
export class PriceRuleTargetComponent implements OnChanges {
  @Input() target = 'global';
  @Input() targetId = '';
  @Output() selected = new EventEmitter<string>();
  private service = inject(ProductService);
  queries = new Subject<string>();
  search = '';
  error = '';
  products: ProductParentDto[] = [];
  categories: CategoryDto[] = [];
  variants: VariantSearchDto[] = [];

  constructor() {
    this.service.getCategories().pipe(takeUntilDestroyed()).subscribe({
      next: categories => this.categories = categories, error: error => this.error = priceError(error)
    });
    this.queries.pipe(
      switchMap(query => {
        this.products = [];
        this.variants = [];
        this.error = '';
        const empty = { products: [] as ProductParentDto[], variants: [] as VariantSearchDto[] };
        if (!query.trim()) return of(empty);
        return timer(200).pipe(switchMap(() =>
          (this.target === 'variantId'
            ? this.service.searchVariants(query.trim()).pipe(map(variants => ({ ...empty, variants })))
            : this.service.searchParents(query.trim()).pipe(map(products => ({ ...empty, products })))).pipe(
            catchError(error => { this.error = priceError(error); return of(empty); }))));
      }),
      takeUntilDestroyed()
    ).subscribe(result => { this.products = result.products; this.variants = result.variants; });
  }
  ngOnChanges(changes: SimpleChanges): void {
    if (!changes['target']) return;
    this.products = [];
    this.variants = [];
    this.search = '';
    this.queries.next('');
  }
  chooseProduct(product: ProductParentDto): void {
    this.products = []; this.search = product.name; this.error = '';
    this.selected.emit(product.productId);
  }
  chooseVariant(variant: VariantSearchDto): void {
    this.variants = [];
    this.search = `${variant.productName} · ${variant.barcode} · ${variant.colorName || 'N/A'} · ${variant.sizeName || 'N/A'}`;
    this.error = '';
    this.selected.emit(variant.variantId);
  }
}
