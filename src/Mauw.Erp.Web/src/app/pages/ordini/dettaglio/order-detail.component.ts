import { CommonModule } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { Component, OnDestroy, OnInit } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import {
  AddCompletedOrderItemRequest,
  CreateOrderItemRequest,
  Order,
  OrderActionResponse,
  OrderItem,
  OrderPermittedAction
} from '../../../models/order.model';
import { ProductDto, VariantSearchDto } from '../../../models/product.model';
import { AuthService } from '../../../services/auth.service';
import { InvoiceService } from '../../../services/invoice.service';
import { OrderService } from '../../../services/order.service';
import { BackButtonComponent } from '../../../shared/back-button/back-button.component';
import { PaginationComponent } from '../../../shared/pagination/pagination.component';
import { PageSlicePipe } from '../../../shared/pagination/page-slice.pipe';
import { TableActionButtonComponent } from '../../../shared/table-action-button/table-action-button.component';
import { AddActionButtonComponent } from '../../../shared/add-action-button/add-action-button.component';
import { ProductService } from '../../../services/product.service';
import { Subject } from 'rxjs';
import { finalize, takeUntil } from 'rxjs/operators';

interface DraftOrderItemEdit {
  quantity: number;
  unitNetPrice: number;
  vatRate: number;
  natureCode: string | null;
  discountPercent: number;
}

@Component({
  selector: 'app-order-detail',
  standalone: true,
  imports: [
    CommonModule, FormsModule, RouterLink, BackButtonComponent, PaginationComponent,
    PageSlicePipe, TableActionButtonComponent, AddActionButtonComponent
  ],
  templateUrl: './order-detail.component.html',
  styleUrl: './order-detail.component.scss'
})
export class OrderDetailComponent implements OnInit, OnDestroy {
  order: Order | null = null;
  loading = true;
  submitting = false;
  downloadingPdf: string | null = null;
  generatingInvoice = false;
  searchingBillingProducts = false;
  addingBillingItem = false;
  removingBillingItem = false;
  billingProductSearch = '';
  billingProductResults: VariantSearchDto[] = [];
  selectedBillingVariant: VariantSearchDto | null = null;
  selectedBillingProduct: ProductDto | null = null;
  billingQuantity = 1;
  billingNatureCode: string | null = null;
  newItemUnitNetPrice = 0;
  pendingDraftRemoval: OrderItem | null = null;
  removingDraftItem = false;
  pendingBillingRemoval: OrderItem | null = null;
  savingShippingCharge = false;
  savingItemId: string | null = null;
  itemsPage = 1;
  itemsPageSize = 20;
  errorMessage = '';
  successMessage = '';
  returnUrl = '/ordini/elenco';
  dialogAction: OrderPermittedAction | null = null;
  readonly draftEdits: Record<string, DraftOrderItemEdit> = {};
  readonly shipmentQuantities: Record<string, number> = {};
  readonly carriers = ['Ritiro Cliente', 'Vettore Proprio', 'GLS', 'BRT', 'DHL'];
  shippingCostEdit = 0;
  shippingVatRateEdit: number | null = null;
  shippingNatureCodeEdit: string | null = null;
  readonly natureCodes = [
    'N1', 'N2.1', 'N2.2', 'N3.1', 'N3.2', 'N3.3', 'N3.4', 'N3.5', 'N3.6',
    'N4', 'N5', 'N6.1', 'N6.2', 'N6.3', 'N6.4', 'N6.5', 'N6.6', 'N6.7',
    'N6.8', 'N6.9', 'N7'
  ];

  reason = '';
  carrierName = '';
  trackingCode = '';
  boxCount = 1;
  weightKg = 0;
  paymentMethod = 'Contanti';
  transactionDetails = '';

  private readonly destroy$ = new Subject<void>();

  constructor(
    private readonly route: ActivatedRoute,
    private readonly orderService: OrderService,
    private readonly router: Router,
    private readonly invoiceService: InvoiceService,
    private readonly products: ProductService,
    private readonly auth: AuthService
  ) {}

