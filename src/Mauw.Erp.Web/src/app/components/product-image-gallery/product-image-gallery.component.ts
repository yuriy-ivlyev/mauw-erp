import { Component, EventEmitter, Input, OnChanges, Output, SimpleChanges } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { from } from 'rxjs';
import { concatMap } from 'rxjs/operators';
import { ConfirmDeleteModalComponent } from '../../shared/confirm-delete-modal/confirm-delete-modal.component';
import { ImageService } from '../../services/image.service';
import { ProductService } from '../../services/product.service';
import { ImageCategory, ImageDto, ProductVariantDto } from '../../models/product.model';

const ALLOWED_TYPES = ['image/jpeg', 'image/png', 'image/webp'];
const MAX_FILE_BYTES = 10 * 1024 * 1024;

export const IMAGE_CATEGORY_LABELS: Record<ImageCategory, string> = {
  Main: 'Principale',
  Left: 'Sinistra',
  Right: 'Destra',
  Zoom1: 'Zoom 1',
  Zoom2: 'Zoom 2',
  Zoom3: 'Zoom 3',
  OnPerson: 'Indossato',
  OnManikin: 'Su manichino'
};

/**
 * Galleria immagini di un prodotto: le immagini appartengono al prodotto se non ha varianti,
 * altrimenti a ciascuna variante (selezionabile).
 */
