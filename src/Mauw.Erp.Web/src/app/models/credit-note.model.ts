export type CreditNoteReason =
  | 'GoodsReturn'
  | 'PriceAdjustment'
  | 'CommercialAllowance'
  | 'FullCancellation';

export interface CreateCreditNoteLineRequest {
  invoiceItemId: string | null;
  isShippingCharge: boolean;
  quantity: number;
}

export interface CreateCreditNoteRequest {
  invoiceId: string;
  reason: CreditNoteReason;
  lines: CreateCreditNoteLineRequest[];
  returnToWarehouse: boolean;
  warehouseId: string | null;
}

export interface CreditNoteSummary {
  id: string;
  creditNoteNumber: string;
  creditNoteDate: string;
  taxableTotal: number;
  vatTotal: number;
  grossTotal: number;
  reason: string | null;
  sdiStatus: string;
  allocatedAmount?: number;
  remainingCredit?: number;
}

export interface AvailableCreditNote {
  id: string;
  creditNoteNumber: string;
  creditNoteDate: string;
  grossTotal: number;
  allocatedAmount: number;
  remainingCredit: number;
}

export interface CreditNoteItemDetail {
  id: string;
  sourceInvoiceItemId: string | null;
  variantId: string | null;
  description: string;
  isShippingCharge: boolean;
  quantity: number;
  unitNetPrice: number;
  discountPercent: number;
  vatRate: number;
  natureCode: string | null;
  lineNetTotal: number;
  sku: string | null;
  productName: string | null;
  sizeName: string | null;
  colorName: string | null;
}

export interface CreditNoteDetail extends CreditNoteSummary {
  invoiceId: string;
  relatedInvoiceNumber: string;
  relatedInvoiceDate: string;
  contactId: string;
  contactName: string;
  returnToWarehouse: boolean;
  warehouseId: string | null;
  sdiFileName: string | null;
  sdiTransmissionId: string | null;
  sdiTransmittedAtUtc: string | null;
  items: CreditNoteItemDetail[];
}

export interface CreditNoteSelectableLine {
  invoiceItemId: string | null;
  isShippingCharge: boolean;
  description: string;
  sku: string | null;
  originalQuantity: number;
  creditedQuantity: number;
  remainingQuantity: number;
  unitNetPrice: number;
  discountPercent: number;
  vatRate: number;
  natureCode: string | null;
  lineNetTotal: number;
}

export interface CreditNotePreviewLine {
  invoiceItemId: string | null;
  isShippingCharge: boolean;
  description: string;
  sku: string | null;
  quantity: number;
  unitNetPrice: number;
  discountPercent: number;
  vatRate: number;
  natureCode: string | null;
  lineNetTotal: number;
}

export interface CreditNoteVatSummary {
  vatRate: number;
  natureCode: string | null;
  taxableAmount: number;
  vatAmount: number;
}

export interface CreditNotePreview {
  invoiceId: string;
  taxableTotal: number;
  vatTotal: number;
  grossTotal: number;
  vatSummary: CreditNoteVatSummary[];
  availableLines: CreditNoteSelectableLine[];
  selectedLines: CreditNotePreviewLine[];
}

export interface CreditNoteIssued {
  creditNote: CreditNoteSummary;
  courtesyPdf: string | null;
  pdfGenerationError: string | null;
}

export interface FiscalDocumentTransmissionRequest {
  id: string;
  documentType: 'Invoice' | 'CreditNote';
}

export interface FiscalDocumentTransmissionItemResult {
  id: string;
  documentType: string;
  success: boolean;
  documentNumber: string | null;
  errorMessage: string | null;
}

export interface BulkFiscalTransmissionResult {
  processedCount: number;
  successCount: number;
  failedCount: number;
  results: FiscalDocumentTransmissionItemResult[];
}
