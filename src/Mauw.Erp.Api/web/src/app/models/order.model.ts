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
  items: OrderItem[];
  linkedDocuments: OrderLinkedDocuments | null;
}

export interface OrderFilterParams {
  search?: string;
  status?: string;
  orderType?: string;
  page?: number;
  pageSize?: number;
}

export interface PagedResult<T> {
  items: T[];
  pageNumber: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
}