  ngOnInit(): void {
    const navigationState = this.router.getCurrentNavigation()?.extras.state;
    const successMessage = navigationState?.['successMessage'] ?? history.state?.successMessage;
    if (typeof successMessage === 'string') {
      this.successMessage = successMessage;
    }

    this.route.queryParamMap.pipe(takeUntil(this.destroy$)).subscribe(params => {
      this.returnUrl = this.safeReturnUrl(params.get('returnTo')) ?? '/ordini/elenco';
    });

    this.route.paramMap.pipe(takeUntil(this.destroy$)).subscribe(params => {
      const id = params.get('id');
      if (id) {
        this.loadOrder(id);
      } else {
        this.loading = false;
        this.errorMessage = 'Identificativo ordine non valido.';
      }
    });
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  goBack(): void {
    void this.router.navigateByUrl(this.returnUrl);
  }

  get canManageBilling(): boolean {
    return this.auth.hasRole('Admin') || this.auth.hasRole('Sales') || this.auth.hasRole('Accounting');
  }

  get returnToUrl(): string {
    return this.router.url;
  }

  canEditCompletedInvoice(order: Order): boolean {
    return this.canManageBilling && order.canEditInvoice === true;
  }

  canAddDraftOrderItem(order: Order): boolean {
    return order.status === 'Draft' && !order.isReadOnly &&
      (this.auth.hasRole('Admin') || this.auth.hasRole('Sales'));
  }

  generateInvoice(): void {
    const order = this.order;
    if (!order || order.status !== 'Completed' || !order.canEditInvoice ||
        order.linkedDocuments?.invoiceId || this.generatingInvoice || !this.canManageBilling) {
      return;
    }

    this.generatingInvoice = true;
    this.errorMessage = '';
    this.invoiceService.generateFromOrder(order.id).pipe(
      takeUntil(this.destroy$),
      finalize(() => { this.generatingInvoice = false; })
    ).subscribe({
      next: invoice => {
        void this.router.navigate(['/fatturazione/fatture', invoice.id], {
          queryParams: { returnTo: this.router.url },
          state: { successMessage: `Fattura ${invoice.invoiceNumber} generata dall'ordine.` }
        });
      },
      error: error => { this.errorMessage = this.describeError(error, 'Impossibile generare la fattura.'); }
    });
  }

  searchBillingProducts(): void {
    const query = this.billingProductSearch.trim();
    if (query.length < 2 || this.searchingBillingProducts) {
      return;
    }
    this.searchingBillingProducts = true;
    this.errorMessage = '';
    const availableOnly = this.order?.status === 'Draft';
    this.products.searchVariants(query, 30, undefined, availableOnly).pipe(
      takeUntil(this.destroy$),
      finalize(() => { this.searchingBillingProducts = false; })
    ).subscribe({
      next: results => { this.billingProductResults = results; },
      error: error => { this.errorMessage = this.describeError(error, 'Impossibile cercare gli articoli.'); }
    });
  }

  selectBillingVariant(variant: VariantSearchDto): void {
    this.products.getProduct(variant.productId).pipe(takeUntil(this.destroy$)).subscribe({
      next: product => {
        const details = product.variants.find(item => item.id === variant.variantId);
        if (!details) {
          this.errorMessage = 'La variante selezionata non è più disponibile.';
          return;
        }
        this.selectedBillingVariant = variant;
        this.selectedBillingProduct = product;
        this.billingQuantity = 1;
        this.newItemUnitNetPrice = details.finalNetPrice
          ?? details.listPrice
          ?? details.sellingPrice
          ?? product.sellingPrice
          ?? 0;
        this.billingNatureCode = null;
        this.billingProductResults = [];
      },
      error: error => { this.errorMessage = this.describeError(error, 'Impossibile caricare la variante.'); }
    });
  }

  addBillingItem(): void {
    const order = this.order;
    const variant = this.selectedBillingVariant;
    const product = this.selectedBillingProduct;
    if (!order || !variant || !product || !this.canEditCompletedInvoice(order) || this.addingBillingItem) {
      return;
    }
    if (!Number.isInteger(this.billingQuantity) || this.billingQuantity < 1) {
      this.errorMessage = 'La quantità deve essere un numero intero maggiore di zero.';
      return;
    }
    if (product.vatRate === 0 && !this.billingNatureCode) {
      this.errorMessage = 'Seleziona la Natura IVA per l’articolo con aliquota zero.';
      return;
    }

    const request: AddCompletedOrderItemRequest = {
      variantId: variant.variantId,
      quantity: this.billingQuantity,
      natureCode: product.vatRate === 0 ? this.billingNatureCode : null
    };
    this.addingBillingItem = true;
    this.errorMessage = '';
    this.orderService.addBillingItem(order.id, request).pipe(
      takeUntil(this.destroy$),
      finalize(() => { this.addingBillingItem = false; })
    ).subscribe({
      next: () => {
        this.successMessage = 'Riga contabile aggiunta e fattura sincronizzata.';
        this.clearBillingProduct();
        this.loadOrder(order.id);
      },
      error: error => { this.errorMessage = this.describeError(error, 'Impossibile aggiungere la riga contabile.'); }
    });
  }

  addDraftItem(): void {
    const order = this.order;
    const variant = this.selectedBillingVariant;
    const product = this.selectedBillingProduct;
    if (!order || !variant || !product || !this.canAddDraftOrderItem(order) || this.addingBillingItem) {
      return;
    }
    if (!Number.isInteger(this.billingQuantity) || this.billingQuantity < 1) {
      this.errorMessage = 'La quantità deve essere un numero intero maggiore di zero.';
      return;
    }
    if (!Number.isFinite(this.newItemUnitNetPrice) || this.newItemUnitNetPrice < 0) {
      this.errorMessage = 'Inserisci un prezzo unitario valido.';
      return;
    }
    if (product.vatRate === 0 && !this.billingNatureCode) {
      this.errorMessage = 'Seleziona la Natura IVA per l’articolo con aliquota zero.';
      return;
    }

    this.addingBillingItem = true;
    this.errorMessage = '';
    this.orderService.addItem(order.id, {
      variantId: variant.variantId,
      quantity: this.billingQuantity,
      unitNetPrice: this.newItemUnitNetPrice,
      vatRate: product.vatRate,
      natureCode: product.vatRate === 0 ? this.billingNatureCode : null,
      discountPercent: 0
    }).pipe(
      takeUntil(this.destroy$),
      finalize(() => { this.addingBillingItem = false; })
    ).subscribe({
      next: () => {
        this.successMessage = 'Articolo aggiunto all’ordine.';
        this.clearBillingProduct();
        this.loadOrder(order.id);
      },
      error: error => { this.errorMessage = this.describeError(error, 'Impossibile aggiungere l’articolo all’ordine.'); }
    });
  }

  askRemoveBillingItem(item: OrderItem): void {
    if (item.isExcludedFromInvoice || !this.order || !this.canEditCompletedInvoice(this.order)) {
      return;
    }
    this.pendingBillingRemoval = item;
  }

  cancelRemoveBillingItem(): void {
    if (!this.removingBillingItem) {
      this.pendingBillingRemoval = null;
    }
  }

  confirmRemoveBillingItem(): void {
    const order = this.order;
    const item = this.pendingBillingRemoval;
    if (!order || !item || this.removingBillingItem) {
      return;
    }
    this.removingBillingItem = true;
    this.errorMessage = '';
    this.orderService.removeBillingItem(order.id, item.id).pipe(
      takeUntil(this.destroy$),
      finalize(() => { this.removingBillingItem = false; })
    ).subscribe({
      next: updated => {
        this.order = updated;
        this.pendingBillingRemoval = null;
        this.successMessage = 'Riga esclusa e fattura sincronizzata.';
        this.loadOrder(order.id);
      },
      error: error => { this.errorMessage = this.describeError(error, 'Impossibile rimuovere la riga dalla fattura.'); }
    });
  }

  private clearBillingProduct(): void {
    this.billingProductSearch = '';
    this.billingProductResults = [];
    this.selectedBillingVariant = null;
    this.selectedBillingProduct = null;
    this.billingQuantity = 1;
    this.billingNatureCode = null;
    this.newItemUnitNetPrice = 0;
  }

  loadOrder(id = this.order?.id): void {
    if (!id) {
      return;
    }

    this.loading = true;
    this.errorMessage = '';
    this.orderService.getById(id).pipe(
      takeUntil(this.destroy$),
      finalize(() => { this.loading = false; })
    ).subscribe({
      next: order => {
        this.order = order;
        this.itemsPage = Math.min(this.itemsPage, Math.max(1, Math.ceil(order.items.length / this.itemsPageSize)));
        this.shippingCostEdit = order.shippingCost ?? 0;
        this.shippingVatRateEdit = order.shippingVatRate ?? null;
        this.shippingNatureCodeEdit = order.shippingNatureCode ?? null;
        for (const item of order.items) {
          this.draftEdits[item.id] = {
            quantity: item.quantity,
            unitNetPrice: item.unitNetPrice,
            vatRate: item.vatRate,
            natureCode: item.natureCode,
            discountPercent: item.discountPercent
          };
          this.shipmentQuantities[item.id] = item.remainingQuantity;
        }
      },
      error: error => { this.errorMessage = this.describeError(error, 'Impossibile caricare l’ordine.'); }
    });
  }

  openAction(action: OrderPermittedAction): void {
    this.errorMessage = '';
    this.successMessage = '';
    if (!action.requiresDialog) {
      this.executeAction(action);
      return;
    }

    this.dialogAction = action;
    this.reason = '';
    this.carrierName = '';
    this.trackingCode = '';
    this.boxCount = 1;
    this.weightKg = 0;
    this.paymentMethod = 'Contanti';
    this.transactionDetails = '';
    for (const item of this.order?.items ?? []) {
      this.shipmentQuantities[item.id] = item.remainingQuantity;
    }
  }

  closeDialog(): void {
    if (!this.submitting) {
      this.dialogAction = null;
    }
  }

  submitDialog(): void {
    const action = this.dialogAction;
    if (!action) {
      return;
    }

    let payload: unknown;
    switch (action.dialogType) {
      case 'CancelReason':
        payload = { reason: this.reason.trim() };
        break;
      case 'ShipmentDetails':
        payload = {
          carrierName: this.carrierName.trim(),
          trackingCode: this.trackingCode.trim() || null,
          boxCount: this.boxCount,
          weightKg: this.weightKg || null,
          items: (this.order?.items ?? [])
            .filter(item => item.remainingQuantity > 0 && this.shipmentQuantities[item.id] > 0)
            .map(item => ({ orderItemId: item.id, quantity: this.shipmentQuantities[item.id] }))
        };
        break;
      case 'PaymentDetails':
        payload = {
          paymentMethod: this.paymentMethod,
          transactionDetails: this.transactionDetails.trim() || null
        };
        break;
      default:
        this.errorMessage = 'Tipo di dialogo non supportato.';
        return;
    }

    this.executeAction(action, payload);
  }

  saveItem(item: OrderItem): void {
    if (!this.order || this.order.isReadOnly || this.savingItemId !== null) {
      return;
    }

    const edit = this.draftEdits[item.id];
    this.errorMessage = '';
    this.savingItemId = item.id;
    this.orderService.updateItem(this.order.id, item.id, {
      variantId: item.variantId,
      quantity: edit.quantity,
      unitNetPrice: edit.unitNetPrice,
      vatRate: edit.vatRate,
      natureCode: edit.natureCode,
      discountPercent: edit.discountPercent
    }).pipe(
      takeUntil(this.destroy$),
      finalize(() => { this.savingItemId = null; })
    ).subscribe({
      next: order => {
        this.order = order;
        this.successMessage = 'Riga aggiornata correttamente.';
        this.loadOrder(order.id);
      },
      error: error => { this.errorMessage = this.describeError(error, 'Impossibile aggiornare la riga.'); }
    });
  }

  askRemoveDraftItem(item: OrderItem): void {
    if (!this.order || !this.canAddDraftOrderItem(this.order) || this.removingDraftItem) {
      return;
    }
    this.pendingDraftRemoval = item;
  }

  cancelRemoveDraftItem(): void {
    if (!this.removingDraftItem) {
      this.pendingDraftRemoval = null;
    }
  }

  confirmRemoveDraftItem(): void {
    const order = this.order;
    const item = this.pendingDraftRemoval;
    if (!order || !item || !this.canAddDraftOrderItem(order) || this.removingDraftItem) {
      return;
    }

    this.removingDraftItem = true;
    this.errorMessage = '';
    this.orderService.removeItem(order.id, item.id).pipe(
      takeUntil(this.destroy$),
      finalize(() => { this.removingDraftItem = false; })
    ).subscribe({
      next: updated => {
        this.order = updated;
        this.pendingDraftRemoval = null;
        this.successMessage = 'Articolo eliminato dall’ordine.';
        this.loadOrder(order.id);
      },
      error: error => { this.errorMessage = this.describeError(error, 'Impossibile eliminare l’articolo dall’ordine.'); }
    });
  }

  saveShippingCharge(): void {
    if (!this.order ||
        (this.order.isReadOnly && !this.canEditCompletedInvoice(this.order)) ||
        this.savingShippingCharge) {
      return;
    }
    if (this.shippingCostEdit > 0 && this.shippingVatRateEdit === null) {
      this.errorMessage = 'Seleziona l’aliquota IVA della spedizione.';
      return;
    }
    if (this.shippingCostEdit > 0 && this.shippingVatRateEdit === 0 && !this.shippingNatureCodeEdit) {
      this.errorMessage = 'Seleziona la Natura IVA della spedizione.';
      return;
    }

    this.errorMessage = '';
    this.savingShippingCharge = true;
    const request = {
      shippingCost: this.shippingCostEdit,
      vatRate: this.shippingCostEdit > 0 ? this.shippingVatRateEdit : null,
      natureCode: this.shippingCostEdit > 0 && this.shippingVatRateEdit === 0
        ? this.shippingNatureCodeEdit
        : null
    };
    const update = this.order.status === 'Completed'
      ? this.orderService.updateCompletedInvoiceShippingCharge(this.order.id, request)
      : this.orderService.updateShippingCharge(this.order.id, request);
    update.pipe(
      takeUntil(this.destroy$),
      finalize(() => { this.savingShippingCharge = false; })
    ).subscribe({
      next: order => {
        this.order = order;
        this.shippingCostEdit = order.shippingCost ?? 0;
        this.shippingVatRateEdit = order.shippingVatRate ?? null;
        this.shippingNatureCodeEdit = order.shippingNatureCode ?? null;
        this.successMessage = 'Costo di spedizione aggiornato.';
      },
      error: error => { this.errorMessage = this.describeError(error, 'Impossibile aggiornare il costo di spedizione.'); }
    });
  }

  getDialogTitle(action: OrderPermittedAction): string {
    const titles: Record<string, string> = {
      CancelReason: 'Motivo dell’operazione',
      ShipmentDetails: 'Dettagli della spedizione',
      PaymentDetails: 'Dettagli del pagamento'
    };
    return titles[action.dialogType ?? ''] ?? action.label;
  }

  actionClass(style: string): string {
    const styles: Record<string, string> = {
      primary: 'bg-blue-700 text-white hover:bg-blue-800',
      secondary: 'bg-slate-600 text-white hover:bg-slate-700',
      danger: 'bg-red-700 text-white hover:bg-red-800',
      warning: 'bg-amber-500 text-white hover:bg-amber-600'
    };
    return styles[style] ?? styles['secondary'];
  }

  statusClass(status: string): string {
    const classes: Record<string, string> = {
      Draft: 'status-draft',
      PendingApproval: 'status-pending',
      Confirmed: 'status-confirmed',
      Picking: 'status-processing',
      Packing: 'status-processing',
      PartiallyShipped: 'status-partial',
      Shipped: 'status-shipped',
      Completed: 'status-completed',
      Cancelled: 'status-cancelled',
      OnHold: 'status-hold'
    };
    return classes[status] ?? 'status-default';
  }

  downloadPdf(type: 'order' | 'invoice' | 'ddt', id: string, filename: string): void {
    if (this.downloadingPdf) {
      return;
    }

    this.downloadingPdf = `${type}-${id}`;
    const request = type === 'order'
      ? this.orderService.downloadOrderPdf(id)
      : type === 'invoice'
        ? this.orderService.downloadInvoicePdf(id)
        : this.orderService.downloadDdtPdf(id);

    request.pipe(
      takeUntil(this.destroy$),
      finalize(() => { this.downloadingPdf = null; })
    ).subscribe({
      next: blob => {
        const url = URL.createObjectURL(blob);
        window.open(url, '_blank', 'noopener');
        const link = document.createElement('a');
        link.href = url;
        link.download = filename;
        link.click();
        window.setTimeout(() => URL.revokeObjectURL(url), 60_000);
      },
      error: error => { this.errorMessage = this.describeError(error, 'Impossibile scaricare il PDF.'); }
    });
  }

  private executeAction(action: OrderPermittedAction, payload?: unknown): void {
    if (!this.order || this.submitting) {
      return;
    }

    this.submitting = true;
    this.errorMessage = '';
    this.orderService.postAction(action, payload).pipe(
      takeUntil(this.destroy$),
      finalize(() => { this.submitting = false; })
    ).subscribe({
      next: response => {
        this.order = this.extractOrder(response);
        if ('order' in response) {
          this.successMessage = response.shipment
            ? `DDT ${response.shipment.ddtNumber} emesso correttamente.`
            : `Azione completata: ${action.label}.`;
          this.openFulfillmentPdf(response);
        } else {
          this.successMessage = `Azione completata: ${action.label}.`;
        }
        this.dialogAction = null;
        this.loadOrder(this.order.id);
      },
      error: error => { this.errorMessage = this.describeError(error, 'Impossibile completare l’operazione.'); }
    });
  }

  private extractOrder(response: OrderActionResponse): Order {
    return 'order' in response ? response.order : response;
  }

  private openFulfillmentPdf(response: Extract<OrderActionResponse, { documentBytes: string }>): void {
    const binary = window.atob(response.documentBytes);
    const bytes = new Uint8Array(binary.length);
    for (let index = 0; index < binary.length; index++) {
      bytes[index] = binary.charCodeAt(index);
    }

    const url = URL.createObjectURL(new Blob([bytes], { type: response.contentType }));
    window.open(url, '_blank', 'noopener');
    const link = document.createElement('a');
    link.href = url;
    link.download = response.documentFileName;
    link.click();
    window.setTimeout(() => URL.revokeObjectURL(url), 60_000);
  }

  private describeError(error: unknown, fallback: string): string {
    if (error instanceof HttpErrorResponse) {
      if (typeof error.error === 'string' && error.error.trim()) {
        return error.error.trim();
      }
      if (error.error instanceof Blob) {
        void error.error.text().then(text => {
          if (text.trim()) {
            this.errorMessage = text.trim();
          }
        });
      }
    }
    return fallback;
  }

  private safeReturnUrl(value: string | null): string | null {
    if (!value || !value.startsWith('/') || value.startsWith('//') || value.includes('\\')) {
      return null;
    }
    return value;
  }
}
