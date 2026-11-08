export type SupplierDocumentOcrColumn = 'SupplierCode' | 'Description' | 'Quantity';

export interface SupplierDocumentOcrHeaderAlias {
  id: string;
  column: SupplierDocumentOcrColumn;
  alias: string;
  position: number;
}

export interface CreateSupplierDocumentOcrHeaderAliasRequest {
  column: SupplierDocumentOcrColumn;
  alias: string;
}
