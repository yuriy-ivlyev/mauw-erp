import { Component, DestroyRef, OnDestroy, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { HttpErrorResponse } from '@angular/common/http';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { DomSanitizer, SafeResourceUrl } from '@angular/platform-browser';
import { finalize } from 'rxjs/operators';
import { SupplierDeliveryNoteService } from '../../services/supplier-delivery-note.service';
import { SupplierPurchaseOrderService } from '../../services/supplier-purchase-order.service';
import { ContactService } from '../../services/contact.service';
import { WarehouseService } from '../../services/warehouse.service';
import { ProductService } from '../../services/product.service';
import { ConfirmDeleteModalComponent } from '../../shared/confirm-delete-modal/confirm-delete-modal.component';
import { ContactSummary } from '../../models/contact.model';
import { WarehouseSummary } from '../../models/warehouse.model';
import { VariantSearchDto } from '../../models/product.model';
import { SupplierDeliveryNote, SupplierDeliveryNoteLine, SaveSupplierDeliveryNoteLines } from '../../models/supplier-delivery-note.model';
import { SupplierPurchaseOrder } from '../../models/supplier-purchase-order.model';

@Component({
  selector: 'app-carico-merce-wizard',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink, ConfirmDeleteModalComponent],
  templateUrl: './carico-merce-wizard.component.html',
  styles: [`
    :host { display: block; }
    input, select, textarea { border: 1px solid #d1d5db; border-radius: .4rem; padding: .5rem; width: 100%; color: #111827; background: white; }
    th, td { padding: .6rem; text-align: left; vertical-align: top; }
    button:disabled { opacity: .45; cursor: not-allowed; }
    .action { padding: .55rem .9rem; border: 1px solid #d1d5db; border-radius: .4rem; }
    .primary { background: #465fff; color: white; border-color: #465fff; }
  `]
})
export class CaricoMerceWizardComponent implements OnInit, OnDestroy {
  private readonly destroyRef = inject(DestroyRef);
  suppliers: ContactSummary[] = [];
  purchaseOrders: SupplierPurchaseOrder[] = [];
  purchaseOrderId = '';
  warehouses: WarehouseSummary[] = [];
  selected: SupplierDeliveryNote | null = null;
  activeStep = 1;
  busy = false;
  dirty = false;
  error = '';
  success = '';
  supplierSearch = '';
  supplierId = '';
  warehouseId = '';
  documentNumber = '';
  documentDate = new Date().toLocaleDateString('en-CA');
  file: File | null = null;
  variantQuery = '';
  variants: VariantSearchDto[] = [];
  matchingLine: SupplierDeliveryNoteLine | null = null;
  deleteConfirmationOpen = false;
  private lineToRemove: number | null = null;
  ocrJson = '';
  documentPreview: SafeResourceUrl | null = null;
  previewLoading = false;
  private documentPreviewObjectUrl: string | null = null;
  readonly labels: Record<string, string> = {
    Draft: 'Bozza', Parsing: 'In elaborazione', Parsed: 'Elaborata', Confirmed: 'Confermata', Cancelled: 'Annullata',
    Matched: 'Associata', NewArticleNeeded: 'Da associare', Error: 'Errore'
  };

  constructor(
    private readonly service: SupplierDeliveryNoteService,
    private readonly purchaseOrderService: SupplierPurchaseOrderService,
    private readonly contacts: ContactService,
    private readonly warehouseService: WarehouseService,
    private readonly products: ProductService,
    private readonly route: ActivatedRoute,
    private readonly router: Router,
    private readonly sanitizer: DomSanitizer
  ) {}

