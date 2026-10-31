import { PageSortParams } from './pagination.model';
export type { PagedResult } from './pagination.model';

export interface OrderItem {
  id: string;
  orderId: string;
  variantId: string;
  quantity: number;
  unitNetPrice: number;
  vatRate: number;
  lineNetTotal: number;
  lineVatTotal: number | null;
  lineGrossTotal: number | null;
  natureCode: string | null;
  discountPercent: number;
  shippedQuantity: number;
  remainingQuantity: number;
  isBillingOnly?: boolean;
  isExcludedFromInvoice?: boolean;
  pricingSnapshot?: { isCustomerReturn?: boolean } | null;
  sku?: string | null;
  productName?: string | null;
  sizeName?: string | null;
  colorName?: string | null;
}

export interface OrderPermittedAction {
  action: string;
  label: string;
  endpoint: string;
  method: 'POST';
  style: 'primary' | 'secondary' | 'danger' | 'warning' | string;
  requiresDialog: boolean;
  dialogType: string | null;
}

export interface DdtDocumentRef {
  id: string;
  ddtNumber: string;
  ddtDate: string;
}

export interface CreditNoteRef {
  id: string;
  creditNoteNumber: string;
  creditNoteDate: string;
  grossTotal: number;
  reason: string | null;
}

export interface OrderLinkedDocuments {
  invoiceId: string | null;
  invoiceNumber: string | null;
  ddtId: string | null;
  ddtNumber: string | null;
  creditNotes: CreditNoteRef[];
  ddts?: DdtDocumentRef[] | null;
}

export interface Order {
  id: string;
  orderNumber: string;
  contactId: string;
  contactName: string;
  orderDate: string;
  customerDiscountRate: number;
  status: string;
  taxableTotal: number;
  vatTotal: number;
  grossTotal: number;
  orderType: 'Standard' | 'CassaSession' | 'Preventivo' | 'WebOrder' | string;
  fulfillmentType: 'CashDesk' | 'StandardShipping' | string;
  items: OrderItem[];
  linkedDocuments: OrderLinkedDocuments | null;
  statusDescription: string;
  isReadOnly: boolean;
  canEditInvoice?: boolean;
  isUnderAdjustment?: boolean;
  invoiceSdiStatus?: string | null;
  permittedActions: OrderPermittedAction[];
  internalNotes?: string | null;
  shippingCost?: number;
  shippingVatRate?: number | null;
  shippingNatureCode?: string | null;
}

export interface OrderFulfillmentResult {
  order: Order;
  documentFileName: string;
  contentType: string;
  documentBytes: string;
  shipment?: Shipment;
}

export interface Shipment {
  id: string;
  shipmentNumber: string;
  ddtNumber: string;
  carrierName: string | null;
  trackingCode: string | null;
  boxCount: number;
  totalWeightKg: number | null;
  shippedAtUtc: string;
  lines: ShipmentLine[];
}

export interface ShipmentLine {
  orderLineId: string;
  productId: string;
  sku: string;
  quantityShipped: number;
}

export interface CreateShipmentRequest {
  carrierName: string;
  trackingCode: string | null;
  boxCount: number;
  weightKg: number | null;
  items: Array<{ orderItemId: string; quantity: number }>;
}

export type OrderActionResponse = Order | OrderFulfillmentResult;

export interface OrderFilterParams extends PageSortParams {
  search?: string;
  status?: string;
  orderType?: string;
}

export interface CreateOrderRequest {
  contactId: string;
  orderType: 'Standard' | 'CassaSession';
  fulfillmentType: 'StandardShipping' | 'CashDesk';
  internalNotes?: string | null;
  shippingCost?: number;
  shippingVatRate?: number | null;
  shippingNatureCode?: string | null;
}

export interface UpdateOrderShippingChargeRequest {
  shippingCost: number;
  vatRate: number | null;
  natureCode: string | null;
}

export interface CreateOrderItemRequest {
  variantId: string;
  quantity: number;
  unitNetPrice: number;
  vatRate: number;
  natureCode: string | null;
  discountPercent: number;
}

export interface AddCompletedOrderItemRequest {
  variantId: string;
  quantity: number;
  natureCode: string | null;
}

export interface OrderAdjustmentLineRequest {
  orderItemId: string | null;
  variantId: string | null;
  quantity: number;
  natureCode?: string | null;
}

export interface UpdateOrderAdjustmentLinesRequest {
  lines: OrderAdjustmentLineRequest[];
}

export interface DraftOrderPreview {
  lines: Array<{ lineNetTotal: number }>;
  totalTaxable: number;
  totalVat: number;
  totalGross: number;
}
