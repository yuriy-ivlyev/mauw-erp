export interface SupplierPurchaseOrderLine {
  id: string;
  articleVariantId: string;
  sku: string;
  articleDescription: string;
  quantityOrdered: number;
  quantityReceived: number;
  unitCost: number | string;
}

export interface SupplierPurchaseOrder {
  id: string;
  orderNumber: string;
  supplierId: string;
  supplierName: string;
  orderDate: string;
  status: 'Draft' | 'Confirmed' | 'PartiallyReceived' | 'Completed' | 'ClosedWithDiscrepancies';
  notes: string | null;
  version: string;
  lines: SupplierPurchaseOrderLine[];
}

export interface SupplierReconciliationReceipt {
  supplierDeliveryNoteLineId: string;
  supplierDeliveryNoteId: string;
  documentNumber: string;
  quantity: number;
  linkedToOrderLine: boolean;
}

export interface SupplierReconciliationLine {
  supplierPurchaseOrderLineId: string | null;
  articleVariantId: string;
  sku: string;
  articleDescription: string;
  quantityOrdered: number;
  quantityReceived: number;
  delta: number;
  status: 'InLine' | 'Shortage' | 'Surplus';
  receipts: SupplierReconciliationReceipt[];
}

export interface SupplierReconciliation {
  supplierPurchaseOrderId: string;
  orderNumber: string;
  status: SupplierPurchaseOrder['status'];
  lines: SupplierReconciliationLine[];
}

export interface SupplierReturnDraft {
  id: string;
  supplierPurchaseOrderId: string;
  supplierDeliveryNoteId: string;
  createdOn: string;
  issuedOn: string | null;
  status: 'Draft' | 'Issued';
  version: string;
  lines: { supplierDeliveryNoteLineId: string; articleVariantId: string; sku: string; articleDescription: string; quantity: number }[];
}
