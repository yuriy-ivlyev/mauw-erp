export interface SupplierArticleMatch {
  articleVariantId: string;
  articleDescription: string;
}

export interface SupplierDeliveryNoteLine {
  id: string;
  supplierItemCode: string;
  articleVariantId: string | null;
  articleDescription: string | null;
  description: string;
  supplierPurchaseOrderLineId: string | null;
  quantity: number;
  unitPrice: number | string;
  lineStatus: 'Matched' | 'NewArticleNeeded' | 'Error';
  stockMovementId: string | null;
}

export interface SupplierDeliveryNote {
  id: string;
  supplierId: string;
  supplierName: string;
  warehouseId: string;
  warehouseName: string;
  documentNumber: string;
  documentDate: string;
  uploadedAt: string;
  supplierPurchaseOrderId: string | null;
  filePath: string;
  status: 'Draft' | 'Parsing' | 'Parsed' | 'Confirmed' | 'Cancelled';
  version: string;
  lines: SupplierDeliveryNoteLine[];
}

export interface SupplierDeliveryNoteListItem {
  id: string;
  documentNumber: string;
  documentDate: string;
  supplierName: string;
  warehouseName: string;
  lineCount: number;
  pieceCount: number;
  status: SupplierDeliveryNote['status'];
}

export interface SaveSupplierDeliveryNoteLines {
  version: string;
  documentNumber: string;
  documentDate: string;
  lines: Pick<SupplierDeliveryNoteLine, 'supplierItemCode' | 'articleVariantId' | 'supplierPurchaseOrderLineId' | 'description' | 'quantity' | 'unitPrice'>[];
}
