import { CommonModule } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { Component, OnDestroy, OnInit } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { from, of, Subject } from 'rxjs';
import {
  catchError,
  concatMap,
  debounceTime,
  distinctUntilChanged,
  finalize,
  last,
  map,
  switchMap,
  takeUntil
} from 'rxjs/operators';
import { ContactSummary } from '../../../models/contact.model';
import { ProductDto, VariantSearchDto } from '../../../models/product.model';
import { CreateOrderItemRequest, DraftOrderPreview } from '../../../models/order.model';
import { ContactService } from '../../../services/contact.service';
import { OrderService } from '../../../services/order.service';
import { ProductService } from '../../../services/product.service';
import { BackButtonComponent } from '../../../shared/back-button/back-button.component';
import { ConfirmDeleteModalComponent } from '../../../shared/confirm-delete-modal/confirm-delete-modal.component';
import { PaginationComponent } from '../../../shared/pagination/pagination.component';
import { PageSlicePipe } from '../../../shared/pagination/page-slice.pipe';
import { TableActionButtonComponent } from '../../../shared/table-action-button/table-action-button.component';
import { AddActionButtonComponent } from '../../../shared/add-action-button/add-action-button.component';

interface DraftOrderLine {
  variantId: string;
  sku: string;
  description: string;
  size: string;
  color: string;
  quantity: number;
  unitNetPrice: number;
  vatRate: number;
  natureCode: string | null;
}

interface PreviewState {
  data: DraftOrderPreview | null;
  error: string | null;
}

@Component({
  selector: 'app-ordini-nuovo',
  standalone: true,
  imports: [
    CommonModule, FormsModule, RouterLink, BackButtonComponent, ConfirmDeleteModalComponent,
    PaginationComponent,     PageSlicePipe, TableActionButtonComponent, AddActionButtonComponent
  ],
  templateUrl: './ordini-nuovo.component.html',
  styleUrl: './ordini-nuovo.component.scss'
})
export class OrdiniNuovoComponent implements OnInit, OnDestroy {
  customerSearch = '';
  customerResults: ContactSummary[] = [];
  selectedCustomer: ContactSummary | null = null;
  fulfillmentType: 'StandardShipping' | 'CashDesk' = 'StandardShipping';
  internalNotes = '';
  shippingCost = 0;
  shippingVatRate: number | null = null;
  shippingNatureCode: string | null = null;

  productSearch = '';
  productResults: VariantSearchDto[] = [];
  selectedVariant: VariantSearchDto | null = null;
  selectedProduct: ProductDto | null = null;
  selectedVariantDetails: ProductDto['variants'][number] | null = null;
  pendingQuantity = 1;
  pendingUnitNetPrice = 0;
  pendingNatureCode: string | null = null;

  readonly draftLines: DraftOrderLine[] = [];
  draftLinesPage = 1;
  draftLinesPageSize = 20;
  pendingDraftLineRemovalIndex: number | null = null;
  readonly natureCodes = [
    'N1', 'N2.1', 'N2.2', 'N3.1', 'N3.2', 'N3.3', 'N3.4', 'N3.5', 'N3.6',
    'N4', 'N5', 'N6.1', 'N6.2', 'N6.3', 'N6.4', 'N6.5', 'N6.6', 'N6.7',
    'N6.8', 'N6.9', 'N7'
  ];

  searchingCustomers = false;
  searchingProducts = false;
  loadingVariant = false;
  previewPending = false;
  submitting = false;
  createdOrderId: string | null = null;
  createdOrderNumber: string | null = null;
  errorMessage = '';
  previewError = '';
  preview: DraftOrderPreview | null = null;

  private readonly destroy$ = new Subject<void>();
  private readonly customerSearch$ = new Subject<string>();
  private readonly previewRequests$ = new Subject<CreateOrderItemRequest[]>();

  constructor(
    private readonly contacts: ContactService,
    private readonly products: ProductService,
    private readonly orders: OrderService,
    private readonly router: Router
  ) {}

