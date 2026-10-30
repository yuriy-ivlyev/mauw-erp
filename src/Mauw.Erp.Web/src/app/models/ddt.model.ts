export interface DdtItem {
  id: string;
  ddtDocumentId: string;
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

export interface Ddt {
  id: string;
  ddtNumber: string;
  contactId: string;
  orderId: string | null;
  contactName: string;
  warehouseId: string | null;
  ddtDate: string;
  taxableTotal: number;
  vatTotal: number;
  grossTotal: number;
  items: DdtItem[];
}
