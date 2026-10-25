import { Component, DestroyRef, OnDestroy, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { forkJoin, from } from 'rxjs';
import { concatMap, finalize, toArray } from 'rxjs/operators';
import { ContactService } from '../../services/contact.service';
import { ProductService } from '../../services/product.service';
import { SupplierPurchaseOrderService } from '../../services/supplier-purchase-order.service';
import { ContactSummary } from '../../models/contact.model';
import { VariantSearchDto } from '../../models/product.model';
import {
  SupplierPurchaseOrder, SupplierReconciliation, SupplierReconciliationLine, SupplierReturnDraft
} from '../../models/supplier-purchase-order.model';

@Component({
  selector: 'app-ordini-fornitore',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink],
  template: `
    <main class="mx-auto max-w-7xl space-y-4 p-3 sm:p-4">
      <header class="flex flex-wrap items-end justify-between gap-3">
        <div><p class="text-sm text-gray-500">Acquisti</p><h1 class="text-2xl font-semibold dark:text-white">Ordini fornitore</h1></div>
        <a routerLink="/acquisti/ddt-fornitore/nuovo" class="rounded bg-indigo-600 px-4 py-2 text-white">Carica DDT</a>
      </header>
      <p *ngIf="error" role="alert" class="rounded bg-red-50 p-3 text-red-700">{{ error }}</p>
      <p *ngIf="success" role="status" class="rounded bg-green-50 p-3 text-green-700">{{ success }}</p>

      <section class="rounded-xl border bg-white p-4 dark:bg-gray-900">
        <h2 class="mb-3 text-lg font-semibold dark:text-white">Nuovo ordine</h2>
        <div class="grid gap-3 md:grid-cols-3">
          <div class="relative">
            <label for="supplierSearch">Fornitore</label>
            <input id="supplierSearch" class="field" [(ngModel)]="supplierSearch" name="supplierSearch"
              (input)="onSupplierSearch()" [placeholder]="selectedSupplier?.businessName || 'Cerca fornitore...'"
              autocomplete="off">
            <div *ngIf="supplierResults.length" class="absolute z-30 mt-1 max-h-48 w-full overflow-y-auto rounded-lg border bg-white shadow-lg dark:bg-gray-800">
              <button *ngFor="let supplier of supplierResults" type="button"
                class="block w-full border-b px-3 py-2 text-left hover:bg-blue-50 dark:hover:bg-gray-700"
                (click)="selectSupplier(supplier)">
                <span class="block text-sm font-medium">{{ supplier.businessName }}</span>
                <span class="text-xs text-gray-500">{{ supplier.vatNumber || supplier.city || '' }}</span>
              </button>
            </div>
            <div *ngIf="selectedSupplier" class="mt-1 flex items-center justify-between text-xs text-green-700">
              <span>Fornitore selezionato</span>
              <button type="button" class="text-gray-500 hover:text-red-600" aria-label="Rimuovi fornitore"
                (click)="clearSupplier()">&times;</button>
            </div>
          </div>
          <label>Data ordine<input class="field" type="date" [(ngModel)]="orderDate" name="orderDate"></label>
          <label>Note<input class="field" [(ngModel)]="notes" name="notes"></label>
        </div>
        <div class="mt-3">
          <button class="btn" type="button" (click)="openArticleSearch()" [disabled]="busy || !supplierId">
            Cerca articolo
          </button>
          <span class="ml-2 text-sm text-gray-500" *ngIf="draftLines.length">
            {{ draftLines.length }} articoli nell'ordine
          </span>
        </div>
        <div *ngIf="articleSearchOpen" class="fixed inset-0 z-50 flex items-center justify-center bg-black/40 p-4"
          (click)="closeArticleSearch()">
          <section role="dialog" aria-modal="true" aria-labelledby="article-search-title"
            class="w-full max-w-2xl rounded-xl bg-white p-5 shadow-xl dark:bg-gray-900"
            (click)="$event.stopPropagation()">
            <div class="mb-4 flex items-center justify-between gap-3">
              <div>
                <h3 id="article-search-title" class="font-semibold dark:text-white">Cerca articolo</h3>
                <p class="text-xs text-gray-500">SKU, descrizione o codice fornitore: {{ selectedSupplier?.businessName }}</p>
              </div>
              <button type="button" class="rounded px-2 py-1 text-xl text-gray-500 hover:bg-gray-100"
                aria-label="Chiudi ricerca articoli" (click)="closeArticleSearch()">&times;</button>
            </div>
            <form class="mb-3 flex gap-2" (ngSubmit)="searchVariants()">
              <input class="field mt-0" [(ngModel)]="variantQuery" name="variantQuery"
                placeholder="SKU, descrizione o codice fornitore..." autocomplete="off" autofocus>
              <button class="btn primary shrink-0" type="submit" [disabled]="busy || !variantQuery.trim()">Cerca</button>
            </form>
            <div *ngIf="variants.length" class="max-h-72 overflow-auto rounded border dark:border-gray-700">
              <button *ngFor="let variant of variants" type="button"
                class="block w-full border-b p-3 text-left hover:bg-gray-50 dark:border-gray-700 dark:hover:bg-gray-800"
                (click)="addVariant(variant)">
                <span class="block font-medium">{{ variant.productName }}<ng-container *ngIf="variant.colorName || variant.sizeName"> · {{ variant.colorName }} / {{ variant.sizeName }}</ng-container></span>
                <span class="text-xs text-gray-500">SKU: {{ variant.sku || variant.barcode }} · Cod. fornitore: {{ variant.supplierCode || '—' }}{{ variant.purchaseCost != null ? ' · Costo: ' + variant.purchaseCost : '' }}</span>
              </button>
            </div>
            <p *ngIf="searchDone && !variants.length" class="py-6 text-center text-sm text-gray-500">
              Nessun articolo trovato per questo fornitore.
            </p>
            <div class="mt-4 flex justify-end">
              <button type="button" class="btn" (click)="closeArticleSearch()">Chiudi</button>
            </div>
          </section>
        </div>
        <div class="mt-3 overflow-x-auto">
          <table class="w-full min-w-[560px] text-sm">
            <thead><tr class="border-b text-left"><th class="p-2">Variante</th><th class="p-2">Quantità</th><th class="p-2">Costo unitario</th><th></th></tr></thead>
            <tbody>
              <tr *ngFor="let line of draftLines; let index = index" class="border-b">
                <td class="p-2">{{ line.articleDescription }}</td>
                <td class="p-2"><input class="field w-28" type="number" min="1" step="1" [(ngModel)]="line.quantityOrdered" [name]="'qty' + index"></td>
                <td class="p-2"><input class="field w-36" inputmode="decimal" [(ngModel)]="line.unitCost" [name]="'cost' + index"></td>
                <td class="p-2"><button class="text-red-700" type="button" (click)="draftLines.splice(index, 1)">Rimuovi</button></td>
              </tr>
              <tr *ngIf="!draftLines.length"><td colspan="4" class="p-5 text-center text-gray-500">Aggiungi almeno una variante.</td></tr>
            </tbody>
          </table>
        </div>
        <button class="btn primary mt-4" type="button" (click)="createOrder()" [disabled]="busy">Salva bozza ordine</button>
      </section>

      <section class="grid gap-5 lg:grid-cols-[minmax(0,1fr)_minmax(0,1.2fr)]">
        <div class="overflow-x-auto rounded-xl border bg-white dark:bg-gray-900">
          <div class="flex items-center justify-between border-b p-4"><h2 class="font-semibold">Ordini recenti</h2><button class="btn" (click)="loadOrders()">Aggiorna</button></div>
          <table class="w-full min-w-[480px] text-sm">
            <thead><tr class="border-b text-left"><th class="p-3">Ordine</th><th class="p-3">Fornitore</th><th class="p-3">Stato</th></tr></thead>
            <tbody><tr *ngFor="let order of orders" class="cursor-pointer border-b hover:bg-gray-50" (click)="select(order)">
              <td class="p-3 font-medium">{{ order.orderNumber }}</td><td class="p-3">{{ order.supplierName }}</td><td class="p-3">{{ statusLabel(order.status) }}</td>
            </tr><tr *ngIf="!orders.length"><td colspan="3" class="p-5 text-center text-gray-500">Nessun ordine.</td></tr></tbody>
          </table>
        </div>

        <div *ngIf="selected && reconciliation" class="space-y-4 rounded-xl border bg-white p-4 dark:bg-gray-900">
          <div class="flex flex-wrap items-center justify-between gap-2">
            <div><h2 class="font-semibold">{{ selected.orderNumber }} · {{ selected.supplierName }}</h2><p class="text-sm text-gray-500">{{ statusLabel(selected.status) }}</p></div>
            <a *ngIf="selected.status === 'Confirmed' || selected.status === 'PartiallyReceived'"
              [routerLink]="['/acquisti/ddt-fornitore/nuovo']" [queryParams]="{ purchaseOrderId: selected.id }" class="btn">Registra DDT</a>
          </div>
          <div class="overflow-x-auto">
            <table class="w-full min-w-[580px] text-sm">
              <thead><tr class="border-b text-left"><th class="p-2">SKU / variante</th><th class="p-2">Ord.</th><th class="p-2">Ricev.</th><th class="p-2">Delta</th><th class="p-2">Esito</th></tr></thead>
              <tbody><tr *ngFor="let line of reconciliation.lines" class="border-b" [ngClass]="rowClass(line)">
                <td class="p-2"><strong>{{ line.sku }}</strong><span class="block">{{ line.articleDescription }}</span></td><td class="p-2">{{ line.quantityOrdered }}</td><td class="p-2">{{ line.quantityReceived }}</td><td class="p-2">{{ line.delta > 0 ? '+' : '' }}{{ line.delta }}</td><td class="p-2">{{ rowLabel(line.status) }}</td>
              </tr></tbody>
            </table>
          </div>
          <div class="flex flex-wrap gap-2">
            <button class="btn" (click)="downloadShortages()" [disabled]="busy">Scarica Lista Mancanze</button>
            <button class="btn" (click)="createReturn()" [disabled]="busy || !hasSurplus()">Crea Reso da Eccedenze</button>
            <button class="btn" (click)="confirmOrder()" [disabled]="busy || selected.status !== 'Draft'">Conferma ordine</button>
            <button class="btn danger" (click)="closeOrder()" [disabled]="busy || ['Draft','Completed','ClosedWithDiscrepancies'].includes(selected.status)">Chiudi e Accetta Differenze</button>
          </div>
          <p class="text-xs text-gray-500">Il reso viene predisposto in bozza; la giacenza diminuisce solo alla successiva emissione/conferma dello scarico.</p>
        </div>
      </section>
      <section *ngIf="selected && returnDrafts.length" class="rounded-xl border bg-white p-4 dark:bg-gray-900">
        <h2 class="mb-3 font-semibold">Bozze DDT di reso</h2>
        <div *ngFor="let draft of returnDrafts" class="flex flex-wrap items-center justify-between gap-3 border-t py-3">
          <div>
            <strong>Reso {{ draft.id.slice(0, 8) }} · {{ draft.lines.length }} righe</strong>
            <span class="ml-2 text-sm text-gray-500">{{ draft.status === 'Draft' ? 'Bozza' : 'Emesso' }}</span>
            <p class="text-xs text-gray-500">{{ returnDescription(draft) }}</p>
          </div>
          <button *ngIf="draft.status === 'Draft'" class="btn danger" (click)="issueReturn(draft)" [disabled]="busy">
            Emetti reso e scarica giacenze
          </button>
        </div>
      </section>
    </main>
  `,
  styles: [`
    .field { display: block; width: 100%; margin-top: .25rem; border: 1px solid #d1d5db; border-radius: .4rem; padding: .5rem; color: #111827; background: white; }
    .btn { border: 1px solid #d1d5db; border-radius: .4rem; padding: .5rem .8rem; background: white; }
    .primary { color: white; background: #465fff; border-color: #465fff; }
    .danger { color: #991b1b; }
    .shortage { background: #fff7ed; } .surplus { background: #fef2f2; } .inline { background: #f0fdf4; }
  `]
})
export class OrdiniFornitoreComponent implements OnInit, OnDestroy {
  private readonly destroyRef = inject(DestroyRef);
  supplierSearch = '';
  supplierResults: ContactSummary[] = [];
  selectedSupplier: ContactSummary | null = null;
  private supplierSearchTimer: ReturnType<typeof setTimeout> | undefined;
  variants: VariantSearchDto[] = [];
  articleSearchOpen = false;
  searchDone = false;
  orders: SupplierPurchaseOrder[] = [];
  selected: SupplierPurchaseOrder | null = null;
  reconciliation: SupplierReconciliation | null = null;
  returnDrafts: SupplierReturnDraft[] = [];
  draftLines: { articleVariantId: string; articleDescription: string; quantityOrdered: number; unitCost: string }[] = [];
  supplierId = '';
  orderDate = new Date().toLocaleDateString('en-CA');
  notes = '';
  variantQuery = '';
  error = '';
  success = '';
  busy = false;