  ngOnInit(): void {
    this.route.paramMap.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(params => {
      const id = params.get('id');
      if (id) this.loadNote(id);
      else {
        if (this.documentPreviewObjectUrl) URL.revokeObjectURL(this.documentPreviewObjectUrl);
        this.documentPreviewObjectUrl = null;
        this.documentPreview = null;
        this.selected = null;
        this.activeStep = 1;
        this.dirty = false;
      }
    });
    this.searchSuppliers();
    this.purchaseOrderService.list(undefined, true).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: orders => {
        this.purchaseOrders = orders;
        const id = this.route.snapshot.queryParamMap.get('purchaseOrderId');
        if (id) {
          this.purchaseOrderId = id;
          this.syncSupplierFromOrder();
        }
      },
      error: error => this.showError(error)
    });
    this.warehouseService.getList({ page: 1, pageSize: 100, isActive: true })
      .pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
        next: result => {
          this.warehouses = result.items;
          this.warehouseId = result.items.find(w => w.isDefault)?.id ?? '';
        },
        error: error => this.showError(error)
      });
  }

  ngOnDestroy(): void {
    if (this.documentPreviewObjectUrl) URL.revokeObjectURL(this.documentPreviewObjectUrl);
  }

  get editable(): boolean {
    return this.selected?.status === 'Draft' || this.selected?.status === 'Parsed';
  }

  get canConfirm(): boolean {
    return this.editable && !this.dirty && !!this.selected?.documentNumber.trim()
      && !!this.selected.lines.length
      && this.selected.lines.every(l => !!l.articleVariantId && l.lineStatus === 'Matched' && Number.isInteger(l.quantity) && l.quantity > 0);
  }

  pieceCount(): number {
    return this.selected?.lines.reduce((total, line) => total + line.quantity, 0) ?? 0;
  }

  private loadNote(id: string): void {
    this.busy = true;
    this.service.get(id).pipe(takeUntilDestroyed(this.destroyRef), finalize(() => this.busy = false)).subscribe({
      next: note => {
        this.accept(note);
        const prefillOrderId = this.route.snapshot.queryParamMap.get('purchaseOrderId');
        if (note.supplierPurchaseOrderId && note.lines.length === 0
            && prefillOrderId === note.supplierPurchaseOrderId) {
          this.busy = true;
          this.purchaseOrderService.get(note.supplierPurchaseOrderId)
            .pipe(takeUntilDestroyed(this.destroyRef), finalize(() => this.busy = false)).subscribe({
              next: order => {
                if (!this.selected || this.selected.id !== note.id) return;
                this.selected.lines = order.lines.filter(line => line.quantityOrdered > line.quantityReceived)
                  .map(line => ({
                    id: '', supplierItemCode: '', articleVariantId: line.articleVariantId,
                    articleDescription: line.articleDescription, supplierPurchaseOrderLineId: line.id,
                    description: line.articleDescription,
                    quantity: line.quantityOrdered - line.quantityReceived,
                    unitPrice: line.unitCost, lineStatus: 'Matched', stockMovementId: null
                  }));
                this.dirty = true;
                void this.router.navigate([], {
                  relativeTo: this.route,
                  queryParams: { purchaseOrderId: null },
                  queryParamsHandling: 'merge',
                  replaceUrl: true
                });
              },
              error: error => this.showError(error)
            });
        }
        this.loadDocumentPreview(id);
        this.activeStep = note.status === 'Confirmed' || note.status === 'Cancelled' ? 3 : 2;
      },
      error: error => this.showError(error)
    });
  }

  goBack(): void {
    if (this.dirty && !window.confirm('Scartare le modifiche non salvate?')) return;
    void this.router.navigate(['/acquisti/ddt-fornitore']);
  }

  previousStep(): void {
    if (this.activeStep > 1) this.activeStep--;
  }

  review(): void {
    if (!this.selected || this.busy) return;
    if (this.dirty) {
      this.save(false, true);
      return;
    }
    this.activeStep = 3;
  }

  searchSuppliers(): void {
    this.contacts.getList({ isSupplier: true, status: 'Active', search: this.supplierSearch, page: 1, pageSize: 30 })
      .pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
        next: result => this.suppliers = result.items, error: error => this.showError(error)
      });
  }

  syncSupplierFromOrder(): void {
    const order = this.purchaseOrders.find(x => x.id === this.purchaseOrderId);
    if (order) this.supplierId = order.supplierId;
  }

  supplierChanged(): void {
    if (this.purchaseOrders.find(x => x.id === this.purchaseOrderId)?.supplierId !== this.supplierId)
      this.purchaseOrderId = '';
  }

  chooseFile(event: Event): void {
    if (event.target instanceof HTMLInputElement) this.file = event.target.files?.[0] ?? null;
  }

  upload(): void {
    this.error = ''; this.success = '';
    if (!this.file || !this.supplierId || !this.warehouseId || !this.documentDate) {
      this.error = 'Selezionare fornitore, magazzino, data e documento.'; return;
    }
    if (this.file.size > 20 * 1024 * 1024) { this.error = 'Il documento supera 20 MB.'; return; }
    const data = new FormData();
    data.append('supplierId', this.supplierId); data.append('warehouseId', this.warehouseId);
    data.append('documentNumber', this.documentNumber);
    data.append('documentDate', `${this.documentDate}T00:00:00Z`);
    if (this.purchaseOrderId) data.append('supplierPurchaseOrderId', this.purchaseOrderId);
    data.append('file', this.file);
    this.busy = true;
    this.service.upload(data).pipe(takeUntilDestroyed(this.destroyRef), finalize(() => this.busy = false)).subscribe({
      next: note => {
        void this.router.navigate(['/acquisti/ddt-fornitore', note.id], {
          queryParams: this.purchaseOrderId ? { purchaseOrderId: this.purchaseOrderId } : {}
        });
      },
      error: error => this.showError(error)
    });
  }

  close(): void {
    this.goBack();
  }

  addLine(): void {
    this.selected?.lines.push({
      id: '', supplierItemCode: '', articleVariantId: null, articleDescription: null,
      supplierPurchaseOrderLineId: null, description: '', quantity: 1,
      unitPrice: '0.00', lineStatus: 'NewArticleNeeded', stockMovementId: null
    });
    this.dirty = true;
  }

  removeLine(index: number): void {
    this.lineToRemove = index;
    this.deleteConfirmationOpen = true;
  }

  confirmRemoveLine(): void {
    if (this.lineToRemove === null) return;
    this.selected?.lines.splice(this.lineToRemove, 1);
    this.matchingLine = null;
    this.dirty = true;
    this.cancelRemoveLine();
  }

  cancelRemoveLine(): void {
    this.deleteConfirmationOpen = false;
    this.lineToRemove = null;
  }

  searchVariants(): void {
    this.products.searchVariants(this.variantQuery, 30).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: result => this.variants = result, error: error => this.showError(error)
    });
  }

  supplierCodeChanged(line: SupplierDeliveryNoteLine): void {
    line.articleVariantId = null;
    line.supplierPurchaseOrderLineId = null;
    line.articleDescription = null;
    line.lineStatus = 'NewArticleNeeded';
    this.dirty = true;
  }

  findArticle(line: SupplierDeliveryNoteLine): void {
    if (!this.selected || !this.editable || this.busy) return;
    const code = line.supplierItemCode.trim();
    if (!code) {
      this.error = 'Inserire il codice prodotto fornitore da cercare.';
      return;
    }
    const note = this.selected;
    this.error = ''; this.success = '';
    this.busy = true;
    this.service.findArticle(note.id, code)
      .pipe(takeUntilDestroyed(this.destroyRef), finalize(() => this.busy = false)).subscribe({
        next: article => {
          if (this.selected !== note || !note.lines.includes(line) || line.supplierItemCode.trim() !== code) return;
          line.articleVariantId = article.articleVariantId;
          line.articleDescription = article.articleDescription;
          line.lineStatus = 'Matched';
          if (!line.description.trim()) line.description = article.articleDescription;
          this.dirty = true;
          this.matchingLine = null;
          this.variants = [];
          this.success = 'Articolo trovato e associato alla riga. Salvare la bozza per confermare la modifica.';
        },
        error: error => this.showError(error)
      });
  }

  match(variant: VariantSearchDto): void {
    if (!this.matchingLine) return;
    if (this.matchingLine.articleVariantId !== variant.variantId)
      this.matchingLine.supplierPurchaseOrderLineId = null;
    this.matchingLine.articleVariantId = variant.variantId;
    this.matchingLine.articleDescription = [
      variant.productName,
      [variant.colorName, variant.sizeName].filter(Boolean).join(' / ')
    ].filter(Boolean).join(' - ');
    this.matchingLine.lineStatus = 'Matched';
    this.dirty = true; this.matchingLine = null; this.variants = [];
  }

  save(parsed = false, advance = false): void {
    if (!this.selected) return;
    const request: SaveSupplierDeliveryNoteLines = {
      version: this.selected.version, documentNumber: this.selected.documentNumber,
      documentDate: this.selected.documentDate,
      lines: this.selected.lines.map(l => ({
        supplierItemCode: l.supplierItemCode, articleVariantId: l.articleVariantId,
        supplierPurchaseOrderLineId: l.supplierPurchaseOrderLineId,
        description: l.description, quantity: l.quantity,
        unitPrice: typeof l.unitPrice === 'string' ? l.unitPrice.trim().replace(',', '.') : l.unitPrice
      }))
    };
    if (request.lines.some(l => !Number.isInteger(l.quantity) || l.quantity <= 0)) {
      this.error = 'Le quantita devono essere intere e positive.'; return;
    }
    this.busy = true;
    this.service.save(this.selected.id, request, parsed)
      .pipe(takeUntilDestroyed(this.destroyRef), finalize(() => this.busy = false)).subscribe({
        next: note => {
          this.accept(note);
          this.activeStep = advance ? 3 : this.activeStep;
          this.success = parsed ? 'Righe acquisite e associazioni aggiornate.' : 'Bozza salvata.';
        },
        error: error => this.showError(error)
      });
  }

  importOcr(): void {
    if (!this.selected) return;
    let value: unknown;
    try { value = JSON.parse(this.ocrJson); }
    catch { this.error = 'JSON OCR non valido.'; return; }
    if (!this.isOcrPayload(value)) { this.error = 'Il payload deve contenere lines con codice, descrizione, quantita intera e prezzo.'; return; }
    this.selected.documentNumber = value.documentNumber ?? this.selected.documentNumber;
    this.selected.lines = value.lines.map(l => ({
      ...l, id: '', articleVariantId: null, articleDescription: null,
      supplierPurchaseOrderLineId: null, lineStatus: 'NewArticleNeeded', stockMovementId: null
    }));
    this.dirty = true;
    this.save(true);
  }

  action(action: 'confirm' | 'cancel'): void {
    if (!this.selected || !window.confirm(action === 'confirm'
      ? 'Confermare il DDT e caricare la merce? Non sara piu modificabile.'
      : 'Annullare questa bozza?')) return;
    this.busy = true;
    this.service.action(this.selected.id, this.selected.version, action)
      .pipe(takeUntilDestroyed(this.destroyRef), finalize(() => this.busy = false)).subscribe({
        next: note => {
          this.accept(note);
          if (action === 'confirm') this.activeStep = 3;
          else void this.router.navigate(['/acquisti/ddt-fornitore']);
          this.success = action === 'confirm' ? 'DDT confermato. Movimenti e giacenze aggiornati.' : 'Bozza annullata.';
        }, error: error => this.showError(error)
      });
  }

  download(): void {
    if (!this.selected) return;
    const id = this.selected.id;
    this.service.download(id).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: blob => {
        const url = URL.createObjectURL(blob);
        const link = document.createElement('a');
        link.href = url;
        link.download = `DDT-${id}.${blob.type === 'application/pdf' ? 'pdf' : blob.type === 'image/png' ? 'png' : 'jpg'}`;
        link.click(); setTimeout(() => URL.revokeObjectURL(url), 1000);
      }, error: error => this.showError(error)
    });
  }

  private loadDocumentPreview(id: string): void {
    this.previewLoading = true;
    this.service.download(id).pipe(takeUntilDestroyed(this.destroyRef), finalize(() => this.previewLoading = false))
      .subscribe({
        next: blob => {
          if (this.documentPreviewObjectUrl) URL.revokeObjectURL(this.documentPreviewObjectUrl);
          this.documentPreviewObjectUrl = URL.createObjectURL(blob);
          this.documentPreview = this.sanitizer.bypassSecurityTrustResourceUrl(this.documentPreviewObjectUrl);
        },
        error: error => this.showError(error)
      });
  }

  private accept(note: SupplierDeliveryNote): void {
    this.selected = note; this.dirty = false; this.error = ''; this.success = '';
    this.matchingLine = null; this.variants = []; this.ocrJson = '';
  }

  private showError(error: unknown): void {
    this.success = '';
    if (error instanceof HttpErrorResponse && error.error && typeof error.error.detail === 'string') {
      this.error = error.error.detail;
    } else {
      this.error = 'Operazione non riuscita. Verificare connessione e autorizzazioni, poi riprovare.';
    }
  }

  private isOcrPayload(value: unknown): value is {
    documentNumber?: string;
    lines: { supplierItemCode: string; description: string; quantity: number; unitPrice: number | string }[];
  } {
    if (typeof value !== 'object' || value === null || !('lines' in value) || !Array.isArray(value.lines)) return false;
    if ('documentNumber' in value && typeof value.documentNumber !== 'string') return false;
    return value.lines.every((l: unknown) => typeof l === 'object' && l !== null
      && 'supplierItemCode' in l && typeof l.supplierItemCode === 'string'
      && 'description' in l && typeof l.description === 'string'
      && 'quantity' in l && typeof l.quantity === 'number' && Number.isInteger(l.quantity)
      && 'unitPrice' in l && (typeof l.unitPrice === 'number' || typeof l.unitPrice === 'string'));
  }
}
