export interface InvoiceItem {
  id: string;
  invoiceId: string;
  variantId: string;
  quantity: number;
  unitNetPrice: number;
  vatRate: number;
  lineNetTotal: number;
  lineVatTotal: number | null;
  lineGrossTotal: number | null;
  natureCode: string | null;
  discountPercent: number;
  sku: string | null;
  productName: string | null;
  sizeName: string | null;
  colorName: string | null;
}

export interface InvoicePayment {
  id: string;
  amount: number;
  paymentDate: string;
  paymentMethod: string | null;
  transactionDetails: string | null;
}

export interface Invoice {
  id: string;
  invoiceNumber: string;
  contactId: string;
  orderId: string | null;
  contactName: string;
  invoiceDate: string;
  taxableTotal: number;
  vatTotal: number;
  grossTotal: number;
  shippingCost: number;
  shippingVatRate: number | null;
  shippingNatureCode: string | null;
  sdiSubmittedAt: string | null;
  paidAmount: number;
  balanceDue: number;
  status: string;
  items: InvoiceItem[];
  payments: InvoicePayment[];
}

export interface InvoicePagedResult {
  items: Invoice[];
  pageNumber: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
}

export interface InvoiceListParams {
  page: number;
  pageSize: number;
  search?: string;
  status?: string;
  dateFrom?: string;
  dateTo?: string;
  unpaidOnly?: boolean;
  sortBy?: string;
  sortDescending?: boolean;
}

export interface RecordInvoicePaymentRequest {
  amount: number;
  paymentDate: string;
  paymentMethod: string;
  transactionDetails: string | null;
}
