export interface ProductVariantDto {
  id: string;
  productId: string;
  size: string;
  color: string;
  barcode: string;
  purchaseCost: number;
  stockQuantity: number;
  listPrice: number;
  finalNetPrice: number;
  vatAmount: number;
  finalGrossPrice: number;
}

export interface ProductDto {
  id: string;
  supplierId: string;
  supplierCode: string;
  internalCode: string;
  name: string;
  defaultMarkupRate: number;
  vatRate: number;
  imageUrl: string;
  variants: ProductVariantDto[];
}

export interface CreateProductRequest {
  supplierId: string; // UUID string format
  supplierCode: string;
  internalCode: string;
  name: string;
  defaultMarkupRate: number;
  vatRate: number;
  imageUrl: string;
  variants: CreateProductVariantRequest[];
}

export interface CreateProductVariantRequest {
  size: string;
  color: string;
  barcode: string;
  purchaseCost: number;
  stockQuantity: number;
}

export interface PageResponse<T> {
  items: T[];
  pageNumber: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
}

export function generateUUID(): string {
  return 'xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx'.replace(/[xy]/g, function(c) {
    const r = Math.random() * 16 | 0;
    const v = c === 'x' ? r : (r & 0x3 | 0x8);
    return v.toString(16);
  });
}
