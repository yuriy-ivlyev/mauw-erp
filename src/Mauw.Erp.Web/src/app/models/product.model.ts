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
  sellingPrice: number | null;
  id: string;
  productId: string;
  colorId?: string;
  sizeId?: string;
  size: string;
  color: string;
  barcode: string;
  supplierCode?: string | null;
  purchaseCost: number;
  stockQuantity: number;
  listPrice: number | null;
  finalNetPrice: number | null;
  vatAmount: number;
  finalGrossPrice: number | null;
  weightGrams?: number | null;
  varianteVetrina?: boolean;
}

export type ImageCategory = 'Main' | 'Left' | 'Right' | 'Zoom1' | 'Zoom2' | 'Zoom3' | 'OnPerson' | 'OnManikin';

/** Immagine di un prodotto o di una variante (storage S3, servita dal proxy autenticato). */
export interface ImageDto {
  id: string;
  targetId: string;
  url: string;
  category: ImageCategory;
  sortOrder: number;
}

export interface ProductDto {
  sellingPrice: number | null;
  id: string;
  supplierId: string;
  supplierCode: string;
  barcode: string;
  name: string;
  vatRate: number;
  basePurchaseCost: number;
  hasVariants: boolean;
  categoryId?: string;
  categoryName?: string;
  collectionId?: string;
  collectionName?: string;
  materialId?: string;
  materialName?: string;
  /** Miniatura per la lista: copertina del prodotto o della sua variante vetrina. */
  mainImageUrl?: string;
  variants: ProductVariantDto[];
}

export interface CreateProductRequest {
  sellingPrice: number | null;
  supplierId: string;
  supplierCode: string;
  barcode: string;
  name: string;
  vatRate: number;
  basePurchaseCost: number;
  hasVariants: boolean;
  categoryId?: string;
  collectionId?: string;
  materialId?: string;
}

export interface UpdateProductRequest {
  sellingPrice: number | null;
  supplierId: string;
  supplierCode: string;
  barcode: string;
  name: string;
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
  barcode: string;
  name: string;
  hasVariants: boolean;
  variantCount: number;
}

export interface VariantSearchDto {
  variantId: string;
  productId: string;
  productName: string;
  sku: string | null;
  barcode: string;
  colorName: string | null;
  sizeName: string | null;
  currentStock: number;
  supplierCode: string | null;
  purchaseCost: number;
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
  unitNetPrice: number;
  vatRate: number;
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
  sellingPrice: number | null;
  productId: string;
  colorId: string;
  sizeId: string;
  barcode: string;
  supplierCode?: string | null;
  purchaseCost: number;
  weightGrams?: number | null;
}

export interface UpdateProductVariantRequest {
  colorId: string | null;
  sizeId: string | null;
  barcode: string;
  supplierCode?: string | null;
  purchaseCost: number;
  weightGrams?: number | null;
  sellingPrice: number | null;
}

export type PageResponse<T> = PagedResult<T>;

export function generateUUID(): string {
  return 'xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx'.replace(/[xy]/g, function(c) {
    const r = Math.random() * 16 | 0;
    const v = c === 'x' ? r : (r & 0x3 | 0x8);
    return v.toString(16);
  });
}
import { PagedResult } from './pagination.model';
export type { PagedResult } from './pagination.model';
