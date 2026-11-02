export type StatementMovementType = 'Invoice' | 'CreditNote' | 'Payment';

export interface CustomerStatementDto {
  customerId: string;
  customerName: string;
  totalInvoiced: number;
  totalCredited: number;
  totalPaid: number;
  netBalance: number;
  availableCreditNotesAmount: number;
  movements: StatementMovementDto[];
}

export interface StatementMovementDto {
  dateUtc: string;
  type: StatementMovementType;
  documentId: string;
  documentNumber: string;
  description: string;
  debit: number;
  credit: number;
  runningBalance: number;
  paymentMethod: string | null;
  sourceCreditNoteNumber: string | null;
  sdiStatus: string | null;
  sourceCreditNoteId?: string | null;
}