@Component({
  selector: 'app-product-image-gallery',
  standalone: true,
  imports: [CommonModule, FormsModule, ConfirmDeleteModalComponent],
  template: `
    <div class="space-y-4">
      <div *ngIf="hasVariants" class="flex items-center gap-3">
        <label for="gallery-variant" class="text-sm font-medium whitespace-nowrap">Variante</label>
        <select id="gallery-variant" [ngModel]="targetId" (ngModelChange)="selectTarget($event)"
          [disabled]="busy || variants.length === 0"
          class="flex-1 px-3 py-2 border rounded text-sm focus:outline-none focus:ring-2 focus:ring-blue-500">
          <option *ngFor="let v of variants" [ngValue]="v.id">{{ variantLabel(v) }}</option>
        </select>
        <label *ngIf="targetId" class="flex items-center gap-1.5 text-sm whitespace-nowrap"
          title="Le immagini di questa variante rappresentano il prodotto nella lista">
          <input type="checkbox" [checked]="isVetrina" [disabled]="busy" (change)="toggleVetrina($event)" />
          Variante vetrina
        </label>
      </div>

      <div *ngIf="hasVariants && variants.length === 0"
        class="bg-yellow-50 text-yellow-800 p-3 rounded text-sm">
        Aggiungi almeno una variante per poter caricare le immagini: le immagini di un prodotto con varianti
        appartengono alle singole varianti.
      </div>

      <ng-container *ngIf="targetId">
        <div class="flex h-96 overflow-hidden gap-4">
          <div class="flex-1 bg-gray-900 flex items-center justify-center p-4 rounded border-2 border-gray-200">
            <img *ngIf="selected; else noSelected" [src]="selected.url" alt="Immagine selezionata"
              class="max-w-full max-h-full object-contain rounded" />
            <ng-template #noSelected>
              <div class="text-gray-500 text-center">
                <div class="text-6xl mb-2">&#128444;</div>
                <p class="text-sm">{{ loading ? 'Caricamento...' : 'Nessuna immagine' }}</p>
              </div>
            </ng-template>
          </div>

          <div class="w-48 flex flex-col border rounded">
            <div class="flex-1 overflow-y-auto p-2 space-y-2 border-b">
              <div *ngFor="let img of images" (click)="selected = img"
                class="relative rounded overflow-hidden cursor-pointer border-2 transition"
                [class.border-blue-500]="selected?.id === img.id"
                [class.border-transparent]="selected?.id !== img.id">
                <img [src]="img.url" [alt]="categoryLabel(img.category)" class="w-full h-24 object-cover" />
                <div class="absolute bottom-0 inset-x-0 bg-black/60 text-white text-xs px-1.5 py-0.5">
                  <span *ngIf="img.category === 'Main'">&#9733; </span>{{ categoryLabel(img.category) }}
                </div>
              </div>
              <div *ngIf="!loading && images.length === 0" class="text-gray-400 text-xs text-center py-4">
                Nessuna immagine
              </div>
            </div>
            <div class="p-2 flex-shrink-0 space-y-1">
              <ng-container *ngIf="selected">
                <label for="gallery-image-category" class="block text-xs text-gray-600">Categoria immagine</label>
                <select id="gallery-image-category" [ngModel]="selected.category" [disabled]="busy"
                  (ngModelChange)="changeCategory($event)"
                  class="w-full px-2 py-1 border rounded text-xs focus:outline-none focus:ring-2 focus:ring-blue-500">
                  <option *ngFor="let c of allCategories" [value]="c">{{ categoryLabel(c) }}</option>
                </select>
                <button (click)="pendingDelete = selected" [disabled]="busy"
                  class="w-full text-xs bg-red-600 hover:bg-red-700 text-white font-semibold py-1.5 rounded disabled:opacity-50">
                  &#128465; Elimina
                </button>
              </ng-container>
            </div>
          </div>
        </div>

        <div class="border-t pt-4 flex flex-wrap gap-3 items-center">
          <label for="gallery-category" class="text-sm font-medium">Categoria</label>
          <select id="gallery-category" [(ngModel)]="category" [disabled]="busy"
            class="px-3 py-2 border rounded text-sm focus:outline-none focus:ring-2 focus:ring-blue-500">
            <option value="">Automatica</option>
            <option *ngFor="let c of uploadCategories" [value]="c">{{ categoryLabel(c) }}</option>
          </select>
          <label
            class="bg-blue-600 hover:bg-blue-700 text-white text-sm px-4 py-2 rounded cursor-pointer"
            [class.opacity-50]="busy" [class.pointer-events-none]="busy">
            {{ uploading ? 'Caricamento...' : '+ Carica immagini' }}
            <input type="file" class="hidden" multiple accept="image/jpeg,image/png,image/webp"
              [disabled]="busy" (change)="onFilesSelected($event)" />
          </label>
          <span class="text-xs text-gray-500">
            JPEG, PNG o WebP, max 10 MB. Automatica: la prima è Principale. Per cambiare la Principale
            usa la categoria dell'immagine.
          </span>
        </div>
      </ng-container>

      <div *ngIf="error" class="bg-red-100 text-red-800 p-2 rounded text-sm">{{ error }}</div>
    </div>

    <app-confirm-delete-modal
      [isOpen]="!!pendingDelete"
      message="Sei sicuro di voler eliminare questa immagine? L'operazione non è reversibile."
      (confirmed)="confirmDelete()"
      (cancelled)="pendingDelete = null">
    </app-confirm-delete-modal>
  `
})
export class ProductImageGalleryComponent implements OnChanges {
  @Input({ required: true }) productId!: string;
  @Input() hasVariants = false;
  @Input() variants: ProductVariantDto[] = [];
  /** Emesso dopo ogni modifica che puo' cambiare la miniatura del prodotto in lista. */
  @Output() changed = new EventEmitter<void>();

  readonly allCategories = Object.keys(IMAGE_CATEGORY_LABELS) as ImageCategory[];
  /** Main si assegna solo cambiando categoria (scambio con l'attuale Principale). */
  readonly uploadCategories: ImageCategory[] = this.allCategories.filter(c => c !== 'Main');

  targetId: string | null = null;
  images: ImageDto[] = [];
  selected: ImageDto | null = null;
  category: ImageCategory | '' = '';
  loading = false;
  uploading = false;
  updating = false;
  error: string | null = null;
  pendingDelete: ImageDto | null = null;

  constructor(private imageService: ImageService, private productService: ProductService) { }

  get busy(): boolean {
    return this.uploading || this.updating;
  }

  get isVetrina(): boolean {
    return !!this.variants.find(v => v.id === this.targetId)?.varianteVetrina;
  }

  ngOnChanges(changes: SimpleChanges): void {
    if (!changes['productId'] && !changes['hasVariants'] && !changes['variants']) return;
    const next = this.hasVariants
      ? (this.variants.find(v => v.id === this.targetId)?.id ?? this.variants[0]?.id ?? null)
      : this.productId;
    if (next !== this.targetId) this.selectTarget(next);
  }