  ngOnInit(): void {
    this.customerSearch$.pipe(
      debounceTime(250),
      distinctUntilChanged(),
      switchMap(query => {
        this.searchingCustomers = query.length >= 2;
        if (query.length < 2) {
          return of(null);
        }
        return this.contacts.getList({
          search: query,
          isCustomer: true,
          status: 'Active',
          page: 1,
          pageSize: 20
        }).pipe(
          map(result => ({ result, error: null })),
          catchError(error => {
            this.errorMessage = this.describeError(error, 'Impossibile cercare i clienti.');
            return of({ result: null, error });
          }),
          finalize(() => { this.searchingCustomers = false; })
        );
      }),
      takeUntil(this.destroy$)
    ).subscribe(response => {
      this.customerResults = response?.result?.items ?? [];
    });

    this.previewRequests$.pipe(
      debounceTime(200),
      switchMap(items => {
        if (!items.length) {
          return of<PreviewState>({ data: null, error: null });
        }
        return this.orders.previewDraftItems(
          items,
          this.shippingCost,
          this.shippingCost > 0 ? this.shippingVatRate : null,
          this.shippingCost > 0 && this.shippingVatRate === 0 ? this.shippingNatureCode : null
        ).pipe(
          map(data => ({ data, error: null })),
          catchError(error => of<PreviewState>({
            data: null,
            error: this.describeError(error, 'Riepilogo fiscale non disponibile.')
          }))
        );
      }),
      takeUntil(this.destroy$)
    ).subscribe(state => {
      this.preview = state.data;
      this.previewError = state.error ?? '';
      this.previewPending = false;
    });
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  onCustomerSearch(value: string): void {
    if (this.selectedCustomer && value !== this.selectedCustomer.businessName) {
      this.selectedCustomer = null;
    }
    this.customerSearch$.next(value.trim());
  }

  selectCustomer(customer: ContactSummary): void {
    this.selectedCustomer = customer;
    this.customerSearch = customer.businessName;
    this.customerResults = [];
    this.errorMessage = '';
  }

  clearCustomer(): void {
    this.selectedCustomer = null;
    this.customerSearch = '';
    this.customerResults = [];
  }

  searchProducts(): void {
    const query = this.productSearch.trim();
    if (query.length < 2 || this.searchingProducts) {
      return;
    }

    this.searchingProducts = true;
    this.errorMessage = '';
    this.products.searchVariants(query, 30, undefined, true).pipe(
      takeUntil(this.destroy$),
      finalize(() => { this.searchingProducts = false; })
    ).subscribe({
      next: results => { this.productResults = results; },
      error: error => { this.errorMessage = this.describeError(error, 'Impossibile cercare gli articoli.'); }
    });
  }

  selectVariant(variant: VariantSearchDto): void {
    if (this.loadingVariant) {
      return;
    }

    this.loadingVariant = true;
    this.errorMessage = '';
    this.products.getProduct(variant.productId).pipe(
      takeUntil(this.destroy$),
      finalize(() => { this.loadingVariant = false; })
    ).subscribe({
      next: product => {
        const details = product.variants.find(item => item.id === variant.variantId);
        if (!details) {
          this.errorMessage = 'La variante selezionata non è più disponibile.';
          return;
        }

        this.selectedVariant = variant;
        this.selectedProduct = product;
        this.selectedVariantDetails = details;
        this.pendingQuantity = 1;
        this.pendingUnitNetPrice = details.finalNetPrice
          ?? details.listPrice
          ?? details.sellingPrice
          ?? product.sellingPrice
          ?? 0;
        this.pendingNatureCode = null;
        this.productResults = [];
      },
      error: error => { this.errorMessage = this.describeError(error, 'Impossibile caricare i dati della variante.'); }
    });
  }

  clearVariant(): void {
    this.selectedVariant = null;
    this.selectedProduct = null;
    this.selectedVariantDetails = null;
    this.pendingQuantity = 1;
    this.pendingUnitNetPrice = 0;
    this.pendingNatureCode = null;
  }

  addDraftLine(): void {
    const variant = this.selectedVariant;
    const product = this.selectedProduct;
    const details = this.selectedVariantDetails;
    if (!variant || !product || !details) {
      this.errorMessage = 'Seleziona prima una variante articolo.';
      return;
    }
    if (!Number.isInteger(this.pendingQuantity) || this.pendingQuantity < 1) {
      this.errorMessage = 'La quantità deve essere un numero intero maggiore di zero.';
      return;
    }
    if (!Number.isFinite(this.pendingUnitNetPrice) || this.pendingUnitNetPrice < 0) {
      this.errorMessage = 'Inserisci un prezzo unitario valido.';
      return;
    }
    if (product.vatRate === 0 && !this.pendingNatureCode) {
      this.errorMessage = 'Seleziona la Natura IVA per l’articolo con aliquota zero.';
      return;
    }

    this.draftLines.push({
      variantId: variant.variantId,
      sku: variant.sku || variant.barcode,
      description: product.name,
      size: details.size || variant.sizeName || '—',
      color: details.color || variant.colorName || '—',
      quantity: this.pendingQuantity,
      unitNetPrice: this.pendingUnitNetPrice,
      vatRate: product.vatRate,
      natureCode: product.vatRate === 0 ? this.pendingNatureCode : null
    });
    this.draftLinesPage = Math.max(1, Math.ceil(this.draftLines.length / this.draftLinesPageSize));
    this.clearVariant();
    this.productSearch = '';
    this.requestPreview();
    this.errorMessage = '';
  }

  removeDraftLine(index: number): void {
    if (index < 0 || index >= this.draftLines.length) {
      return;
    }
    this.pendingDraftLineRemovalIndex = index;
  }

  confirmRemoveDraftLine(): void {
    const index = this.pendingDraftLineRemovalIndex;
    if (index === null) {
      return;
    }
    this.draftLines.splice(index, 1);
    this.pendingDraftLineRemovalIndex = null;
    this.draftLinesPage = Math.min(
      this.draftLinesPage,
      Math.max(1, Math.ceil(this.draftLines.length / this.draftLinesPageSize))
    );
    this.requestPreview();
  }

  cancelRemoveDraftLine(): void {
    this.pendingDraftLineRemovalIndex = null;
  }

  onDraftLinesPageSizeChange(pageSize: number): void {
    this.draftLinesPageSize = pageSize;
    this.draftLinesPage = 1;
  }

  returnToOrders(): void {
    void this.router.navigate(['/ordini/elenco']);
  }

  requestPreview(): void {
    const items = this.toCreateRequests();
    this.preview = null;
    this.previewError = '';
    this.previewPending = items.length > 0;
    this.previewRequests$.next(items);
  }

  get totalPieces(): number {
    return this.draftLines.reduce((total, line) => total + line.quantity, 0);
  }

  lineSubtotal(index: number): number | null {
    return this.preview?.lines[index]?.lineNetTotal ?? null;
  }

  createDraft(): void {
    if (this.submitting || this.createdOrderId) {
      return;
    }
    if (!this.selectedCustomer || this.draftLines.length === 0) {
      this.errorMessage = 'Seleziona un cliente e aggiungi almeno una riga.';
      return;
    }
    if (this.shippingCost > 0 && this.shippingVatRate === null) {
      this.errorMessage = 'Seleziona l’aliquota IVA della spedizione.';
      return;
    }
    if (this.shippingCost > 0 && this.shippingVatRate === 0 && !this.shippingNatureCode) {
      this.errorMessage = 'Seleziona la Natura IVA della spedizione.';
      return;
    }

    const items = this.toCreateRequests();
    if (items.some(item => !Number.isInteger(item.quantity) || item.quantity < 1)) {
      this.errorMessage = 'Tutte le quantità devono essere numeri interi maggiori di zero.';
      return;
    }

    this.submitting = true;
    this.errorMessage = '';
    this.orders.createOrder({
      contactId: this.selectedCustomer.id,
      orderType: this.fulfillmentType === 'CashDesk' ? 'CassaSession' : 'Standard',
      fulfillmentType: this.fulfillmentType,
      internalNotes: this.internalNotes.trim() || null,
      shippingCost: this.shippingCost,
      shippingVatRate: this.shippingCost > 0 ? this.shippingVatRate : null,
      shippingNatureCode: this.shippingCost > 0 && this.shippingVatRate === 0 ? this.shippingNatureCode : null
    }).pipe(
      switchMap(order => {
        this.createdOrderId = order.id;
        this.createdOrderNumber = order.orderNumber;
        return from(items).pipe(
          concatMap(item => this.orders.addItem(order.id, item)),
          last(),
          map(() => order)
        );
      }),
      takeUntil(this.destroy$),
      finalize(() => { this.submitting = false; })
    ).subscribe({
      next: order => {
        void this.router.navigate(['/vendite/ordini', order.id], {
          replaceUrl: true,
          state: { successMessage: `Bozza ordine ${order.orderNumber} creata correttamente.` }
        });
      },
      error: error => {
        const detail = this.describeError(error, 'Impossibile completare la creazione dell’ordine.');
        this.errorMessage = this.createdOrderId
          ? `La bozza ${this.createdOrderNumber} è stata creata, ma non tutte le righe sono state salvate. ${detail}`
          : detail;
      }
    });
  }

  private toCreateRequests(): CreateOrderItemRequest[] {
    return this.draftLines.map(line => ({
      variantId: line.variantId,
      quantity: line.quantity,
      unitNetPrice: line.unitNetPrice,
      vatRate: line.vatRate,
      natureCode: line.vatRate === 0 ? line.natureCode : null,
      discountPercent: 0
    }));
  }

  private describeError(error: unknown, fallback: string): string {
    if (error instanceof HttpErrorResponse) {
      if (typeof error.error === 'string' && error.error.trim()) {
        return error.error.trim();
      }
      if (error.error && typeof error.error === 'object' && typeof error.error.detail === 'string') {
        return error.error.detail;
      }
    }
    return fallback;
  }
}
