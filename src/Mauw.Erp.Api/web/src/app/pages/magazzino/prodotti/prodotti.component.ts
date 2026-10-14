import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ProductService } from '../../../services/product.service';
import { ProductDto, PageResponse, CreateProductRequest, CreateProductVariantRequest } from '../../../models/product.model';

@Component({
  selector: 'app-prodotti',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <div class="p-6">
      <div class="flex justify-between items-center mb-6">
        <h1 class="text-3xl font-bold">Prodotti</h1>
        <button 
          (click)="openNewProductModal()"
          class="bg-blue-600 hover:bg-blue-700 text-white px-4 py-2 rounded">
          + Nuovo Prodotto
        </button>
      </div>

      <!-- Barra di ricerca -->
      <div class="mb-6">
        <input
          [(ngModel)]="searchTerm"
          (keyup.enter)="search()"
          type="text"
          placeholder="Cerca per nome o codice interno..."
          class="w-full px-4 py-2 border rounded-lg focus:outline-none focus:ring-2 focus:ring-blue-500"
        />
      </div>

      <!-- Tabella prodotti -->
      <div class="bg-white rounded-lg shadow overflow-hidden">
        <table class="w-full">
          <thead class="bg-gray-100 border-b">
            <tr>
              <th class="px-6 py-3 text-left text-sm font-semibold">Codice</th>
              <th class="px-6 py-3 text-left text-sm font-semibold">Nome Prodotto</th>
              <th class="px-6 py-3 text-left text-sm font-semibold">Fornitori Codice</th>
              <th class="px-6 py-3 text-left text-sm font-semibold">Varianti</th>
              <th class="px-6 py-3 text-left text-sm font-semibold">Prezzo Acquisto</th>
              <th class="px-6 py-3 text-left text-sm font-semibold">Azioni</th>
            </tr>
          </thead>
          <tbody>
            <tr *ngFor="let product of products" class="border-b hover:bg-gray-50">
              <td class="px-6 py-3 text-sm">{{ product.internalCode }}</td>
              <td class="px-6 py-3 text-sm">{{ product.name }}</td>
              <td class="px-6 py-3 text-sm">{{ product.supplierCode }}</td>
              <td class="px-6 py-3 text-sm">
                <span class="bg-blue-100 text-blue-800 px-2 py-1 rounded">{{ product.variants.length }}</span>
              </td>
              <td class="px-6 py-3 text-sm">
                <span *ngIf="product.variants.length > 0">
                  €{{ product.variants[0].purchaseCost.toFixed(2) }}
                </span>
              </td>
              <td class="px-6 py-3 text-sm space-x-2">
                <button 
                  (click)="editProduct(product)"
                  class="text-blue-600 hover:text-blue-800 underline">
                  Modifica
                </button>
                <button 
                  (click)="deleteProduct(product.id)"
                  class="text-red-600 hover:text-red-800 underline">
                  Elimina
                </button>
              </td>
            </tr>
            <tr *ngIf="products.length === 0">
              <td colspan="6" class="px-6 py-8 text-center text-gray-500">
                Nessun prodotto trovato
              </td>
            </tr>
          </tbody>
        </table>
      </div>

      <!-- Paginazione -->
      <div class="mt-6 flex justify-between items-center">
        <div class="text-sm text-gray-600">
          Pagina {{ currentPage }} di {{ totalPages }} ({{ totalCount }} prodotti totali)
        </div>
        <div class="flex gap-2">
          <button
            [disabled]="currentPage === 1"
            (click)="previousPage()"
            class="px-4 py-2 border rounded hover:bg-gray-50 disabled:opacity-50"
          >
            Precedente
          </button>
          <button
            [disabled]="currentPage === totalPages"
            (click)="nextPage()"
            class="px-4 py-2 border rounded hover:bg-gray-50 disabled:opacity-50"
          >
            Successiva
          </button>
        </div>
      </div>

      <!-- Loading/Error -->
      <div *ngIf="loading" class="text-center text-gray-500 mt-4">
        Caricamento...
      </div>
      <div *ngIf="error" class="bg-red-100 text-red-800 p-4 rounded mt-4">
        {{ error }}
      </div>

      <!-- Modal Form -->
      <div *ngIf="showModal" class="fixed inset-0 bg-black bg-opacity-50 flex items-center justify-center z-50">
        <div class="bg-white rounded-lg shadow-lg max-w-2xl w-full mx-4 max-h-[90vh] overflow-y-auto">
          <div class="sticky top-0 bg-white border-b p-6 flex justify-between items-center">
            <h2 class="text-2xl font-bold">
              {{ editingProductId ? 'Modifica Prodotto' : 'Nuovo Prodotto' }}
            </h2>
            <button 
              (click)="closeModal()"
              class="text-gray-500 hover:text-gray-700 text-2xl">
              ×
            </button>
          </div>

          <div class="p-6 space-y-4">
            <!-- Informazioni Prodotto -->
            <div class="space-y-4">
              <div class="grid grid-cols-2 gap-4">
                <div>
                  <label class="block text-sm font-medium mb-1">Codice Interno *</label>
                  <input 
                    [(ngModel)]="formData.internalCode"
                    type="text" 
                    class="w-full px-3 py-2 border rounded focus:outline-none focus:ring-2 focus:ring-blue-500"
                  />
                </div>
                <div>
                  <label class="block text-sm font-medium mb-1">Nome Prodotto *</label>
                  <input 
                    [(ngModel)]="formData.name"
                    type="text" 
                    class="w-full px-3 py-2 border rounded focus:outline-none focus:ring-2 focus:ring-blue-500"
                  />
                </div>
                <div>
                  <label class="block text-sm font-medium mb-1">Codice Fornitore</label>
                  <input 
                    [(ngModel)]="formData.supplierCode"
                    type="text" 
                    class="w-full px-3 py-2 border rounded focus:outline-none focus:ring-2 focus:ring-blue-500"
                  />
                </div>
                <div>
                  <label class="block text-sm font-medium mb-1">Markup Rate (%)</label>
                  <input 
                    [(ngModel)]="formData.defaultMarkupRate"
                    type="number" 
                    step="0.01"
                    class="w-full px-3 py-2 border rounded focus:outline-none focus:ring-2 focus:ring-blue-500"
                  />
                </div>
                <div>
                  <label class="block text-sm font-medium mb-1">VAT Rate (%)</label>
                  <input 
                    [(ngModel)]="formData.vatRate"
                    type="number" 
                    step="0.01"
                    class="w-full px-3 py-2 border rounded focus:outline-none focus:ring-2 focus:ring-blue-500"
                  />
                </div>
                <div>
                  <label class="block text-sm font-medium mb-1">URL Immagine</label>
                  <input 
                    [(ngModel)]="formData.imageUrl"
                    type="text" 
                    class="w-full px-3 py-2 border rounded focus:outline-none focus:ring-2 focus:ring-blue-500"
                  />
                </div>
              </div>
            </div>

            <!-- Varianti -->
            <div class="border-t pt-6">
              <div class="flex justify-between items-center mb-4">
                <h3 class="text-lg font-semibold">Varianti</h3>
                <button 
                  (click)="addVariant()"
                  class="bg-green-600 hover:bg-green-700 text-white px-3 py-1 rounded text-sm">
                  + Aggiungi Variante
                </button>
              </div>

              <div *ngFor="let variant of formData.variants; let i = index" class="border rounded p-4 mb-4 space-y-3">
                <div class="flex justify-between items-center mb-3">
                  <h4 class="font-medium">Variante {{ i + 1 }}</h4>
                  <button 
                    (click)="removeVariant(i)"
                    class="text-red-600 hover:text-red-800 text-sm">
                    Rimuovi
                  </button>
                </div>

                <div class="grid grid-cols-2 gap-3">
                  <div>
                    <label class="block text-sm font-medium mb-1">Taglia</label>
                    <input 
                      [(ngModel)]="variant.size"
                      type="text" 
                      placeholder="es. M, L, XL"
                      class="w-full px-3 py-2 border rounded focus:outline-none focus:ring-2 focus:ring-blue-500"
                    />
                  </div>
                  <div>
                    <label class="block text-sm font-medium mb-1">Colore</label>
                    <input 
                      [(ngModel)]="variant.color"
                      type="text" 
                      placeholder="es. Rosso, Blu"
                      class="w-full px-3 py-2 border rounded focus:outline-none focus:ring-2 focus:ring-blue-500"
                    />
                  </div>
                  <div>
                    <label class="block text-sm font-medium mb-1">Barcode</label>
                    <input 
                      [(ngModel)]="variant.barcode"
                      type="text" 
                      class="w-full px-3 py-2 border rounded focus:outline-none focus:ring-2 focus:ring-blue-500"
                    />
                  </div>
                  <div>
                    <label class="block text-sm font-medium mb-1">Costo Acquisto</label>
                    <input 
                      [(ngModel)]="variant.purchaseCost"
                      type="number" 
                      step="0.01"
                      class="w-full px-3 py-2 border rounded focus:outline-none focus:ring-2 focus:ring-blue-500"
                    />
                  </div>
                </div>
                <div>
                  <label class="block text-sm font-medium mb-1">Quantità Stock</label>
                  <input 
                    [(ngModel)]="variant.stockQuantity"
                    type="number" 
                    class="w-full px-3 py-2 border rounded focus:outline-none focus:ring-2 focus:ring-blue-500"
                  />
                </div>
              </div>
            </div>

            <!-- Bottoni -->
            <div class="flex gap-3 justify-end border-t pt-6">
              <button 
                (click)="closeModal()"
                class="px-4 py-2 border rounded hover:bg-gray-50">
                Annulla
              </button>
              <button 
                (click)="saveProduct()"
                [disabled]="savingProduct"
                class="bg-blue-600 hover:bg-blue-700 text-white px-4 py-2 rounded disabled:opacity-50">
                {{ savingProduct ? 'Salvataggio...' : 'Salva' }}
              </button>
            </div>
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

  constructor(private productService: ProductService) { }

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
      imageUrl: '',
      variants: []
    };
  }

  loadProducts(): void {
    this.loading = true;
    this.error = null;
    this.productService.getProducts(this.currentPage, this.pageSize, this.searchTerm || undefined)
      .subscribe({
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
      imageUrl: product.imageUrl || '',
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

  addVariant(): void {
    this.formData.variants.push(this.createEmptyVariant());
  }

  removeVariant(index: number): void {
    this.formData.variants.splice(index, 1);
  }

  private createEmptyVariant(): CreateProductVariantRequest {
    return {
      size: '',
      color: '',
      barcode: '',
      purchaseCost: 0,
      stockQuantity: 0
    };
  }

  saveProduct(): void {
    if (!this.formData.name || !this.formData.internalCode || this.formData.variants.length === 0) {
      this.error = 'Completa i campi obbligatori e aggiungi almeno una variante';
      return;
    }

    // Genera UUID per supplierId se vuoto
    if (!this.formData.supplierId || this.formData.supplierId.trim() === '') {
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
        this.error = this.editingProductId ? 'Errore nella modifica del prodotto' : 'Errore nella creazione del prodotto';
        this.savingProduct = false;
        console.error(err);
      }
    });
  }

  private generateUUID(): string {
    return 'xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx'.replace(/[xy]/g, function(c) {
      const r = Math.random() * 16 | 0;
      const v = c === 'x' ? r : (r & 0x3 | 0x8);
      return v.toString(16);
    });
  }

  deleteProduct(productId: string): void {
    if (!confirm('Sei sicuro di voler eliminare questo prodotto?')) {
      return;
    }

    this.loading = true;
    this.error = null;
    this.productService.deleteProduct(productId)
      .subscribe({
        next: () => {
          this.loading = false;
          this.loadProducts();
        },
        error: (err) => {
          this.error = 'Errore nell\'eliminazione del prodotto';
          this.loading = false;
          console.error(err);
        }
      });
  }
}
