import { Component, OnInit, ViewChild, ElementRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { ApiService } from '../../../services/api.service';
import { ProductService } from '../../../services/product.service';
import { ProductMatrixModalComponent, MatrixSelection } from './product-matrix-modal.component';
import { ProductParentDto, ProductMatrixDto } from '../../../models/product.model';
import { CassaSessionService, CartItem, CassaContact, PausedSessionSummary } from '../../../services/cassa-session.service';

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
  quantityAvailable: number;
  weightGrams?: number | null;
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
<!-- ===== MODALI ===== -->

<!-- Matrix modal overlay -->
<app-product-matrix-modal
  *ngIf="matrixProduct"
  [matrix]="matrixProduct"
  [vatRate]="selectedVatRate"
  (cancel)="closeMatrix()"
  (confirmed)="onMatrixConfirmed($event)">
</app-product-matrix-modal>

<!-- Conferma svuota carrello -->
<div *ngIf="showClearCartConfirm"
  class="fixed inset-0 bg-black/50 z-50 flex items-center justify-center p-4">
  <div class="bg-white rounded-2xl shadow-2xl w-full max-w-sm p-6">
    <h2 class="text-lg font-bold text-gray-800 mb-2">Svuota carrello?</h2>
    <p class="text-sm text-gray-500 mb-6">Tutti gli articoli verranno rimossi. L'operazione non è reversibile.</p>
    <div class="flex gap-3 justify-end">
      <button (click)="showClearCartConfirm = false" class="px-4 py-2 text-sm text-gray-600 hover:bg-gray-100 rounded-lg transition">Annulla</button>
      <button (click)="confirmClearCart()" class="px-4 py-2 bg-red-600 hover:bg-red-700 text-white text-sm font-semibold rounded-lg transition">Svuota</button>
    </div>
  </div>
</div>

<!-- Sessione sospesa con successo -->
<div *ngIf="showSuspendSuccess"
  class="fixed inset-0 bg-black/50 z-50 flex items-center justify-center p-4">
  <div class="bg-white rounded-2xl shadow-2xl w-full max-w-md p-6">
    <div class="text-center mb-4">
      <div class="text-5xl mb-3">⏸️</div>
      <h2 class="text-xl font-bold text-gray-800">Sessione sospesa</h2>
      <p class="text-sm text-gray-500 mt-1">Comunica questo codice all'operatore che dovrà riprendere la sessione</p>
    </div>
    <div class="bg-blue-50 border-2 border-blue-300 rounded-xl p-4 text-center mb-6">
      <div class="text-xs text-blue-500 uppercase font-semibold mb-1">Codice sessione</div>
      <div class="text-3xl font-mono font-bold text-blue-700 tracking-widest">{{ currentSessionCode }}</div>
    </div>
    <button (click)="closeSuspendSuccess()" class="w-full px-4 py-2.5 bg-blue-600 hover:bg-blue-700 text-white font-semibold rounded-lg transition">Chiudi</button>
  </div>
</div>

<!-- Modale riprendi sessione -->
<div *ngIf="showResumeModal"
  class="fixed inset-0 bg-black/50 z-50 flex items-center justify-center p-4">
  <div class="bg-white rounded-2xl shadow-2xl w-full max-w-lg p-6">
    <h2 class="text-lg font-bold text-gray-800 mb-1">Riprendi sessione</h2>
    <p class="text-sm text-gray-500 mb-4">Seleziona una sessione sospesa da riprendere su questo terminale.</p>
    <div *ngIf="loadingPausedSessions" class="text-center py-8 text-gray-400">Caricamento...</div>
    <div *ngIf="!loadingPausedSessions && pausedSessions.length === 0" class="text-center py-8 text-gray-400">
      <div class="text-3xl mb-2">🔍</div>
      <p class="text-sm">Nessuna sessione sospesa trovata</p>
    </div>
    <div *ngIf="!loadingPausedSessions && pausedSessions.length > 0" class="space-y-2 max-h-72 overflow-y-auto mb-4">
      <div *ngFor="let s of pausedSessions"
        class="border rounded-xl px-4 py-3 flex items-center justify-between hover:bg-blue-50 transition">
        <div>
          <div class="font-mono font-bold text-blue-700 text-sm">{{ s.sessionCode }}</div>
          <div class="text-xs text-gray-500 mt-0.5">
            {{ s.contactName ?? 'Nessun cliente' }} · {{ s.cartItemCount }} articoli ·
            {{ s.updatedAt | date:'dd/MM HH:mm' }}
          </div>
        </div>
        <button (click)="doResumeSession(s)"
          [disabled]="resumeLoading"
          class="ml-4 px-3 py-1.5 bg-blue-600 hover:bg-blue-700 disabled:opacity-40 text-white text-xs font-semibold rounded-lg transition">
          {{ resumeLoading ? '...' : '▶ Riprendi' }}
        </button>
      </div>
    </div>
    <button (click)="showResumeModal = false" class="w-full px-4 py-2 text-sm text-gray-500 hover:bg-gray-100 rounded-lg transition">Chiudi</button>
  </div>
</div>

<!-- Conferma termina sessione -->
<div *ngIf="showTerminateConfirm"
  class="fixed inset-0 bg-black/50 z-50 flex items-center justify-center p-4">
  <div class="bg-white rounded-2xl shadow-2xl w-full max-w-sm p-6">
    <h2 class="text-lg font-bold text-gray-800 mb-2">Terminare la sessione?</h2>
    <p class="text-sm text-gray-500 mb-6">Il carrello verrà svuotato e la sessione non sarà più recuperabile da nessun terminale.</p>
    <div class="flex gap-3 justify-end">
      <button (click)="showTerminateConfirm = false" [disabled]="terminateLoading" class="px-4 py-2 text-sm text-gray-600 hover:bg-gray-100 rounded-lg transition">Annulla</button>
      <button (click)="doTerminateSession()" [disabled]="terminateLoading"
        class="px-4 py-2 bg-red-600 hover:bg-red-700 disabled:opacity-40 text-white text-sm font-semibold rounded-lg transition">
        {{ terminateLoading ? '...' : 'Termina' }}
      </button>
    </div>
  </div>
</div>

<!-- ===== LAYOUT PRINCIPALE ===== -->
<div class="flex flex-col h-[calc(100vh-7rem)] bg-gray-100 overflow-hidden">

  <!-- Barra sessione -->
  <div class="bg-white border-b px-4 py-2 flex items-center justify-between flex-shrink-0">
    <div class="flex items-center gap-2">
      <span class="text-xs text-gray-400 font-medium uppercase tracking-wide">Sessione:</span>
      <span *ngIf="currentSessionCode" class="font-mono text-xs font-bold text-blue-700 bg-blue-50 px-2 py-0.5 rounded">{{ currentSessionCode }}</span>
      <span *ngIf="!currentSessionCode" class="text-xs text-gray-400 italic">non salvata</span>
    </div>
    <div class="flex items-center gap-2">
      <button (click)="openResumeModal()"
        class="px-3 py-1.5 text-xs font-semibold text-blue-600 border border-blue-200 hover:bg-blue-50 rounded-lg transition flex items-center gap-1">
        ▶ Riprendi sessione
      </button>
      <button (click)="suspendSession()"
        [disabled]="cart.length === 0 || !selectedContact || suspendLoading"
        [title]="!selectedContact ? 'Seleziona un cliente per sospendere' : ''"
        class="px-3 py-1.5 text-xs font-semibold text-yellow-700 border border-yellow-300 bg-yellow-50 hover:bg-yellow-100 disabled:opacity-40 rounded-lg transition flex items-center gap-1">
        {{ suspendLoading ? '...' : '⏸ Sospendi' }}
      </button>
      <button (click)="showTerminateConfirm = true"
        class="px-3 py-1.5 text-xs font-semibold text-white bg-red-600 hover:bg-red-700 rounded-lg transition flex items-center gap-1">
        🔴 Termina sessione
      </button>
    </div>
  </div>

  <div class="flex flex-1 overflow-hidden">

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
      <input [(ngModel)]="documentNumber" (ngModelChange)="sessionService.setDocumentNumber($event)" type="text" placeholder="Autogenerato se vuoto"
        class="w-full px-3 py-2 border border-gray-200 rounded-lg text-sm focus:outline-none focus:border-blue-500" />
    </div>
  </div>

  <!-- ===== CENTER: Cart ===== -->
  <div class="flex-1 flex flex-col overflow-hidden">
    <div class="px-6 py-4 bg-white border-b flex items-center justify-between">
      <h1 class="text-xl font-bold text-gray-800">🛒 Banco / Cassa Showroom</h1>
      <button *ngIf="cart.length > 0" (click)="showClearCartConfirm = true"
        class="text-xs text-red-600 hover:text-white hover:bg-red-600 font-medium border border-red-400 hover:border-red-600 px-3 py-1 rounded transition">
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
    <div *ngIf="cart.length > 0" class="bg-white border-t px-4 py-2.5 flex-shrink-0">
      <div class="flex items-center justify-between gap-4">

        <!-- Totals: compact inline -->
        <div class="flex items-center gap-5 divide-x divide-gray-200">
          <div class="text-right pr-5">
            <div class="text-[10px] text-gray-400 uppercase tracking-wide font-medium">Imponibile</div>
            <div class="text-sm font-semibold text-gray-700">{{ totals.net | number:'1.2-2' }} €</div>
          </div>
          <div class="text-right pr-5">
            <div class="text-[10px] text-gray-400 uppercase tracking-wide font-medium">IVA</div>
            <div class="text-sm font-semibold text-gray-500">{{ totals.vat | number:'1.2-2' }} €</div>
          </div>
          <div class="text-right pr-5">
            <div class="text-[10px] text-gray-400 uppercase tracking-wide font-medium">Totale Lordo</div>
            <div class="text-base font-extrabold text-blue-700">{{ totals.gross | number:'1.2-2' }} €</div>
          </div>
          <div class="text-right">
            <div class="text-[10px] text-gray-400 uppercase tracking-wide font-medium">Peso</div>
            <div class="text-sm font-medium text-gray-400">{{ totals.weightKg | number:'1.2-2' }} kg</div>
          </div>
        </div>

        <!-- Action buttons: aligned right -->
        <div class="flex gap-2 flex-shrink-0 ml-auto">
          <button (click)="generateDocument('ddt')"
            [disabled]="!selectedContact || generating"
            class="px-4 py-2 bg-green-600 hover:bg-green-700 disabled:opacity-40 text-white text-sm font-semibold rounded-lg shadow transition flex items-center gap-1.5">
            <span>🚚</span>
            <span>{{ generating === 'ddt' ? '...' : 'DDT' }}</span>
          </button>
          <button (click)="generateDocument('invoice')"
            [disabled]="!selectedContact || generating"
            class="px-4 py-2 bg-blue-700 hover:bg-blue-800 disabled:opacity-40 text-white text-sm font-semibold rounded-lg shadow transition flex items-center gap-1.5">
            <span>🧾</span>
            <span>{{ generating === 'invoice' ? '...' : 'Fattura' }}</span>
          </button>
        </div>
      </div>
      <p *ngIf="!selectedContact" class="text-[11px] text-orange-500 mt-1">⚠️ Seleziona un cliente per procedere</p>
      <p *ngIf="generateError" class="text-[11px] text-red-500 mt-1">{{ generateError }}</p>
    </div>
  </div>

  </div><!-- /flex row -->
</div><!-- /flex-col outer -->
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
  contactResults: CassaContact[] = [];
  selectedContact: CassaContact | null = null;
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

  // Session management
  currentSessionCode: string | null = null;
  showSuspendSuccess = false;
  showResumeModal = false;
  showTerminateConfirm = false;
  showClearCartConfirm = false;
  pausedSessions: PausedSessionSummary[] = [];
  loadingPausedSessions = false;
  suspendLoading = false;
  terminateLoading = false;
  resumeLoading = false;

  get totals() {
    const net   = this.cart.reduce((s, i) => s + i.lineNetTotal, 0);
    const gross = this.cart.reduce((s, i) => s + i.lineGrossTotal, 0);
    const weightG = this.cart.reduce((s, i) => s + (i.weightGrams ?? 0) * i.quantity, 0);
    return { net, vat: gross - net, gross, weightKg: weightG / 1000 };
  }

  constructor(
    private api: ApiService,
    private productService: ProductService,
    private router: Router,
    protected sessionService: CassaSessionService
  ) {}

  ngOnInit(): void {
    const snap = this.sessionService.snapshot;
    this.cart = snap.cart;
    this.selectedContact = snap.selectedContact;
    this.documentNumber = snap.documentNumber;
    this.currentSessionCode = snap.sessionCode;
    this.loadDefaultWarehouse();
  }

  private syncToService(): void {
    this.sessionService.setCart(this.cart);
    this.sessionService.setContact(this.selectedContact);
    this.sessionService.setDocumentNumber(this.documentNumber);
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
        quantityAvailable: s.availableStock,
        weightGrams: s.weightGrams
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
    this.api.get<PageResponse<CassaContact>>(`v1/contacts?page=1&pageSize=20&search=${encodeURIComponent(q)}`).subscribe({
      next: (res) => { this.contactResults = res.items || []; },
      error: () => { this.contactResults = []; }
    });
  }

  selectContact(c: CassaContact): void {
    this.selectedContact = c;
    this.contactSearch = '';
    this.contactResults = [];
    this.syncToService();
  }

  clearContact(): void {
    this.selectedContact = null;
    this.contactSearch = '';
    this.syncToService();
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
    this.syncToService();
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
    this.syncToService();
  }

  decreaseQty(i: number): void {
    if (this.cart[i].quantity <= 1) { this.removeItem(i); return; }
    this.cart[i].quantity--;
    this.recalcLine(this.cart[i]);
    this.syncToService();
  }

  removeItem(i: number): void {
    this.cart.splice(i, 1);
    this.syncToService();
  }

  confirmClearCart(): void {
    this.cart = [];
    this.showClearCartConfirm = false;
    this.syncToService();
  }

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
    this.sessionService.clearSession();
    this.cart = [];
    this.documentNumber = '';
    this.currentSessionCode = null;
    this.router.navigate([type === 'ddt' ? '/ddt/elenco' : '/fatturazione/fatture']);
  }

  // ── Session management ─────────────────────────────────────
  suspendSession(): void {
    if (this.cart.length === 0 || !this.selectedContact) return;
    this.suspendLoading = true;
    this.sessionService.setCart(this.cart);
    this.sessionService.setContact(this.selectedContact);
    this.sessionService.setDocumentNumber(this.documentNumber);

    this.sessionService.createOrPauseSession().subscribe({
      next: (dto) => {
        this.sessionService.setSessionRef(dto.id, dto.sessionCode);
        this.currentSessionCode = dto.sessionCode;
        this.suspendLoading = false;
        this.showSuspendSuccess = true;
      },
      error: () => { this.suspendLoading = false; }
    });
  }

  closeSuspendSuccess(): void {
    this.showSuspendSuccess = false;
    this.sessionService.clearSession();
    this.cart = [];
    this.selectedContact = null;
    this.documentNumber = '';
    this.currentSessionCode = null;
  }

  openResumeModal(): void {
    this.showResumeModal = true;
    this.loadingPausedSessions = true;
    this.sessionService.getPausedSessions().subscribe({
      next: (list) => { this.pausedSessions = list; this.loadingPausedSessions = false; },
      error: () => { this.pausedSessions = []; this.loadingPausedSessions = false; }
    });
  }

  doResumeSession(summary: PausedSessionSummary): void {
    this.resumeLoading = true;
    this.sessionService.resumeSession(summary.id).subscribe({
      next: () => {
        this.sessionService.loadSessionDetail(summary.id).subscribe({
          next: (detail) => {
            this.sessionService.applyLoadedSession(detail);
            const snap = this.sessionService.snapshot;
            this.cart = snap.cart;
            this.selectedContact = snap.selectedContact;
            this.documentNumber = snap.documentNumber;
            this.currentSessionCode = snap.sessionCode;
            this.resumeLoading = false;
            this.showResumeModal = false;
          },
          error: () => { this.resumeLoading = false; }
        });
      },
      error: () => { this.resumeLoading = false; }
    });
  }

  doTerminateSession(): void {
    this.terminateLoading = true;
    const sessionId = this.sessionService.snapshot.sessionId;

    const cleanup = () => {
      this.sessionService.clearSession();
      this.cart = [];
      this.selectedContact = null;
      this.documentNumber = '';
      this.currentSessionCode = null;
      this.terminateLoading = false;
      this.showTerminateConfirm = false;
    };

    if (sessionId) {
      this.sessionService.terminateSession(sessionId).subscribe({
        next: cleanup,
        error: cleanup // clean locally even if server call fails
      });
    } else {
      cleanup();
    }
  }
}
