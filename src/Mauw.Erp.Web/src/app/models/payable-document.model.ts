import { PagedResult } from './pagination.model';

export type PayableDocumentKind = 'SupplierInvoice' | 'ExpenseReimbursement' | 'EmployeePayroll';
export type PayableDocumentStatus = 'Draft' | 'PendingApproval' | 'Approved' | 'Rejected' | 'Cancelled';
export type PayablePaymentStatus = 'NotPayable' | 'Unpaid' | 'PartiallyPaid' | 'Paid';
export type PayableExpenseCategory =
  | 'Merchandise'
  | 'Utilities'
  | 'Services'
  | 'MaintenanceWork'
  | 'Travel'
  | 'ProfessionalFees'
  | 'AssetCandidate'
  | 'Other'
  | 'Payroll';

export interface PayableDocumentLine {
  id: string;
  description: string;
  category: PayableExpenseCategory;
  costCenter: string | null;
  project: string | null;
  taxableAmount: number;
  vatRate: number | null;
  natureCode: string | null;
  vatAmount: number;
  grossAmount: number;
}

export interface PayableDocumentPayment {
  id: string;
  amount: number;
  paymentDateUtc: string;
  paymentMethod: string;
  transactionDetails: string | null;
  createdByUserId: string | null;
}

export interface PayableDocumentListItem {
  id: string;
  documentCode: string;
  kind: PayableDocumentKind;
  employeeId: string | null;
  employeeNumber: string | null;
  supplierName: string | null;
  sourceDocumentNumber: string | null;
  documentDate: string;
  dueDate: string;
  beneficiaryName: string | null;
  status: PayableDocumentStatus;
  paymentStatus: PayablePaymentStatus;
  taxableTotal: number;
  vatTotal: number;
  grossTotal: number;
  paidAmount: number;
  balanceDue: number;
  attachmentFileName: string | null;
  hasAttachment: boolean;
}

export interface PayableDocument extends PayableDocumentListItem {
  supplierId: string | null;
  notes: string | null;
  attachmentContentType: string | null;
  attachmentLength: number | null;
  createdByUserId: string | null;
  approvedByUserId: string | null;
  submittedAtUtc: string | null;
  approvedAtUtc: string | null;
  rejectionReason: string | null;
  version: string;
  lines: PayableDocumentLine[];
  payments: PayableDocumentPayment[];
}

export interface PayableDocumentLineRequest {
  description: string;
  category: PayableExpenseCategory;
  taxableAmount: number;
  vatRate: number | null;
  natureCode: string | null;
  vatAmount: number;
  grossAmount: number;
  costCenter: string | null;
  project: string | null;
}

export interface CreatePayableDocumentRequest {
  kind: PayableDocumentKind;
  supplierId: string | null;
  sourceDocumentNumber: string | null;
  documentDate: string;
  dueDate: string;
  beneficiaryName: string | null;
  notes: string | null;
  lines: PayableDocumentLineRequest[];
  version?: string;
  employeeId?: string | null;
}

export interface PayableDocumentPaymentRequest {
  version: string;
  amount: number;
  paymentDate: string | null;
  paymentMethod: string;
  transactionDetails: string | null;
}

export type PayableDocumentPage = PagedResult<PayableDocumentListItem>;