  constructor(
    private readonly purchaseOrders: SupplierPurchaseOrderService,
    private readonly contacts: ContactService,
    private readonly products: ProductService
  ) {}

  ngOnInit(): void {
    this.loadOrders();
  }

  ngOnDestroy(): void {
    clearTimeout(this.supplierSearchTimer);
  }

  onSupplierSearch(): void {
    clearTimeout(this.supplierSearchTimer);
    this.supplierResults = [];
    if (!this.supplierSearch.trim()) return;
    this.supplierSearchTimer = setTimeout(() => this.searchSuppliers(this.supplierSearch.trim()), 300);
  }

  private searchSuppliers(query: string): void {
    this.contacts.getList({ search: query, isSupplier: true, status: 'Active', page: 1, pageSize: 20 })
      .pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
        next: result => {
          if (this.supplierSearch.trim() === query && !this.supplierId) this.supplierResults = result.items;
        },
        error: error => this.showError(error)
      });
  }

  selectSupplier(supplier: ContactSummary): void {
    this.selectedSupplier = supplier;
    this.supplierId = supplier.id;
    this.supplierSearch = '';
    this.supplierResults = [];
    this.variantQuery = '';
    this.variants = [];
    this.searchDone = false;
  }

  clearSupplier(): void {
    this.selectedSupplier = null;
    this.supplierId = '';
    this.supplierSearch = '';
    this.supplierResults = [];
    this.variantQuery = '';
    this.variants = [];
    this.articleSearchOpen = false;
    this.searchDone = false;
  }

  loadOrders(): void {
    this.purchaseOrders.list().pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: orders => this.orders = orders,
      error: error => this.showError(error)
    });
  }

  searchVariants(): void {
    if (!this.supplierId || !this.variantQuery.trim()) return;
    this.error = '';
    this.searchDone = false;
    const query = this.variantQuery.trim();
    const supplierId = this.supplierId;
    this.products.searchVariants(query, 30, supplierId).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: variants => {
        if (this.supplierId !== supplierId || this.variantQuery.trim() !== query) return;
        this.variants = variants;
        this.searchDone = true;
      },
      error: error => this.showError(error)
    });
  }

  openArticleSearch(): void {
    if (!this.supplierId) return;
    this.articleSearchOpen = true;
  }

  closeArticleSearch(): void {
    this.articleSearchOpen = false;
  }

  addVariant(variant: VariantSearchDto): void {
    if (this.draftLines.some(line => line.articleVariantId === variant.variantId)) {
      this.closeArticleSearch();
      return;
    }
    const details = [variant.colorName, variant.sizeName].filter(Boolean).join(' / ');
    this.draftLines.push({
      articleVariantId: variant.variantId,
      articleDescription: details ? `${variant.productName} - ${details}` : variant.productName,
      quantityOrdered: 1,
      unitCost: variant.purchaseCost?.toString() ?? ''
    });
    this.closeArticleSearch();
  }

  createOrder(): void {
    this.error = '';
    if (!this.supplierId || this.draftLines.length === 0
        || this.draftLines.some(x => !Number.isInteger(x.quantityOrdered) || x.quantityOrdered <= 0
          || !/^\d+([.,]\d{1,8})?$/.test(x.unitCost.trim()))) {
      this.error = 'Seleziona il fornitore, aggiungi varianti e inserisci quantita e costi validi.';
      return;
    }
    this.busy = true;
    this.purchaseOrders.create({
      supplierId: this.supplierId, orderDate: `${this.orderDate}T00:00:00Z`, notes: this.notes,
      lines: this.draftLines.map(x => ({
        articleVariantId: x.articleVariantId, quantityOrdered: x.quantityOrdered,
        unitCost: x.unitCost.trim().replace(',', '.')
      }))
    }).pipe(takeUntilDestroyed(this.destroyRef), finalize(() => this.busy = false)).subscribe({
      next: order => {
        this.success = `Bozza ${order.orderNumber} creata.`;
        this.draftLines = []; this.notes = '';
        this.loadOrders(); this.select(order);
      },
      error: error => this.showError(error)
    });
  }

  select(order: SupplierPurchaseOrder): void {
    this.selected = order; this.reconciliation = null; this.returnDrafts = [];
    forkJoin({
      order: this.purchaseOrders.get(order.id),
      reconciliation: this.purchaseOrders.reconcile(order.id),
      returns: this.purchaseOrders.returnDrafts(order.id)
    }).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: result => {
        this.selected = result.order;
        this.reconciliation = result.reconciliation;
        this.returnDrafts = result.returns;
      },
      error: error => this.showError(error)
    });
  }

  confirmOrder(): void {
    if (!this.selected) return;
    this.busy = true;
    this.purchaseOrders.confirm(this.selected.id, this.selected.version)
      .pipe(takeUntilDestroyed(this.destroyRef), finalize(() => this.busy = false)).subscribe({
        next: order => { this.success = 'Ordine confermato.'; this.select(order); this.loadOrders(); },
        error: error => this.showError(error)
      });
  }

  downloadShortages(): void {
    if (!this.selected) return;
    this.busy = true;
    this.purchaseOrders.shortages(this.selected.id)
      .pipe(takeUntilDestroyed(this.destroyRef), finalize(() => this.busy = false)).subscribe({
        next: rows => {
          const csv = ['Variante;Quantita mancante', ...rows.map(row =>
            `"${row.sku.replaceAll('"', '""')} - ${row.articleDescription.replaceAll('"', '""')}";${row.quantityMissing}`)].join('\r\n');
          this.downloadBlob(new Blob(['\uFEFF', csv], { type: 'text/csv;charset=utf-8' }),
            `mancanze-${this.selected?.orderNumber ?? 'ordine'}.csv`);
        },
        error: error => this.showError(error)
      });
  }

  createReturn(): void {
    if (!this.selected || !this.reconciliation) return;
    const byNote = new Map<string, { supplierDeliveryNoteLineId: string; quantity: number }[]>();
    for (const line of this.reconciliation.lines.filter(x => x.delta > 0)) {
      let surplus = line.delta;
      for (const receipt of line.receipts) {
        const quantity = Math.min(surplus, receipt.quantity);
        if (quantity <= 0) continue;
        const rows = byNote.get(receipt.supplierDeliveryNoteId) ?? [];
        rows.push({ supplierDeliveryNoteLineId: receipt.supplierDeliveryNoteLineId, quantity });
        byNote.set(receipt.supplierDeliveryNoteId, rows);
        surplus -= quantity;
        if (!surplus) break;
      }
    }
    if (!byNote.size) return;
    this.busy = true;
    from([...byNote.values()]).pipe(
      concatMap(lines => this.purchaseOrders.createReturnDraft(this.selected!.id, lines)),
      toArray(),
      takeUntilDestroyed(this.destroyRef),
      finalize(() => this.busy = false)
    ).subscribe({
        next: drafts => {
          this.returnDrafts = [...drafts, ...this.returnDrafts];
          this.success = `Bozze di reso create: ${drafts.length}.`;
        },
        error: error => this.showError(error)
      });
  }

  issueReturn(draft: SupplierReturnDraft): void {
    if (!this.selected || !window.confirm('Emettere il DDT di reso e scaricare le relative giacenze?')) return;
    this.busy = true;
    this.purchaseOrders.issueReturnDraft(this.selected.id, draft.id, draft.version)
      .pipe(takeUntilDestroyed(this.destroyRef), finalize(() => this.busy = false)).subscribe({
        next: issued => {
          this.success = 'Reso emesso e giacenze aggiornate.';
          this.returnDrafts = this.returnDrafts.map(row => row.id === issued.id ? issued : row);
          this.select(this.selected!);
          this.loadOrders();
        },
        error: error => this.showError(error)
      });
  }

  closeOrder(): void {
    if (!this.selected) return;
    this.busy = true;
    this.purchaseOrders.close(this.selected.id, this.selected.version)
      .pipe(takeUntilDestroyed(this.destroyRef), finalize(() => this.busy = false)).subscribe({
        next: order => { this.success = 'Ordine chiuso; le differenze sono state accettate.'; this.select(order); this.loadOrders(); },
        error: error => this.showError(error)
      });
  }

  hasSurplus(): boolean {
    return !!this.reconciliation?.lines.some(line => line.delta > 0);
  }

  returnDescription(draft: SupplierReturnDraft): string {
    return draft.lines.map(line => `${line.sku} ${line.articleDescription} × ${line.quantity}`).join(' · ');
  }

  rowClass(line: SupplierReconciliationLine): string {
    return line.status === 'InLine' ? 'inline' : line.status === 'Shortage' ? 'shortage' : 'surplus';
  }

  rowLabel(status: SupplierReconciliationLine['status']): string {
    return status === 'InLine' ? 'In linea' : status === 'Shortage' ? 'Mancanza' : 'Surplus / extra';
  }

  statusLabel(status: SupplierPurchaseOrder['status']): string {
    return ({ Draft: 'Bozza', Confirmed: 'Confermato', PartiallyReceived: 'Parzialmente ricevuto',
      Completed: 'Completato', ClosedWithDiscrepancies: 'Chiuso con discrepanze' })[status];
  }

  private showError(error: unknown): void {
    const response = error as { error?: { detail?: string; title?: string } };
    this.error = response.error?.detail ?? response.error?.title ?? 'Operazione non riuscita.';
  }

  private downloadBlob(blob: Blob, fileName: string): void {
    const url = URL.createObjectURL(blob);
    const anchor = document.createElement('a');
    anchor.href = url; anchor.download = fileName; anchor.click();
    URL.revokeObjectURL(url);
  }
}
