export interface ColorDto {
  id: string;
  name: string;
  htmlColor: string;
}

export interface SizeDto {
  id: string;
  name: string;
}

export interface CategoryDto {
  id: string;
  name: string;
  macroCategoryId?: string;
  macroCategoryName?: string;
}

export interface CollectionDto {
  id: string;
  name: string;
  description?: string;
}

export interface MaterialCompositionDto {
  id: string;
  componentName: string;
  percentageValue: number;
}

export interface MaterialDto {
  id: string;
  name: string;
  composition: MaterialCompositionDto[];
}

export interface MacroCategoryDto {
  id: string;
  name: string;
}

export interface ProductVariantDto {
  id: string;
  productId: string;
  colorId?: string;
  sizeId?: string;
  size: string;
  color: string;
  barcode: string;
  purchaseCost: number;
  stockQuantity: number;
  listPrice: number;
  finalNetPrice: number;
  vatAmount: number;
  finalGrossPrice: number;
  weightGrams?: number | null;
}

export interface ProductImageDto {
  id: string;
  productId: string;
  url: string;
  storageKey?: string;
  displayOrder: number;
  isMain: boolean;
  productVariantId?: string;
}

export interface AddProductImageRequest {
  url: string;
  isMain: boolean;
}

export interface ProductDto {
  id: string;
  supplierId: string;
  supplierCode: string;
  internalCode: string;
  name: string;
  defaultMarkupRate: number;
  vatRate: number;
  basePurchaseCost: number;
  hasVariants: boolean;
  categoryId?: string;
  categoryName?: string;
  collectionId?: string;
  collectionName?: string;
  materialId?: string;
  materialName?: string;
  images: ProductImageDto[];
  mainImageUrl?: string;
  variants: ProductVariantDto[];
}

export interface CreateProductRequest {
  supplierId: string;
  supplierCode: string;
  internalCode: string;
  name: string;
  defaultMarkupRate: number;
  vatRate: number;
  basePurchaseCost: number;
  hasVariants: boolean;
  categoryId?: string;
  collectionId?: string;
  materialId?: string;
}

export interface UpdateProductRequest {
  supplierId: string;
  supplierCode: string;
  internalCode: string;
  name: string;
  defaultMarkupRate: number;
  vatRate: number;
  basePurchaseCost: number;
  hasVariants: boolean;
  categoryId?: string;
  collectionId?: string;
  materialId?: string;
}

export interface ProductParentDto {
  productId: string;
  supplierCode: string;
  internalCode: string;
  name: string;
  hasVariants: boolean;
  variantCount: number;
}

export interface MatrixSizeDto {
  id: string;
  name: string;
  displayOrder: number;
}

export interface MatrixColorDto {
  id: string;
  name: string;
  htmlColor: string;
}

export interface MatrixCellDto {
  variantId: string;
  colorId: string | null;
  sizeId: string | null;
  barcode: string;
  unitPrice: number;
  availableStock: number;
  weightGrams?: number | null;
}

export interface ProductMatrixDto {
  productId: string;
  supplierCode: string;
  productName: string;
  hasVariants: boolean;
  sizes: MatrixSizeDto[];
  colors: MatrixColorDto[];
  cells: MatrixCellDto[];
}

export interface CreateProductVariantRequest {
  productId: string;
  colorId: string;
  sizeId: string;
  barcode: string;
  purchaseCost: number;
  weightGrams?: number | null;
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
