import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ProductService } from '../../../services/product.service';
import {
  ProductDto, ProductImageDto, PageResponse,
  CreateProductRequest, CreateProductVariantRequest, AddProductImageRequest
} from '../../../models/product.model';

@Component({
  selector: 'app-prodotti',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <div class="p-6">
      <div class="flex justify-between items-center mb-6">
        <h1 class="text-3xl font-bold">Prodotti</h1>
        <button (click)="openNewProductModal()"
          class="bg-blue-600 hover:bg-blue-700 text-white px-4 py-2 rounded">
          + Nuovo Prodotto
        </button>
      </div>

      <div class="mb-6">
        <input [(ngModel)]="searchTerm" (keyup.enter)="search()" type="text"
          placeholder="Cerca per nome o codice interno..."
          class="w-full px-4 py-2 border rounded-lg focus:outline-none focus:ring-2 focus:ring-blue-500" />
      </div>

      <div class="bg-white rounded-lg shadow overflow-hidden">
        <table class="w-full">
          <thead class="bg-gray-100 border-b">
            <tr>
              <th class="px-4 py-3 text-left text-sm font-semibold w-20">Img</th>
              <th class="px-6 py-3 text-left text-sm font-semibold">Codice</th>
              <th class="px-6 py-3 text-left text-sm font-semibold">Nome Prodotto</th>
              <th class="px-6 py-3 text-left text-sm font-semibold">Fornitore Codice</th>
              <th class="px-6 py-3 text-left text-sm font-semibold">Varianti</th>
              <th class="px-6 py-3 text-left text-sm font-semibold">Prezzo Acquisto</th>
              <th class="px-6 py-3 text-left text-sm font-semibold">Azioni</th>
            </tr>
          </thead>
          <tbody>
            <tr *ngFor="let product of products" class="border-b hover:bg-gray-50">

              <!-- Cella immagine cliccabile -->
              <td class="px-4 py-3">
                <div (click)="openGalleryModal(product)"
                  class="w-14 h-14 rounded border border-gray-200 overflow-hidden cursor-pointer
                         hover:ring-2 hover:ring-blue-400 transition flex items-center justify-center
                         bg-gray-50 flex-shrink-0">
                  <img *ngIf="product.mainImageUrl; else noImg"
                    [src]="product.mainImageUrl" [alt]="product.name"
                    class="w-full h-full object-cover"
                    (error)="onThumbError($event, product)" />
                  <ng-template #noImg>
                    <span class="text-gray-400 text-xs text-center leading-tight px-1 select-none">no image</span>
                  </ng-template>
                </div>
              </td>

              <td class="px-6 py-3 text-sm">{{ product.internalCode }}</td>
              <td class="px-6 py-3 text-sm">{{ product.name }}</td>
              <td class="px-6 py-3 text-sm">{{ product.supplierCode }}</td>
              <td class="px-6 py-3 text-sm">
                <span class="bg-blue-100 text-blue-800 px-2 py-1 rounded">{{ product.variants.length }}</span>
              </td>
              <td class="px-6 py-3 text-sm">
                <span *ngIf="product.variants.length > 0">
                  &euro;{{ product.variants[0].purchaseCost.toFixed(2) }}
                </span>
              </td>
              <td class="px-6 py-3 text-sm space-x-2">
                <button (click)="editProduct(product)" class="text-blue-600 hover:text-blue-800 underline">Modifica</button>
                <button (click)="deleteProduct(product.id)" class="text-red-600 hover:text-red-800 underline">Elimina</button>
              </td>
            </tr>
            <tr *ngIf="products.length === 0">
              <td colspan="7" class="px-6 py-8 text-center text-gray-500">Nessun prodotto trovato</td>
            </tr>
          </tbody>
        </table>
      </div>

      <div class="mt-6 flex justify-between items-center">
        <div class="text-sm text-gray-600">
          Pagina {{ currentPage }} di {{ totalPages }} ({{ totalCount }} prodotti totali)
        </div>
        <div class="flex gap-2">
          <button [disabled]="currentPage === 1" (click)="previousPage()"
            class="px-4 py-2 border rounded hover:bg-gray-50 disabled:opacity-50">Precedente</button>
          <button [disabled]="currentPage === totalPages" (click)="nextPage()"
            class="px-4 py-2 border rounded hover:bg-gray-50 disabled:opacity-50">Successiva</button>
        </div>
      </div>

      <div *ngIf="loading" class="text-center text-gray-500 mt-4">Caricamento...</div>
      <div *ngIf="error" class="bg-red-100 text-red-800 p-4 rounded mt-4">{{ error }}</div>

      <!-- ===== Modal Crea/Modifica Prodotto ===== -->
      <div *ngIf="showModal" class="fixed inset-0 bg-black bg-opacity-50 flex items-center justify-center z-50">
        <div class="bg-white rounded-lg shadow-lg max-w-2xl w-full mx-4 max-h-[90vh] overflow-y-auto">
          <div class="sticky top-0 bg-white border-b p-6 flex justify-between items-center">
            <h2 class="text-2xl font-bold">{{ editingProductId ? 'Modifica Prodotto' : 'Nuovo Prodotto' }}</h2>
            <button (click)="closeModal()" class="text-gray-500 hover:text-gray-700 text-2xl">x</button>
          </div>
          <div class="p-6 space-y-4">
            <div class="grid grid-cols-2 gap-4">
              <div>
                <label class="block text-sm font-medium mb-1">Codice Interno *</label>
                <input [(ngModel)]="formData.internalCode" type="text"
                  class="w-full px-3 py-2 border rounded focus:outline-none focus:ring-2 focus:ring-blue-500" />
              </div>
              <div>
                <label class="block text-sm font-medium mb-1">Nome Prodotto *</label>
                <input [(ngModel)]="formData.name" type="text"
                  class="w-full px-3 py-2 border rounded focus:outline-none focus:ring-2 focus:ring-blue-500" />
              </div>
              <div>
                <label class="block text-sm font-medium mb-1">Codice Fornitore</label>
                <input [(ngModel)]="formData.supplierCode" type="text"
                  class="w-full px-3 py-2 border rounded focus:outline-none focus:ring-2 focus:ring-blue-500" />
              </div>
              <div>
                <label class="block text-sm font-medium mb-1">Markup Rate (%)</label>
                <input [(ngModel)]="formData.defaultMarkupRate" type="number" step="0.01"
                  class="w-full px-3 py-2 border rounded focus:outline-none focus:ring-2 focus:ring-blue-500" />
              </div>
              <div>
                <label class="block text-sm font-medium mb-1">VAT Rate (%)</label>
                <input [(ngModel)]="formData.vatRate" type="number" step="0.01"
                  class="w-full px-3 py-2 border rounded focus:outline-none focus:ring-2 focus:ring-blue-500" />
              </div>
            </div>

            <div class="border-t pt-6">
              <div class="flex justify-between items-center mb-4">
                <h3 class="text-lg font-semibold">Varianti</h3>
                <button (click)="addVariant()"
                  class="bg-green-600 hover:bg-green-700 text-white px-3 py-1 rounded text-sm">
                  + Aggiungi Variante
                </button>
              </div>
              <div *ngFor="let variant of formData.variants; let i = index" class="border rounded p-4 mb-4">
                <div class="flex justify-between items-center mb-3">
                  <h4 class="font-medium">Variante {{ i + 1 }}</h4>
                  <button (click)="removeVariant(i)" class="text-red-600 hover:text-red-800 text-sm">Rimuovi</button>
                </div>
                <div class="grid grid-cols-2 gap-3">
                  <div>
                    <label class="block text-sm font-medium mb-1">Taglia</label>
                    <input [(ngModel)]="variant.size" type="text" placeholder="es. M, L, XL"
                      class="w-full px-3 py-2 border rounded focus:outline-none focus:ring-2 focus:ring-blue-500" />
                  </div>
                  <div>
                    <label class="block text-sm font-medium mb-1">Colore</label>
                    <input [(ngModel)]="variant.color" type="text" placeholder="es. Rosso, Blu"
                      class="w-full px-3 py-2 border rounded focus:outline-none focus:ring-2 focus:ring-blue-500" />
                  </div>
                  <div>
                    <label class="block text-sm font-medium mb-1">Barcode</label>
                    <input [(ngModel)]="variant.barcode" type="text"
                      class="w-full px-3 py-2 border rounded focus:outline-none focus:ring-2 focus:ring-blue-500" />
                  </div>
                  <div>
                    <label class="block text-sm font-medium mb-1">Costo Acquisto</label>
                    <input [(ngModel)]="variant.purchaseCost" type="number" step="0.01"
                      class="w-full px-3 py-2 border rounded focus:outline-none focus:ring-2 focus:ring-blue-500" />
                  </div>
                </div>
                <div class="mt-3">
                  <label class="block text-sm font-medium mb-1">Quantita Stock</label>
                  <input [(ngModel)]="variant.stockQuantity" type="number"
                    class="w-full px-3 py-2 border rounded focus:outline-none focus:ring-2 focus:ring-blue-500" />
                </div>
              </div>
            </div>

            <div class="flex gap-3 justify-end border-t pt-6">
              <button (click)="closeModal()" class="px-4 py-2 border rounded hover:bg-gray-50">Annulla</button>
              <button (click)="saveProduct()" [disabled]="savingProduct"
                class="bg-blue-600 hover:bg-blue-700 text-white px-4 py-2 rounded disabled:opacity-50">
                {{ savingProduct ? 'Salvataggio...' : 'Salva' }}
              </button>
            </div>
          </div>
        </div>
      </div>

      <!-- ===== Modal Galleria Immagini ===== -->
      <div *ngIf="showGalleryModal && galleryProduct"
        class="fixed inset-0 bg-black bg-opacity-60 flex items-center justify-center z-50 p-4">
        <div class="bg-white rounded-xl shadow-2xl w-full max-w-4xl max-h-[90vh] flex flex-col">

          <!-- Header -->
          <div class="flex justify-between items-center px-6 py-4 border-b flex-shrink-0">
            <h2 class="text-xl font-bold truncate">{{ galleryProduct.name }}</h2>
            <button (click)="closeGalleryModal()" class="text-gray-400 hover:text-gray-700 text-3xl leading-none">&times;</button>
          </div>

          <!-- Body: immagine grande + sidebar thumbnail -->
          <div class="flex flex-1 min-h-0 overflow-hidden">

            <!-- Immagine grande -->
            <div class="flex-1 bg-gray-900 flex items-center justify-center p-4 min-w-0">
              <ng-container *ngIf="selectedImage; else noSelectedImg">
                <img [src]="selectedImage.url" alt="Immagine selezionata"
                  class="max-w-full max-h-full object-contain rounded"
                  (error)="onImgError($event)" />
              </ng-container>
              <ng-template #noSelectedImg>
                <div class="text-gray-500 text-center">
                  <div class="text-6xl mb-2">&#128444;</div>
                  <p class="text-sm">Nessuna immagine</p>
                </div>
              </ng-template>
            </div>

            <!-- Sidebar destra -->
            <div class="w-52 flex flex-col border-l flex-shrink-0">

              <!-- Thumbnails scrollabili -->
              <div class="flex-1 overflow-y-auto p-2 space-y-2">
                <div *ngFor="let img of galleryProduct.images"
                  (click)="selectImage(img)"
                  class="relative rounded overflow-hidden cursor-pointer border-2 transition"
                  [class.border-blue-500]="selectedImage?.id === img.id"
                  [class.border-transparent]="selectedImage?.id !== img.id">
                  <img [src]="img.url" [alt]="img.url"
                    class="w-full h-24 object-cover"
                    (error)="onImgError($event)" />
                  <div *ngIf="img.isMain"
                    class="absolute top-1 left-1 bg-yellow-400 text-xs font-bold px-1.5 py-0.5 rounded leading-none">
                    &#9733;
                  </div>
                </div>
                <div *ngIf="!galleryProduct.images.length"
                  class="text-gray-400 text-xs text-center py-4">
                  Nessuna immagine
                </div>
              </div>

              <!-- Azioni immagine selezionata -->
              <div class="border-t p-2 space-y-1 flex-shrink-0">
                <button *ngIf="selectedImage && !selectedImage.isMain"
                  (click)="setMainImage(selectedImage)"
                  class="w-full text-xs bg-yellow-400 hover:bg-yellow-500 font-semibold py-1.5 rounded">
                  &#9733; Imposta principale
                </button>
                <button *ngIf="selectedImage"
                  (click)="removeImage(selectedImage)"
                  class="w-full text-xs bg-red-600 hover:bg-red-700 text-white font-semibold py-1.5 rounded">
                  &#128465; Elimina
                </button>
              </div>

            </div>
          </div>

          <!-- Footer: aggiungi immagine -->
          <div class="border-t px-6 py-4 flex-shrink-0 space-y-2">
            <div class="flex gap-2 items-center">
              <input [(ngModel)]="newImageUrl" type="text" placeholder="URL nuova immagine..."
                class="flex-1 px-3 py-2 border rounded text-sm focus:outline-none focus:ring-2 focus:ring-blue-500" />
              <label class="flex items-center gap-1 text-sm whitespace-nowrap">
                <input type="checkbox" [(ngModel)]="newImageIsMain" /> Principale
              </label>
              <button (click)="addImage()" [disabled]="!newImageUrl || addingImage"
                class="bg-blue-600 hover:bg-blue-700 text-white text-sm px-4 py-2 rounded disabled:opacity-50 whitespace-nowrap">
                {{ addingImage ? '...' : '+ Aggiungi' }}
              </button>
            </div>
            <div *ngIf="galleryError" class="bg-red-100 text-red-800 p-2 rounded text-sm">{{ galleryError }}</div>
          </div>
        </div>
      </div>

    </div>
  `
})
export class ProdottiComponent implements OnInit {
  products: ProductDto[] = [];
  currentPage = 1;
  pageSize = 20;
  totalCount = 0;
  totalPages = 0;
  loading = false;
  error: string | null = null;
  searchTerm = '';

  showModal = false;
  editingProductId: string | null = null;
  savingProduct = false;
  formData: CreateProductRequest = this.initializeForm();

  showGalleryModal = false;
  galleryProduct: ProductDto | null = null;
  selectedImage: ProductImageDto | null = null;
  newImageUrl = '';
  newImageIsMain = false;
  addingImage = false;
  galleryError: string | null = null;

  constructor(private productService: ProductService) {}

  ngOnInit(): void {
    this.loadProducts();
  }

  private initializeForm(): CreateProductRequest {
    return {
      supplierId: '',
      supplierCode: '',
      internalCode: '',
      name: '',
      defaultMarkupRate: 0,
      vatRate: 0,
      variants: []
    };
  }

  loadProducts(): void {
    this.loading = true;
    this.error = null;
    this.productService.getProducts(this.currentPage, this.pageSize, this.searchTerm || undefined).subscribe({
      next: (response: PageResponse<ProductDto>) => {
        this.products = response.items;
        this.totalCount = response.totalCount;
        this.totalPages = response.totalPages;
        this.loading = false;
      },
      error: (err) => {
        this.error = 'Errore nel caricamento dei prodotti';
        this.loading = false;
        console.error(err);
      }
    });
  }

  search(): void { this.currentPage = 1; this.loadProducts(); }

  nextPage(): void {
    if (this.currentPage < this.totalPages) { this.currentPage++; this.loadProducts(); }
  }

  previousPage(): void {
    if (this.currentPage > 1) { this.currentPage--; this.loadProducts(); }
  }

  openNewProductModal(): void {
    this.editingProductId = null;
    this.formData = this.initializeForm();
    this.formData.variants = [this.createEmptyVariant()];
    this.showModal = true;
  }

  editProduct(product: ProductDto): void {
    this.editingProductId = product.id;
    this.formData = {
      supplierId: product.supplierId,
      supplierCode: product.supplierCode,
      internalCode: product.internalCode,
      name: product.name,
      defaultMarkupRate: product.defaultMarkupRate,
      vatRate: product.vatRate,
      variants: product.variants.map(v => ({
        size: v.size,
        color: v.color,
        barcode: v.barcode,
        purchaseCost: v.purchaseCost,
        stockQuantity: v.stockQuantity
      }))
    };
    this.showModal = true;
  }

  closeModal(): void {
    this.showModal = false;
    this.editingProductId = null;
    this.formData = this.initializeForm();
  }

  addVariant(): void { this.formData.variants.push(this.createEmptyVariant()); }
  removeVariant(index: number): void { this.formData.variants.splice(index, 1); }

  private createEmptyVariant(): CreateProductVariantRequest {
    return { size: '', color: '', barcode: '', purchaseCost: 0, stockQuantity: 0 };
  }

  saveProduct(): void {
    if (!this.formData.name || !this.formData.internalCode || this.formData.variants.length === 0) {
      this.error = 'Completa i campi obbligatori e aggiungi almeno una variante';
      return;
    }
    if (!this.formData.supplierId?.trim()) {
      this.formData.supplierId = this.generateUUID();
    }
    this.savingProduct = true;
    this.error = null;

    const operation = this.editingProductId
      ? this.productService.updateProduct(this.editingProductId, this.formData)
      : this.productService.createProduct(this.formData);

    operation.subscribe({
      next: () => {
        this.savingProduct = false;
        this.closeModal();
        this.currentPage = 1;
        this.loadProducts();
      },
      error: (err) => {
        this.error = err?.error && typeof err.error === 'string'
          ? err.error
          : (this.editingProductId ? 'Errore nella modifica del prodotto' : 'Errore nella creazione del prodotto');
        this.savingProduct = false;
        console.error(err);
      }
    });
  }

  deleteProduct(productId: string): void {
    if (!confirm('Sei sicuro di voler eliminare questo prodotto?')) return;
    this.loading = true;
    this.productService.deleteProduct(productId).subscribe({
      next: () => { this.loading = false; this.loadProducts(); },
      error: (err) => {
        this.error = "Errore nell'eliminazione del prodotto";
        this.loading = false;
        console.error(err);
      }
    });
  }

  openGalleryModal(product: ProductDto): void {
    this.galleryProduct = { ...product, images: [...(product.images ?? [])] };
    this.selectedImage = this.galleryProduct.images.find(i => i.isMain) ?? this.galleryProduct.images[0] ?? null;
    this.newImageUrl = '';
    this.newImageIsMain = false;
    this.galleryError = null;
    this.showGalleryModal = true;
  }

  closeGalleryModal(): void {
    this.showGalleryModal = false;
    this.galleryProduct = null;
    this.selectedImage = null;
    this.loadProducts();
  }

  selectImage(img: ProductImageDto): void {
    this.selectedImage = img;
  }

  addImage(): void {
    if (!this.galleryProduct || !this.newImageUrl) return;
    const request: AddProductImageRequest = { url: this.newImageUrl, isMain: this.newImageIsMain };
    this.addingImage = true;
    this.galleryError = null;
    this.productService.addImage(this.galleryProduct.id, request).subscribe({
      next: (img: ProductImageDto) => {
        if (request.isMain) {
          this.galleryProduct!.images = this.galleryProduct!.images.map(i => ({ ...i, isMain: false }));
        }
        this.galleryProduct!.images.push(img);
        this.selectedImage = img;
        this.newImageUrl = '';
        this.newImageIsMain = false;
        this.addingImage = false;
      },
      error: (err: any) => {
        this.galleryError = "Errore nell'aggiunta dell'immagine";
        this.addingImage = false;
        console.error(err);
      }
    });
  }

  removeImage(img: ProductImageDto): void {
    if (!this.galleryProduct) return;
    this.productService.removeImage(this.galleryProduct.id, img.id).subscribe({
      next: () => {
        this.galleryProduct!.images = this.galleryProduct!.images.filter(i => i.id !== img.id);
        if (img.isMain && this.galleryProduct!.images.length > 0) {
          this.galleryProduct!.images[0] = { ...this.galleryProduct!.images[0], isMain: true };
        }
        this.selectedImage = this.galleryProduct!.images.find(i => i.isMain) ?? this.galleryProduct!.images[0] ?? null;
      },
      error: (err: any) => {
        this.galleryError = "Errore nella rimozione dell'immagine";
        console.error(err);
      }
    });
  }

  setMainImage(img: ProductImageDto): void {
    if (!this.galleryProduct) return;
    this.productService.setMainImage(this.galleryProduct.id, img.id).subscribe({
      next: () => {
        this.galleryProduct!.images = this.galleryProduct!.images.map(i => ({ ...i, isMain: i.id === img.id }));
        this.selectedImage = { ...img, isMain: true };
      },
      error: (err: any) => {
        this.galleryError = "Errore nell'impostazione dell'immagine principale";
        console.error(err);
      }
    });
  }

  onThumbError(event: Event, product: ProductDto): void {
    // Prevent further error loops by clearing the URL on the model
    product.mainImageUrl = undefined;
    (event.target as HTMLImageElement).style.display = 'none';
  }

  onImgError(event: Event): void {
    (event.target as HTMLImageElement).style.display = 'none';
  }

  private generateUUID(): string {
    return 'xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx'.replace(/[xy]/g, function(c) {
      const r = Math.random() * 16 | 0;
      const v = c === 'x' ? r : (r & 0x3 | 0x8);
      return v.toString(16);
    });
  }
}