  variantLabel(v: ProductVariantDto): string {
    const parts = [v.color, v.size].filter(p => !!p && p !== 'N/A');
    return `${v.varianteVetrina ? '★ ' : ''}${parts.length ? parts.join(' / ') : 'Variante'} · ${v.barcode}`;
  }

  categoryLabel(category: ImageCategory): string {
    return IMAGE_CATEGORY_LABELS[category] ?? category;
  }

  selectTarget(targetId: string | null): void {
    this.targetId = targetId;
    this.images = [];
    this.selected = null;
    this.error = null;
    this.load(targetId, null);
  }

  private load(targetId: string | null, keepSelectedId: string | null): void {
    if (!targetId) return;

    this.loading = true;
    this.imageService.list(targetId).subscribe({
      next: images => {
        if (this.targetId !== targetId) return;
        this.images = images;
        this.selected = images.find(i => i.id === keepSelectedId)
          ?? images.find(i => i.category === 'Main')
          ?? images[0]
          ?? null;
        this.loading = false;
      },
      error: err => {
        if (this.targetId !== targetId) return;
        this.error = this.messageFrom(err, 'Errore nel caricamento delle immagini');
        this.loading = false;
      }
    });
  }

  changeCategory(category: ImageCategory): void {
    const image = this.selected;
    if (!image || image.category === category) return;

    this.error = null;
    this.updating = true;
    this.imageService.setCategory(image.targetId, image.id, category).subscribe({
      next: () => {
        this.updating = false;
        // Impostare Main cambia anche la categoria di un'altra immagine: si ricarica l'elenco.
        this.load(this.targetId, image.id);
        this.changed.emit();
      },
      error: err => {
        this.updating = false;
        this.error = this.messageFrom(err, 'Errore nel cambio di categoria');
        this.load(this.targetId, image.id);
      }
    });
  }

  toggleVetrina(event: Event): void {
    const variantId = this.targetId;
    if (!variantId) return;

    const value = (event.target as HTMLInputElement).checked;
    this.error = null;
    this.updating = true;
    this.productService.setVarianteVetrina(this.productId, variantId, value).subscribe({
      next: () => {
        this.updating = false;
        this.changed.emit();
      },
      error: err => {
        this.updating = false;
        (event.target as HTMLInputElement).checked = !value;
        this.error = this.messageFrom(err, 'Errore nell\'impostazione della variante vetrina');
      }
    });
  }

  onFilesSelected(event: Event): void {
    const input = event.target as HTMLInputElement;
    const files = Array.from(input.files ?? []);
    input.value = '';
    const targetId = this.targetId;
    if (!targetId || files.length === 0) return;

    const invalid = files.find(f => !ALLOWED_TYPES.includes(f.type) || f.size > MAX_FILE_BYTES);
    if (invalid) {
      this.error = `"${invalid.name}": sono ammessi solo JPEG, PNG o WebP fino a 10 MB.`;
      return;
    }

    this.error = null;
    this.uploading = true;
    const category = this.category || undefined;
    from(files).pipe(concatMap(file => this.imageService.upload(targetId, file, category))).subscribe({
      next: image => {
        if (this.targetId !== targetId) return;
        this.images = [...this.images, image];
        this.selected = image;
      },
      error: err => {
        this.error = this.messageFrom(err, "Errore nel caricamento dell'immagine");
        this.uploading = false;
        this.changed.emit();
      },
      complete: () => {
        this.uploading = false;
        this.changed.emit();
      }
    });
  }

  confirmDelete(): void {
    const image = this.pendingDelete;
    this.pendingDelete = null;
    if (!image) return;

    this.imageService.remove(image.targetId, image.id).subscribe({
      next: () => {
        if (this.targetId !== image.targetId) return;
        this.images = this.images.filter(i => i.id !== image.id);
        if (this.selected?.id === image.id) this.selected = this.images[0] ?? null;
        this.changed.emit();
      },
      error: err => this.error = this.messageFrom(err, "Errore nell'eliminazione dell'immagine")
    });
  }

  private messageFrom(err: any, fallback: string): string {
    const detail = err?.error?.error;
    return typeof detail === 'string' ? `${fallback}: ${detail}` : fallback;
  }
}
