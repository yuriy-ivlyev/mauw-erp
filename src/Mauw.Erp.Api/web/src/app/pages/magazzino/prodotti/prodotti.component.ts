import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ConfirmDeleteModalComponent } from '../../../shared/confirm-delete-modal/confirm-delete-modal.component';
import { FormsModule } from '@angular/forms';
import { ProductService } from '../../../services/product.service';
import {
  ProductDto, ProductImageDto, PageResponse, ColorDto, SizeDto, CategoryDto, CollectionDto, MaterialDto,
  CreateProductRequest, CreateProductVariantRequest, AddProductImageRequest, ProductVariantDto
} from '../../../models/product.model';

@Component({
  selector: 'app-prodotti',
  standalone: true,
  imports: [CommonModule, FormsModule, ConfirmDeleteModalComponent],
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
                    <span class="text-gray-400 text-xs text-center leading-tight px-1 select-none">nessuna immagine</span>
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
      <div *ngIf="showModal" class="fixed inset-0 bg-black bg-opacity-50 flex items-center justify-center z-50 p-4">
        <div class="bg-white rounded-lg shadow-lg max-w-4xl w-full max-h-[90vh] flex flex-col">
          <div class="sticky top-0 bg-white border-b p-6 flex justify-between items-center flex-shrink-0">
            <h2 class="text-2xl font-bold">{{ editingProductId ? 'Modifica Prodotto' : 'Nuovo Prodotto' }}</h2>
            <button (click)="closeModal()" class="text-gray-500 hover:text-gray-700 text-2xl">x</button>
          </div>

          <!-- Tab Navigation -->
          <div class="border-b flex flex-shrink-0">
            <button
              (click)="activeTab = 'details'"
              [class]="'flex-1 px-4 py-3 text-center font-medium border-b-2 transition ' +
                (activeTab === 'details' ? 'border-blue-600 text-blue-600' : 'border-transparent text-gray-600 hover:text-gray-800')">
              Dettagli Prodotto
            </button>
            <button
              *ngIf="editingProductId"
              (click)="activeTab = 'variants'"
              [class]="'flex-1 px-4 py-3 text-center font-medium border-b-2 transition ' +
                (activeTab === 'variants' ? 'border-blue-600 text-blue-600' : 'border-transparent text-gray-600 hover:text-gray-800')">
              Varianti
            </button>
            <button
              *ngIf="editingProductId"
              (click)="activeTab = 'gallery'"
              [class]="'flex-1 px-4 py-3 text-center font-medium border-b-2 transition ' +
                (activeTab === 'gallery' ? 'border-blue-600 text-blue-600' : 'border-transparent text-gray-600 hover:text-gray-800')">
              Galleria
            </button>
          </div>

          <div class="flex-1 overflow-y-auto p-6">
            <!-- TAB: Details -->
            <div *ngIf="activeTab === 'details'" class="space-y-4">
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
                  <label class="block text-sm font-medium mb-1">Prezzo Base Fornitore *</label>
                  <input [(ngModel)]="formData.basePurchaseCost" type="number" step="0.01" min="0"
                    class="w-full px-3 py-2 border rounded focus:outline-none focus:ring-2 focus:ring-blue-500" />
                </div>
                <div>
                  <label class="block text-sm font-medium mb-1">VAT Rate (%)</label>
                  <input [(ngModel)]="formData.vatRate" type="number" step="0.01"
                    class="w-full px-3 py-2 border rounded focus:outline-none focus:ring-2 focus:ring-blue-500" />
                </div>
              </div>

              <div class="grid grid-cols-2 gap-4 pt-4">
                <div>
                  <label class="block text-sm font-medium mb-1">Categoria</label>
                  <select [(ngModel)]="formData.categoryId"
                    class="w-full px-3 py-2 border rounded focus:outline-none focus:ring-2 focus:ring-blue-500">
                    <option value="">-- Seleziona categoria --</option>
                    <option *ngFor="let cat of categories" [value]="cat.id">{{ cat.name }}</option>
                  </select>
                </div>
                <div>
                  <label class="block text-sm font-medium mb-1">Collezione</label>
                  <select [(ngModel)]="formData.collectionId"
                    class="w-full px-3 py-2 border rounded focus:outline-none focus:ring-2 focus:ring-blue-500">
                    <option value="">-- Seleziona collezione --</option>
                    <option *ngFor="let col of collections" [value]="col.id">{{ col.name }}</option>
                  </select>
                </div>
                <div class="col-span-2">
                  <label class="block text-sm font-medium mb-1">Materiale</label>
                  <select [(ngModel)]="formData.materialId"
                    class="w-full px-3 py-2 border rounded focus:outline-none focus:ring-2 focus:ring-blue-500">
                    <option value="">-- Seleziona materiale --</option>
                    <option *ngFor="let mat of materials" [value]="mat.id">{{ mat.name }}</option>
                  </select>
                </div>
              </div>

              <div *ngIf="formError" class="bg-red-100 text-red-800 p-3 rounded text-sm">{{ formError }}</div>
            </div>

            <!-- TAB: Variants -->
            <div *ngIf="activeTab === 'variants'" class="space-y-6">
              <!-- Variant Table -->
              <div>
                <h3 class="text-lg font-semibold mb-4">Varianti Esistenti</h3>
                <div *ngIf="loadingVariants" class="text-center text-gray-500 py-4">Caricamento...</div>
                <div *ngIf="!loadingVariants && productVariants.length === 0" class="text-center text-gray-500 py-4">
                  Nessuna variante. Aggiungi la prima variante qui sotto.
                </div>
                <div *ngIf="!loadingVariants && productVariants.length > 0" class="bg-white rounded border overflow-hidden">
                  <table class="w-full text-sm">
                    <thead class="bg-gray-100 border-b">
                      <tr>
                        <th class="px-4 py-3 text-left font-medium">Colore</th>
                        <th class="px-4 py-3 text-left font-medium">Taglia</th>
                        <th class="px-4 py-3 text-left font-medium">Barcode</th>
                        <th class="px-4 py-3 text-left font-medium">Costo Acquisto</th>
                        <th class="px-4 py-3 text-center font-medium">Azioni</th>
                      </tr>
                    </thead>
                    <tbody>
                      <tr *ngFor="let variant of productVariants" 
                        [class.bg-yellow-50]="editingVariantId === variant.id"
                        class="border-b hover:bg-gray-50">
                        
                        <!-- Normal mode: display data -->
                        <ng-container *ngIf="editingVariantId !== variant.id">
                          <td class="px-4 py-3">
                            <div class="flex gap-2 items-center">
                              <div class="w-5 h-5 rounded border" [style.backgroundColor]="getColorHtml(variant.colorId)"></div>
                              <span>{{ getColorName(variant.colorId) }}</span>
                            </div>
                          </td>
                          <td class="px-4 py-3">{{ getSizeName(variant.sizeId) }}</td>
                          <td class="px-4 py-3">{{ variant.barcode }}</td>
                          <td class="px-4 py-3">&euro;{{ variant.purchaseCost.toFixed(2) }}</td>
                          <td class="px-4 py-3 text-center space-x-2">
                            <button
                              (click)="startEditVariant(variant)"
                              class="text-blue-600 hover:text-blue-800 text-sm">
                              Modifica
                            </button>
                            <button
                              (click)="askDelete(variant.id, 'variant')"
                              [disabled]="deletingVariantId === variant.id"
                              class="text-red-600 hover:text-red-800 text-sm disabled:opacity-50">
                              {{ deletingVariantId === variant.id ? 'Eliminando...' : 'Elimina' }}
                            </button>
                          </td>
                        </ng-container>

                        <!-- Edit mode: form inline -->
                        <ng-container *ngIf="editingVariantId === variant.id">
                          <td class="px-4 py-3">
                            <select [(ngModel)]="editingVariantForm.colorId"
                              class="w-full px-2 py-1 border rounded text-sm focus:outline-none focus:ring-2 focus:ring-blue-500">
                              <option value="">-- N/A --</option>
                              <option *ngFor="let color of colors" [value]="color.id">
                                {{ color.name }}
                              </option>
                            </select>
                          </td>
                          <td class="px-4 py-3">
                            <select [(ngModel)]="editingVariantForm.sizeId"
                              class="w-full px-2 py-1 border rounded text-sm focus:outline-none focus:ring-2 focus:ring-blue-500">
                              <option value="">-- N/A --</option>
                              <option *ngFor="let size of sizes" [value]="size.id">
                                {{ size.name }}
                              </option>
                            </select>
                          </td>
                          <td class="px-4 py-3">
                            <input [(ngModel)]="editingVariantForm.barcode" type="text"
                              class="w-full px-2 py-1 border rounded text-sm focus:outline-none focus:ring-2 focus:ring-blue-500" />
                          </td>
                          <td class="px-4 py-3">
                            <input [(ngModel)]="editingVariantForm.purchaseCost" type="number" step="0.01"
                              class="w-full px-2 py-1 border rounded text-sm focus:outline-none focus:ring-2 focus:ring-blue-500" />
                          </td>
                          <td class="px-4 py-3 text-center space-x-2">
                            <button
                              (click)="saveVariantEdit()"
                              [disabled]="savingVariantEdit"
                              class="text-green-600 hover:text-green-800 text-sm font-semibold disabled:opacity-50">
                              {{ savingVariantEdit ? '...' : 'Salva' }}
                            </button>
                            <button
                              (click)="cancelVariantEdit()"
                              class="text-gray-600 hover:text-gray-800 text-sm">
                              Annulla
                            </button>
                          </td>
                        </ng-container>
                      </tr>
                    </tbody>
                  </table>
                </div>
              </div>

              <!-- Add Variant Form -->
              <div class="border-t pt-6">
                <h3 class="text-lg font-semibold mb-4">Aggiungi Nuova Variante</h3>
                <div class="bg-gray-50 rounded p-4 space-y-4">
                  <div class="grid grid-cols-2 gap-4">
                    <div>
                      <label class="block text-sm font-medium mb-1">Colore *</label>
                      <select [(ngModel)]="newVariantForm.colorId"
                        class="w-full px-3 py-2 border rounded focus:outline-none focus:ring-2 focus:ring-blue-500">
                        <option value="">-- Seleziona colore --</option>
                        <option *ngFor="let color of colors" [value]="color.id">
                          {{ color.name }}
                        </option>
                      </select>
                      <div class="mt-2 flex gap-2 items-center" *ngIf="newVariantForm.colorId">
                        <div class="w-6 h-6 rounded border" 
                          [style.backgroundColor]="getColorHtml(newVariantForm.colorId)"></div>
                        <span class="text-xs text-gray-600">{{ getColorName(newVariantForm.colorId) }}</span>
                      </div>
                    </div>
                    <div>
                      <label class="block text-sm font-medium mb-1">Taglia *</label>
                      <select [(ngModel)]="newVariantForm.sizeId"
                        class="w-full px-3 py-2 border rounded focus:outline-none focus:ring-2 focus:ring-blue-500">
                        <option value="">-- Seleziona taglia --</option>
                        <option *ngFor="let size of sizes" [value]="size.id">{{ size.name }}</option>
                      </select>
                    </div>
                    <div>
                      <label class="block text-sm font-medium mb-1">Barcode *</label>
                      <input [(ngModel)]="newVariantForm.barcode" type="text" placeholder="es. 1234567890"
                        class="w-full px-3 py-2 border rounded focus:outline-none focus:ring-2 focus:ring-blue-500" />
                    </div>
                    <div>
                      <label class="block text-sm font-medium mb-1">Costo Acquisto *</label>
                      <input [(ngModel)]="newVariantForm.purchaseCost" type="number" step="0.01" placeholder="0.00"
                        class="w-full px-3 py-2 border rounded focus:outline-none focus:ring-2 focus:ring-blue-500" />
                    </div>
                  </div>
                  <div *ngIf="variantError" class="bg-red-100 text-red-800 p-3 rounded text-sm">{{ variantError }}</div>
                  <button
                    (click)="addVariant()"
                    [disabled]="savingVariant"
                    class="w-full bg-green-600 hover:bg-green-700 text-white px-4 py-2 rounded disabled:opacity-50">
                    {{ savingVariant ? 'Aggiunta...' : '+ Aggiungi Variante' }}
                  </button>
                </div>
              </div>
            </div>

            <!-- TAB: Gallery -->
            <div *ngIf="activeTab === 'gallery'" class="space-y-4">
              <div class="flex h-96 overflow-hidden gap-4">
                <div class="flex-1 bg-gray-900 flex items-center justify-center p-4 rounded border-2 border-gray-200">
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

                <div class="w-48 flex flex-col border rounded">
                  <div class="flex-1 overflow-y-auto p-2 space-y-2 border-b">
                    <div *ngFor="let img of (galleryProduct?.images || [])"
                      (click)="selectImage(img)"
                      class="relative rounded overflow-hidden cursor-pointer border-2 transition"
                      [class.border-blue-500]="selectedImage?.id === img.id"
                      [class.border-transparent]="selectedImage?.id !== img.id">
                      <img [src]="img.url" [alt]="img.url"
                        class="w-full h-24 object-cover"
                        (error)="onImgError($event)" />
                      <div *ngIf="img.isMain" class="absolute top-1 left-1 bg-yellow-400 text-xs font-bold px-1.5 py-0.5 rounded">
                        &#9733;
                      </div>
                    </div>
                    <div *ngIf="!galleryProduct || galleryProduct.images.length === 0" class="text-gray-400 text-xs text-center py-4">
                      Nessuna immagine
                    </div>
                  </div>

                  <div class="border-t p-2 space-y-1 flex-shrink-0">
                    <button *ngIf="selectedImage && !selectedImage.isMain"
                      (click)="setMainImage(selectedImage)"
                      class="w-full text-xs bg-yellow-400 hover:bg-yellow-500 font-semibold py-1.5 rounded">
                      &#9733; Imposta principale
                    </button>
                    <button *ngIf="selectedImage"
                      (click)="askDelete(selectedImage.id, 'image')"
                      class="w-full text-xs bg-red-600 hover:bg-red-700 text-white font-semibold py-1.5 rounded">
                      &#128465; Elimina
                    </button>
                  </div>
                </div>
              </div>

              <div class="border-t pt-4 space-y-2">
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

          <div class="flex gap-3 justify-end border-t p-6 flex-shrink-0">
            <button (click)="closeModal()" class="px-4 py-2 border rounded hover:bg-gray-50">Annulla</button>
            <button (click)="saveProduct()" [disabled]="savingProduct"
              class="bg-blue-600 hover:bg-blue-700 text-white px-4 py-2 rounded disabled:opacity-50">
              {{ savingProduct ? 'Salvataggio...' : (editingProductId ? 'Salva' : 'Crea Prodotto') }}
            </button>
          </div>
        </div>
      </div>

      <!-- ===== Modal Galleria Immagini (deprecated - now in modal tab) ===== -->
      <!-- Removed: Gallery is now integrated into the product modal as a tab -->

    <app-confirm-delete-modal
      [isOpen]="!!confirmDeleteId"
      message="Sei sicuro di voler eliminare questo elemento? L'operazione non è reversibile."
      (confirmed)="onDeleteConfirmed()"
      (cancelled)="onDeleteCancelled()">
    </app-confirm-delete-modal>
    </div>
  `
})
export class ProdottiComponent implements OnInit {
  // Product list
  products: ProductDto[] = [];
  currentPage = 1;
  pageSize = 20;
  totalCount = 0;
  totalPages = 0;
  loading = false;
  error: string | null = null;
  searchTerm = '';

  // Support data
  colors: ColorDto[] = [];
  sizes: SizeDto[] = [];
  categories: CategoryDto[] = [];
  collections: CollectionDto[] = [];
  materials: MaterialDto[] = [];
  loadingSupportData = false;

  // Modal state
  showModal = false;
  activeTab: 'details' | 'variants' | 'gallery' = 'details';
  editingProductId: string | null = null;
  savingProduct = false;
  formError: string | null = null;
  formData: CreateProductRequest = this.initializeProductForm();

  confirmDeleteId: string | null = null;
  confirmDeleteType: 'product' | 'variant' | 'image' | null = null;

  askDelete(id: string, type: 'product' | 'variant' | 'image' = 'product'): void {
    this.confirmDeleteId = id;
    this.confirmDeleteType = type;
  }

  onDeleteConfirmed(): void {
    if (!this.confirmDeleteId) return;
    const id = this.confirmDeleteId;
    const type = this.confirmDeleteType;
    this.confirmDeleteId = null;
    this.confirmDeleteType = null;
    if (type === 'product') this.deleteProduct(id);
    else if (type === 'variant') this.deleteVariant(id);
  }

  onDeleteCancelled(): void {
    this.confirmDeleteId = null;
    this.confirmDeleteType = null;
  }

  // Variant management
  productVariants: ProductVariantDto[] = [];
  loadingVariants = false;
  newVariantForm = this.initializeVariantForm();
  savingVariant = false;
  variantError: string | null = null;
  deletingVariantId: string | null = null;
  editingVariantId: string | null = null;
  savingVariantEdit = false;
  editingVariantForm = this.initializeVariantForm();

  // Gallery (now within modal)
  galleryProduct: ProductDto | null = null;
  selectedImage: ProductImageDto | null = null;
  newImageUrl = '';
  newImageIsMain = false;
  addingImage = false;
  galleryError: string | null = null;

  constructor(private productService: ProductService) { }

  ngOnInit(): void {
    this.loadSupportData();
    this.loadProducts();
  }

  private loadSupportData(): void {
    this.loadingSupportData = true;
    Promise.all([
      this.productService.getColors().toPromise(),
      this.productService.getSizes().toPromise(),
      this.productService.getCategories().toPromise(),
      this.productService.getCollections().toPromise(),
      this.productService.getMaterials().toPromise()
    ]).then(([colors, sizes, categories, collections, materials]) => {
      this.colors = colors || [];
      this.sizes = sizes || [];
      this.categories = categories || [];
      this.collections = collections || [];
      this.materials = materials || [];
      this.loadingSupportData = false;
    }).catch(err => {
      console.error('Error loading support data:', err);
      this.loadingSupportData = false;
    });
  }

  private initializeProductForm(): CreateProductRequest {
    return {
      supplierId: '',
      supplierCode: '',
      internalCode: '',
      name: '',
      defaultMarkupRate: 0,
      vatRate: 0,
      basePurchaseCost: 0,
      hasVariants: true
    };
  }

  private initializeVariantForm(defaultPurchaseCost = 0) {
    return {
      colorId: '',
      sizeId: '',
      barcode: '',
      purchaseCost: defaultPurchaseCost
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

  search(): void {
    this.currentPage = 1;
    this.loadProducts();
  }

  nextPage(): void {
    if (this.currentPage < this.totalPages) {
      this.currentPage++;
      this.loadProducts();
    }
  }

  previousPage(): void {
    if (this.currentPage > 1) {
      this.currentPage--;
      this.loadProducts();
    }
  }

  openNewProductModal(): void {
    this.editingProductId = null;
    this.formData = this.initializeProductForm();
    this.newVariantForm = this.initializeVariantForm();
    this.productVariants = [];
    this.activeTab = 'details';
    this.formError = null;
    this.variantError = null;
    this.galleryProduct = null;
    this.selectedImage = null;
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
      basePurchaseCost: product.basePurchaseCost,
      hasVariants: product.hasVariants ?? true,
      categoryId: product.categoryId,
      collectionId: product.collectionId,
      materialId: product.materialId
    };
    
    this.newVariantForm = {
      colorId: '',
      sizeId: '',
      barcode: '',
      purchaseCost: product.basePurchaseCost
    };
    
    this.activeTab = 'details';
    this.formError = null;
    this.variantError = null;
    this.galleryProduct = { ...product, images: [...(product.images ?? [])] };
    this.selectedImage = this.galleryProduct.images.find(i => i.isMain) ?? this.galleryProduct.images[0] ?? null;
    this.loadSupportData();
    this.loadVariants(product.id);
    this.showModal = true;
  }

  closeModal(): void {
    this.showModal = false;
    this.editingProductId = null;
    this.formData = this.initializeProductForm();
    this.newVariantForm = this.initializeVariantForm();
    this.productVariants = [];
    this.galleryProduct = null;
    this.selectedImage = null;
  }

  private loadVariants(productId: string): void {
    this.loadingVariants = true;
    this.productService.getProductVariants(productId).subscribe({
      next: (variants) => {
        this.productVariants = variants;
        this.loadingVariants = false;
      },
      error: (err) => {
        console.error('Error loading variants:', err);
        this.loadingVariants = false;
      }
    });
  }

  saveProduct(): void {
    this.formError = null;
    if (!this.formData.name || !this.formData.internalCode) {
      this.formError = 'Completa i campi obbligatori (Nome e Codice Interno)';
      return;
    }
    if (this.formData.basePurchaseCost == null || this.formData.basePurchaseCost < 0) {
      this.formError = 'Il Prezzo Base Fornitore è obbligatorio';
      return;
    }
    if (!this.formData.supplierId?.trim()) {
      this.formData.supplierId = this.generateUUID();
    }

    this.savingProduct = true;
    const operation = this.editingProductId
      ? this.productService.updateProduct(this.editingProductId, this.formData)
      : this.productService.createProduct(this.formData);

    operation.subscribe({
      next: (product) => {
        this.savingProduct = false;
        if (!this.editingProductId) {
          this.editingProductId = product.id;
          this.galleryProduct = { ...product, images: [...(product.images ?? [])] };
          this.newVariantForm = this.initializeVariantForm(product.basePurchaseCost);
          this.activeTab = 'variants';
        } else {
          // Prodotto aggiornato: chiudi modale
          this.closeModal();
        }
        this.currentPage = 1;
        this.loadProducts();
      },
      error: (err) => {
        this.formError = err?.error && typeof err.error === 'string'
          ? err.error
          : (this.editingProductId ? 'Errore nella modifica del prodotto' : 'Errore nella creazione del prodotto');
        this.savingProduct = false;
        console.error(err);
      }
    });
  }

  addVariant(): void {
    this.variantError = null;
    if (!this.newVariantForm.colorId || !this.newVariantForm.sizeId || !this.newVariantForm.barcode || this.newVariantForm.purchaseCost === null || this.newVariantForm.purchaseCost === undefined) {
      this.variantError = 'Completa tutti i campi obbligatori';
      return;
    }
    if (this.productVariants.some(v => v.barcode === this.newVariantForm.barcode)) {
      this.variantError = 'Barcode già esistente';
      return;
    }

    const request: CreateProductVariantRequest = {
      productId: this.editingProductId!,
      colorId: this.newVariantForm.colorId,
      sizeId: this.newVariantForm.sizeId,
      barcode: this.newVariantForm.barcode,
      purchaseCost: this.newVariantForm.purchaseCost
    };

    this.savingVariant = true;
    this.productService.createProductVariant(request).subscribe({
      next: (variant) => {
        this.productVariants.push(variant);
        this.newVariantForm = this.initializeVariantForm(this.formData.basePurchaseCost ?? 0);
        this.savingVariant = false;
      },
      error: (err) => {
        this.variantError = err?.error && typeof err.error === 'string'
          ? err.error
          : 'Errore nell\'aggiunta della variante';
        this.savingVariant = false;
        console.error(err);
      }
    });
  }

  deleteVariant(variantId: string): void {
    if (!this.editingProductId) return;

    this.deletingVariantId = variantId;
    this.productService.deleteProductVariant(this.editingProductId, variantId).subscribe({
      next: () => {
        this.productVariants = this.productVariants.filter(v => v.id !== variantId);
        this.deletingVariantId = null;
      },
      error: (err) => {
        this.variantError = 'Errore nell\'eliminazione della variante';
        this.deletingVariantId = null;
        console.error(err);
      }
    });
  }

  startEditVariant(variant: ProductVariantDto): void {
    this.editingVariantId = variant.id;
    this.editingVariantForm = {
      colorId: variant.colorId || '',
      sizeId: variant.sizeId || '',
      barcode: variant.barcode,
      purchaseCost: variant.purchaseCost
    };
    this.variantError = null;
  }

  saveVariantEdit(): void {
    if (!this.editingProductId || !this.editingVariantId) return;

    this.savingVariantEdit = true;
    const request = {
      colorId: this.editingVariantForm.colorId || null,
      sizeId: this.editingVariantForm.sizeId || null,
      barcode: this.editingVariantForm.barcode,
      purchaseCost: this.editingVariantForm.purchaseCost
    };

    this.productService.updateProductVariant(this.editingProductId, this.editingVariantId, request).subscribe({
      next: (updated) => {
        const idx = this.productVariants.findIndex(v => v.id === this.editingVariantId);
        if (idx >= 0) {
          this.productVariants[idx] = updated;
        }
        this.editingVariantId = null;
        this.savingVariantEdit = false;
      },
      error: (err) => {
        this.variantError = err?.error && typeof err.error === 'string'
          ? err.error
          : 'Errore nella modifica della variante';
        this.savingVariantEdit = false;
        console.error(err);
      }
    });
  }

  cancelVariantEdit(): void {
    this.editingVariantId = null;
    this.editingVariantForm = this.initializeVariantForm();
    this.variantError = null;
  }

  deleteProduct(productId: string): void {
    this.loading = true;
    this.productService.deleteProduct(productId).subscribe({
      next: () => {
        this.loading = false;
        this.loadProducts();
      },
      error: (err) => {
        this.error = "Errore nell'eliminazione del prodotto";
        this.loading = false;
        console.error(err);
      }
    });
  }

  openGalleryModal(product: ProductDto): void {
    this.editProduct(product);
    this.activeTab = 'gallery';
  }

  closeGalleryModal(): void {
    this.closeModal();
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
    product.mainImageUrl = undefined;
    (event.target as HTMLImageElement).style.display = 'none';
  }

  onImgError(event: Event): void {
    (event.target as HTMLImageElement).style.display = 'none';
  }

  getSizeName(sizeId?: string): string {
    if (!sizeId) return 'N/A';
    const size = this.sizes.find(s => s.id === sizeId);
    return size?.name || 'N/A';
  }

  getColorName(colorId?: string): string {
    if (!colorId) return 'N/A';
    const color = this.colors.find(c => c.id === colorId);
    return color?.name || 'N/A';
  }

  getColorHtml(colorId?: string): string {
    if (!colorId) return '#000000';
    const color = this.colors.find(c => c.id === colorId);
    return color?.htmlColor || '#000000';
  }

  private generateUUID(): string {
    return 'xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx'.replace(/[xy]/g, function(c) {
      const r = Math.random() * 16 | 0;
      const v = c === 'x' ? r : (r & 0x3 | 0x8);
      return v.toString(16);
    });
  }
}


