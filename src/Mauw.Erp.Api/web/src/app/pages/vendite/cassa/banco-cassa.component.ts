import { Component, OnInit, ViewChild, ElementRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { ApiService } from '../../../services/api.service';
import { ProductService } from '../../../services/product.service';
import { ProductMatrixModalComponent, MatrixSelection } from './product-matrix-modal.component';
import { ProductParentDto, ProductMatrixDto } from '../../../models/product.model';

interface BarcodeVariant {
  barcode: string;
  variantId: string;
  productId: string;
  productName: string;
  size: string;
  color: string;
  unitNetPrice: number;
  unitGrossPrice: number;
  vatRate: number;
  quantityAvailable: number; // da WH-CENTRALE
}

interface CartItem extends BarcodeVariant {
  quantity: number;
  lineNetTotal: number;
  lineGrossTotal: number;
  maxQuantity: number; // limite giacenza disponibile
}

interface Contact {
  id: string;
  businessName: string;
  vatNumber: string;
  defaultDiscountRate: number;
}

interface PageResponse<T> {
  items: T[];
  totalCount: number;
}

@Component({
  selector: 'app-banco-cassa',
  standalone: true,
  imports: [CommonModule, FormsModule, ProductMatrixModalComponent],
  template: `
<!-- Matrix modal overlay -->
<app-product-matrix-modal
  *ngIf="matrixProduct"
  [matrix]="matrixProduct"
  [vatRate]="selectedVatRate"
  (cancel)="closeMatrix()"
  (confirmed)="onMatrixConfirmed($event)">
</app-product-matrix-modal>

<div class="flex h-[calc(100vh-7rem)] bg-gray-100 overflow-hidden">

  <!-- ===== LEFT: Cliente + Scanner + Ricerca ===== -->
  <div class="w-80 flex flex-col bg-white border-r shadow-sm flex-shrink-0 overflow-hidden">

    <!-- 1. CLIENTE OBBLIGATORIO -->
    <div class="p-4 border-b" [class.bg-green-50]="selectedContact" [class.bg-orange-50]="!selectedContact">
      <label class="block text-xs font-semibold uppercase tracking-wide mb-1"
        [class.text-orange-600]="!selectedContact" [class.text-green-700]="selectedContact">
        {{ selectedContact ? '✓ Cliente selezionato' : '⚠️ Seleziona Cliente (obbligatorio)' }}
      </label>
      <input [(ngModel)]="contactSearch" (input)="onContactSearch()"
        type="text"
        [placeholder]="selectedContact ? selectedContact.businessName : 'Cerca cliente o azienda...'"
        class="w-full px-3 py-2 border rounded-lg text-sm focus:outline-none"
        [class.border-green-400]="selectedContact"
        [class.border-orange-400]="!selectedContact"
        [class.bg-white]="true" />
      <div *ngIf="contactResults.length > 0"
        class="mt-1 max-h-40 overflow-y-auto border border-gray-200 rounded-lg divide-y shadow-sm bg-white z-20 relative">
        <div *ngFor="let c of contactResults"
          class="px-3 py-2 hover:bg-blue-50 cursor-pointer"
          (click)="selectContact(c)">
          <div class="text-sm font-medium">{{ c.businessName }}</div>
          <div class="text-xs text-gray-500">P.IVA: {{ c.vatNumber }}</div>
        </div>
      </div>
      <div *ngIf="selectedContact" class="mt-2 flex items-center justify-between">
        <span class="text-xs font-medium text-green-800">{{ selectedContact.businessName }}</span>
        <button (click)="clearContact()" class="text-green-400 hover:text-red-500 text-lg leading-none transition">&times;</button>
      </div>
    </div>

    <!-- 2. SCANNER + RICERCA (disabilitati senza cliente) -->
    <div class="p-4 border-b bg-gray-50 flex-1 overflow-y-auto min-h-0" [class.opacity-40]="!selectedContact" [class.pointer-events-none]="!selectedContact">
      <h2 class="text-sm font-bold text-gray-700 mb-3">📦 Aggiungi Articolo</h2>

      <!-- Barcode scanner -->
      <div class="mb-4">
        <label class="block text-xs font-semibold text-gray-500 uppercase tracking-wide mb-1">Scansiona Barcode</label>
        <div class="relative">
          <input #barcodeInputEl
            [(ngModel)]="barcodeInput_value"
            (keydown.enter)="lookupBarcode()"
            (input)="onBarcodeInput()"
            type="text"
            placeholder="Barcode / codice..."
            class="w-full pl-9 pr-3 py-2.5 border-2 rounded-lg text-sm focus:outline-none focus:border-blue-500 font-mono"
            [class.border-red-400]="barcodeError"
            [class.border-gray-200]="!barcodeError" />
          <svg class="absolute left-2.5 top-2.5 w-4 h-4 text-gray-400" fill="none" stroke="currentColor" viewBox="0 0 24 24">
            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2"
              d="M12 4v1m6 11h2m-6 0h-2m0 0H8m4 0v1m-4 0H6m0 0H4m2 0V9m0 0H4m2 0v1M8 4h1m4 0h1m4 0h1M4 4h1"/>
          </svg>
        </div>
        <p *ngIf="barcodeError" class="text-red-500 text-xs mt-1">{{ barcodeError }}</p>
        <button (click)="lookupBarcode()" [disabled]="!barcodeInput_value || lookingUp"
          class="mt-2 w-full bg-blue-600 hover:bg-blue-700 disabled:opacity-40 text-white text-sm font-semibold py-2 rounded-lg transition">
          {{ lookingUp ? '...' : '+ Aggiungi da Barcode' }}
        </button>
      </div>

      <div class="relative flex items-center mb-4">
        <div class="flex-grow border-t border-gray-200"></div>
        <span class="mx-3 text-xs text-gray-400 font-medium">oppure cerca modello</span>
        <div class="flex-grow border-t border-gray-200"></div>
      </div>

      <!-- Product PARENT search -->
      <div>
        <label class="block text-xs font-semibold text-gray-500 uppercase tracking-wide mb-1">Cerca per Nome / Codice Fornitore</label>
        <input [(ngModel)]="productSearch" (input)="onProductSearch()"
          type="text" placeholder="Es. AB26W220 o nome articolo..."
          class="w-full px-3 py-2 border border-gray-200 rounded-lg text-sm focus:outline-none focus:border-blue-500" />

        <div *ngIf="parentResults.length > 0"
          class="mt-2 max-h-56 overflow-y-auto border border-gray-200 rounded-lg divide-y divide-gray-100 shadow-sm bg-white">
          <div *ngFor="let p of parentResults"
            class="px-3 py-2.5 hover:bg-blue-50 cursor-pointer transition"
            (click)="onParentProductClick(p)">
            <div class="font-medium text-sm text-gray-800 truncate">{{ p.name }}</div>
            <div class="flex gap-2 mt-0.5 items-center">
              <span class="text-xs bg-blue-100 text-blue-700 px-1.5 py-0.5 rounded font-mono">{{ p.supplierCode }}</span>
              <span *ngIf="p.hasVariants" class="text-xs bg-purple-100 text-purple-700 px-1.5 py-0.5 rounded">Taglia/Colore</span>
              <span *ngIf="!p.hasVariants" class="text-xs bg-gray-100 text-gray-600 px-1.5 py-0.5 rounded">Unico</span>
              <span class="text-xs text-gray-400 ml-auto">{{ p.variantCount }} var.</span>
            </div>
          </div>
        </div>
        <p *ngIf="searchDone && parentResults.length === 0 && productSearch" class="text-xs text-gray-400 mt-2 text-center">
          Nessun risultato
        </p>
      </div>
    </div>

    <!-- 3. N° DOCUMENTO -->
    <div class="p-4">
      <label class="block text-xs font-semibold text-gray-500 uppercase tracking-wide mb-1">N° Documento</label>
      <input [(ngModel)]="documentNumber" type="text" placeholder="Autogenerato se vuoto"
        class="w-full px-3 py-2 border border-gray-200 rounded-lg text-sm focus:outline-none focus:border-blue-500" />
    </div>
  </div>

  <!-- ===== CENTER: Cart ===== -->
  <div class="flex-1 flex flex-col overflow-hidden">
    <div class="px-6 py-4 bg-white border-b flex items-center justify-between">
      <h1 class="text-xl font-bold text-gray-800">🛒 Banco / Cassa Showroom</h1>
      <button *ngIf="cart.length > 0" (click)="clearCart()"
        class="text-xs text-red-400 hover:text-red-600 font-medium border border-red-200 hover:border-red-400 px-3 py-1 rounded transition">
        Svuota carrello
      </button>
    </div>

    <!-- Cart items -->
    <div class="flex-1 overflow-y-auto p-6 min-h-0">
      <div *ngIf="cart.length === 0" class="flex flex-col items-center justify-center h-full text-gray-400">
        <div class="text-6xl mb-4">🛍️</div>
        <p class="text-lg font-medium">Carrello vuoto</p>
        <p class="text-sm mt-1">Scansiona un barcode o cerca un articolo</p>
      </div>

      <div *ngIf="cart.length > 0" class="space-y-2">
        <div *ngFor="let item of cart; let i = index"
          class="bg-white rounded-xl border border-gray-100 shadow-sm px-4 py-3 flex items-center gap-4">
          <!-- Product info -->
          <div class="flex-1 min-w-0">
            <div class="font-semibold text-gray-800 truncate">{{ item.productName }}</div>
            <div class="flex gap-2 mt-0.5 flex-wrap">
              <span *ngIf="item.color" class="text-xs bg-gray-100 px-1.5 py-0.5 rounded text-gray-600">{{ item.color }}</span>
              <span *ngIf="item.size" class="text-xs bg-gray-100 px-1.5 py-0.5 rounded text-gray-600">{{ item.size }}</span>
              <span class="text-xs text-gray-400 font-mono">{{ item.barcode }}</span>
              <span [class]="'text-xs px-1.5 py-0.5 rounded font-semibold ' + (item.maxQuantity > item.quantity ? 'bg-green-50 text-green-600' : 'bg-red-50 text-red-500')">
                Disp: {{ item.maxQuantity }} pz
              </span>
            </div>
          </div>

          <!-- Unit price -->
          <div class="text-right w-24 flex-shrink-0">
            <div class="text-xs text-gray-400">Prezzo unitario</div>
            <div class="text-sm font-medium">{{ item.unitGrossPrice | number:'1.2-2' }} €</div>
          </div>

          <!-- Quantity controls -->
          <div class="flex items-center gap-1.5 flex-shrink-0">
            <button (click)="decreaseQty(i)"
              class="w-7 h-7 rounded-full border border-gray-200 hover:bg-gray-100 flex items-center justify-center text-gray-600 font-bold text-sm transition">
              –
            </button>
            <span class="w-8 text-center font-bold text-gray-800">{{ item.quantity }}</span>
            <button (click)="increaseQty(i)"
              [disabled]="item.quantity >= item.maxQuantity"
              class="w-7 h-7 rounded-full bg-blue-600 hover:bg-blue-700 disabled:bg-gray-300 flex items-center justify-center text-white font-bold text-sm transition">
              +
            </button>
          </div>

          <!-- Line total -->
          <div class="text-right w-24 flex-shrink-0">
            <div class="text-xs text-gray-400">Totale riga</div>
            <div class="font-bold text-gray-800">{{ item.lineGrossTotal | number:'1.2-2' }} €</div>
          </div>

          <!-- Remove -->
          <button (click)="removeItem(i)"
            class="text-gray-300 hover:text-red-500 transition text-xl leading-none flex-shrink-0">
            &times;
          </button>
        </div>
      </div>
    </div>

    <!-- Totals + Actions -->
    <div *ngIf="cart.length > 0" class="bg-white border-t px-6 py-4">
      <div class="flex items-end justify-between gap-6">
        <!-- Totals -->
        <div class="grid grid-cols-3 gap-6 flex-1">
          <div class="text-center">
            <div class="text-xs text-gray-500 font-medium uppercase tracking-wide mb-0.5">Imponibile</div>
            <div class="text-xl font-bold text-gray-700">{{ totals.net | number:'1.2-2' }} €</div>
          </div>
          <div class="text-center">
            <div class="text-xs text-gray-500 font-medium uppercase tracking-wide mb-0.5">IVA</div>
            <div class="text-xl font-bold text-gray-600">{{ totals.vat | number:'1.2-2' }} €</div>
          </div>
          <div class="text-center">
            <div class="text-xs text-gray-500 font-medium uppercase tracking-wide mb-0.5">Totale Lordo</div>
            <div class="text-2xl font-extrabold text-blue-700">{{ totals.gross | number:'1.2-2' }} €</div>
          </div>
        </div>

        <!-- Action buttons -->
        <div class="flex gap-3 flex-shrink-0">
          <button (click)="generateDocument('ddt')"
            [disabled]="!selectedContact || generating"
            class="px-6 py-3 bg-green-600 hover:bg-green-700 disabled:opacity-40 text-white font-bold rounded-xl shadow transition flex items-center gap-2">
            <span class="text-lg">🚚</span>
            <span>{{ generating === 'ddt' ? 'Generazione...' : 'Genera DDT' }}</span>
          </button>
          <button (click)="generateDocument('invoice')"
            [disabled]="!selectedContact || generating"
            class="px-6 py-3 bg-blue-700 hover:bg-blue-800 disabled:opacity-40 text-white font-bold rounded-xl shadow transition flex items-center gap-2">
            <span class="text-lg">🧾</span>
            <span>{{ generating === 'invoice' ? 'Generazione...' : 'Fattura Accompagnatoria' }}</span>
          </button>
        </div>
      </div>
      <p *ngIf="!selectedContact" class="text-xs text-orange-500 mt-2">
        ⚠️ Seleziona un cliente per procedere
      </p>
      <p *ngIf="generateError" class="text-xs text-red-500 mt-2">{{ generateError }}</p>
    </div>
  </div>

</div>
  `
})
export class BancoCassaComponent implements OnInit {
  // Barcode
  barcodeInput_value = '';
  barcodeError: string | null = null;
  lookingUp = false;

  // Product PARENT search
  productSearch = '';
  parentResults: ProductParentDto[] = [];
  searchDone = false;
  private searchTimer: any;

  // Matrix modal
  matrixProduct: ProductMatrixDto | null = null;
  selectedVatRate = 22;
  matrixLoading = false;

  // Contact
  contactSearch = '';
  contactResults: Contact[] = [];
  selectedContact: Contact | null = null;
  private contactTimer: any;

  // Document
  documentNumber = '';

  // Cart
  cart: CartItem[] = [];

  // Default warehouse
  private defaultWarehouseId: string | null = null;

  // Generation
  generating: 'ddt' | 'invoice' | null = null;
  generateError: string | null = null;

  get totals() {
    const net   = this.cart.reduce((s, i) => s + i.lineNetTotal, 0);
    const gross = this.cart.reduce((s, i) => s + i.lineGrossTotal, 0);
    return { net, vat: gross - net, gross };
  }

  constructor(
    private api: ApiService,
    private productService: ProductService,
    private router: Router
  ) {}

  ngOnInit(): void {
    this.loadDefaultWarehouse();
  }

  private loadDefaultWarehouse(): void {
    this.api.get<any>('v1/warehouses?page=1&pageSize=100&isActive=true').subscribe({
      next: (res) => {
        const def = (res.items as any[]).find((w: any) => w.isDefault);
        this.defaultWarehouseId = def?.id ?? null;
      },
      error: () => {}
    });
  }

  // ── Barcode ──────────────────────────────────────────────
  onBarcodeInput(): void {
    this.barcodeError = null;
  }

  lookupBarcode(): void {
    const code = this.barcodeInput_value.trim();
    if (!code) return;
    this.barcodeError = null;
    this.lookingUp = true;
    // Use stock-aware endpoint when warehouse is known
    const url = this.defaultWarehouseId
      ? `v1/inventory/barcode/${encodeURIComponent(code)}?warehouseId=${this.defaultWarehouseId}`
      : `v1/products/barcode/${encodeURIComponent(code)}`;
    this.api.get<BarcodeVariant>(url).subscribe({
      next: (v) => {
        this.addBarcodeVariantToCart(v);
        this.barcodeInput_value = '';
        this.lookingUp = false;
      },
      error: () => {
        this.barcodeError = 'Barcode non trovato';
        this.lookingUp = false;
      }
    });
  }

  // ── Product search (parent products) ─────────────────────
  onProductSearch(): void {
    clearTimeout(this.searchTimer);
    this.searchDone = false;
    if (!this.productSearch.trim()) { this.parentResults = []; return; }
    this.searchTimer = setTimeout(() => this.runParentSearch(), 350);
  }

  private runParentSearch(): void {
    const q = this.productSearch.trim();
    this.productService.searchParents(q, 20).subscribe({
      next: (res) => { this.parentResults = res; this.searchDone = true; },
      error: () => { this.parentResults = []; this.searchDone = true; }
    });
  }

  onParentProductClick(p: ProductParentDto): void {
    this.productSearch = '';
    this.parentResults = [];
    this.barcodeError = null;

    if (!p.hasVariants) {
      // Single-variant product: load matrix to get the single cell and add directly
      this.matrixLoading = true;
      this.productService.getProductMatrix(p.productId, this.defaultWarehouseId ?? undefined).subscribe({
        next: (matrix) => {
          this.matrixLoading = false;
          if (matrix.cells.length > 0) {
            const cell = matrix.cells[0];
            this.addBarcodeVariantToCart({
              barcode: cell.barcode,
              variantId: cell.variantId,
              productId: p.productId,
              productName: p.name,
              size: '',
              color: '',
              unitNetPrice: cell.unitPrice / 1.22,
              unitGrossPrice: cell.unitPrice,
              vatRate: 22,
              quantityAvailable: cell.availableStock
            });
          }
        },
        error: () => { this.matrixLoading = false; }
      });
    } else {
      // Product with variants: open matrix modal
      this.matrixLoading = true;
      this.productService.getProductMatrix(p.productId, this.defaultWarehouseId ?? undefined).subscribe({
        next: (matrix) => {
          this.matrixLoading = false;
          this.matrixProduct = matrix;
          this.selectedVatRate = 22;
        },
        error: () => { this.matrixLoading = false; }
      });
    }
  }

  closeMatrix(): void {
    this.matrixProduct = null;
  }

  onMatrixConfirmed(selections: MatrixSelection[]): void {
    this.matrixProduct = null;
    for (const s of selections) {
      this.addBarcodeVariantToCart({
        barcode: s.barcode,
        variantId: s.variantId,
        productId: '',
        productName: s.productName,
        size: s.sizeName ?? '',
        color: s.colorName ?? '',
        unitNetPrice: s.unitPrice / (1 + s.vatRate / 100),
        unitGrossPrice: s.unitPrice,
        vatRate: s.vatRate,
        quantityAvailable: s.availableStock
      }, s.quantity);
    }
  }

  // ── Contact search ────────────────────────────────────────
  onContactSearch(): void {
    clearTimeout(this.contactTimer);
    if (!this.contactSearch.trim()) { this.contactResults = []; return; }
    this.contactTimer = setTimeout(() => this.runContactSearch(), 300);
  }

  private runContactSearch(): void {
    const q = this.contactSearch.trim();
    this.api.get<PageResponse<Contact>>(`v1/contacts?page=1&pageSize=20&search=${encodeURIComponent(q)}`).subscribe({
      next: (res) => { this.contactResults = res.items || []; },
      error: () => { this.contactResults = []; }
    });
  }

  selectContact(c: Contact): void {
    this.selectedContact = c;
    this.contactSearch = '';
    this.contactResults = [];
  }

  clearContact(): void {
    this.selectedContact = null;
    this.contactSearch = '';
  }

  // ── Cart ──────────────────────────────────────────────────
  addBarcodeVariantToCart(v: BarcodeVariant, qty = 1): void {
    const available = v.quantityAvailable ?? 9999;
    const existing = this.cart.find(i => i.variantId === v.variantId);
    if (existing) {
      const newQty = existing.quantity + qty;
      if (newQty <= existing.maxQuantity) {
        existing.quantity = newQty;
        this.recalcLine(existing);
      } else {
        this.barcodeError = `Giacenza esaurita (max ${existing.maxQuantity} pz disponibili)`;
      }
    } else {
      if (available <= 0 && qty > 0) {
        this.barcodeError = `⚠️ Giacenza esaurita – ${v.productName}`;
        return;
      }
      const item: CartItem = {
        ...v,
        quantity: qty,
        lineNetTotal: v.unitNetPrice * qty,
        lineGrossTotal: v.unitGrossPrice * qty,
        maxQuantity: Math.max(available, qty)
      };
      this.cart.push(item);
    }
    this.productSearch = '';
    this.parentResults = [];
  }

  /** @deprecated use addBarcodeVariantToCart */
  addVariantToCart(v: BarcodeVariant): void {
    this.addBarcodeVariantToCart(v);
  }

  increaseQty(i: number): void {
    const item = this.cart[i];
    if (item.quantity >= item.maxQuantity) {
      this.generateError = `Giacenza massima raggiunta per "${item.productName}" (${item.maxQuantity} pz)`;
      return;
    }
    item.quantity++;
    this.recalcLine(item);
  }

  decreaseQty(i: number): void {
    if (this.cart[i].quantity <= 1) { this.removeItem(i); return; }
    this.cart[i].quantity--;
    this.recalcLine(this.cart[i]);
  }

  removeItem(i: number): void { this.cart.splice(i, 1); }

  clearCart(): void { this.cart = []; }

  private recalcLine(item: CartItem): void {
    item.lineNetTotal   = item.unitNetPrice   * item.quantity;
    item.lineGrossTotal = item.unitGrossPrice * item.quantity;
  }

  // ── Document generation ───────────────────────────────────
  generateDocument(type: 'ddt' | 'invoice'): void {
    if (!this.selectedContact || this.cart.length === 0) return;
    this.generating = type;
    this.generateError = null;

    if (type === 'ddt') {
      const body = { contactId: this.selectedContact.id, ddtNumber: this.documentNumber || undefined };
      this.api.post<any>('v1/ddts', body).subscribe({
        next: (ddt) => this.addItemsAndNavigate('ddt', ddt.id),
        error: (e) => { this.generateError = e?.error ?? 'Errore creazione DDT'; this.generating = null; }
      });
    } else {
      const body = { contactId: this.selectedContact.id };
      this.api.post<any>('v1/invoices', body).subscribe({
        next: (inv) => this.addItemsAndNavigate('invoice', inv.id),
        error: (e) => { this.generateError = e?.error ?? 'Errore creazione fattura'; this.generating = null; }
      });
    }
  }

  private addItemsAndNavigate(type: 'ddt' | 'invoice', docId: string): void {
    const endpoint = type === 'ddt' ? `v1/ddts/${docId}/items` : `v1/invoices/${docId}/items`;
    const requests = this.cart.map(item => ({
      variantId: item.variantId,
      quantity: item.quantity,
      unitNetPrice: item.unitNetPrice,
      unitVatAmount: +(item.unitNetPrice * (item.vatRate / 100)).toFixed(4)
    }));

    const sendNext = (idx: number) => {
      if (idx >= requests.length) {
        // Record ShowroomSale movements on WH-CENTRALE
        this.recordStockMovements(docId, type);
        return;
      }
      this.api.post<any>(endpoint, requests[idx]).subscribe({
        next: () => sendNext(idx + 1),
        error: (e) => { this.generateError = `Errore articolo ${idx + 1}: ${e?.error ?? 'sconosciuto'}`; this.generating = null; }
      });
    };
    sendNext(0);
  }

  private recordStockMovements(docRef: string, type: 'ddt' | 'invoice'): void {
    if (!this.defaultWarehouseId) {
      this.finalizeAfterGeneration(type);
      return;
    }
    const cartSnapshot = [...this.cart];
    const sendMovement = (idx: number) => {
      if (idx >= cartSnapshot.length) {
        this.finalizeAfterGeneration(type);
        return;
      }
      const item = cartSnapshot[idx];
      const body = {
        warehouseId: this.defaultWarehouseId,
        productVariantId: item.variantId,
        reason: 2, // ShowroomSale
        quantity: -item.quantity, // uscita
        documentReference: docRef,
        notes: `Vendita cassa – ${type === 'ddt' ? 'DDT' : 'Fattura'}`
      };
      this.api.post<any>('v1/inventory/movements', body).subscribe({
        next: () => sendMovement(idx + 1),
        error: () => sendMovement(idx + 1) // non blocca: log solo in console
      });
    };
    sendMovement(0);
  }

  private finalizeAfterGeneration(type: 'ddt' | 'invoice'): void {
    this.generating = null;
    this.cart = [];
    this.documentNumber = '';
    this.router.navigate([type === 'ddt' ? '/ddt/elenco' : '/fatturazione/fatture']);
  }
}
