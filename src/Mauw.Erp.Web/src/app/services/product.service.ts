import { Injectable } from '@angular/core';
import { ApiService } from './api.service';
import {
  ProductDto,
  CreateProductRequest,
  UpdateProductRequest,
  CreateProductVariantRequest,
  UpdateProductVariantRequest,
  ProductVariantDto,
  ColorDto,
  SizeDto,
  CategoryDto,
  CollectionDto,
  MaterialDto,
  MacroCategoryDto,
  PageResponse,
  ProductParentDto,
  VariantSearchDto,
  ProductMatrixDto
} from '../models/product.model';
import { Observable } from 'rxjs';

@Injectable({
  providedIn: 'root'
})
export class ProductService {
  private endpoint = 'v1/products';
  private supportEndpoint = 'v1/support';

  constructor(private apiService: ApiService) { }

  // Products CRUD
  getProducts(
    page: number = 1,
    pageSize: number = 20,
    search?: string,
    sortBy?: string,
    sortDescending = false
  ): Observable<PageResponse<ProductDto>> {
    let url = `${this.endpoint}?page=${page}&pageSize=${pageSize}`;
    if (search) {
      url += `&search=${encodeURIComponent(search)}`;
    }
    if (sortBy) url += `&sortBy=${encodeURIComponent(sortBy)}`;
    if (sortDescending) url += '&sortDescending=true';
    return this.apiService.get<PageResponse<ProductDto>>(url);
  }

  getProduct(id: string): Observable<ProductDto> {
    return this.apiService.get<ProductDto>(`${this.endpoint}/${id}`);
  }

  createProduct(request: CreateProductRequest): Observable<ProductDto> {
    return this.apiService.post<ProductDto>(this.endpoint, request);
  }

  updateProduct(id: string, request: UpdateProductRequest): Observable<ProductDto> {
    return this.apiService.put<ProductDto>(`${this.endpoint}/${id}`, request);
  }

  deleteProduct(id: string): Observable<void> {
    return this.apiService.delete<void>(`${this.endpoint}/${id}`);
  }

  // Variants
  getProductVariants(productId: string): Observable<ProductVariantDto[]> {
    return this.apiService.get<ProductVariantDto[]>(`${this.endpoint}/${productId}/variants`);
  }

  createProductVariant(request: CreateProductVariantRequest): Observable<ProductVariantDto> {
    return this.apiService.post<ProductVariantDto>(`${this.endpoint}/${request.productId}/variants`, request);
  }

  updateProductVariant(productId: string, variantId: string, request: UpdateProductVariantRequest): Observable<ProductVariantDto> {
    return this.apiService.patch<ProductVariantDto>(`${this.endpoint}/${productId}/variants/${variantId}`, request);
  }

  deleteProductVariant(productId: string, variantId: string): Observable<void> {
    return this.apiService.delete<void>(`${this.endpoint}/${productId}/variants/${variantId}`);
  }

  getVariantByBarcode(barcode: string): Observable<any> {
    return this.apiService.get<any>(`${this.endpoint}/barcode/${barcode}`);
  }

  searchParents(q: string, limit = 20): Observable<ProductParentDto[]> {
    return this.apiService.get<ProductParentDto[]>(
      `${this.endpoint}/search-parents?q=${encodeURIComponent(q)}&limit=${limit}`
    );
  }

  searchVariants(q: string, limit = 30, supplierId?: string): Observable<VariantSearchDto[]> {
    const supplierParam = supplierId ? `&supplierId=${encodeURIComponent(supplierId)}` : '';
    return this.apiService.get<VariantSearchDto[]>(
      `${this.endpoint}/variants/search?q=${encodeURIComponent(q)}&limit=${limit}${supplierParam}`
    );
  }

  getProductMatrix(productId: string, warehouseId?: string): Observable<ProductMatrixDto> {
    const whParam = warehouseId ? `?warehouseId=${warehouseId}` : '';
    return this.apiService.get<ProductMatrixDto>(`${this.endpoint}/${productId}/matrix${whParam}`);
  }

  // Variante vetrina: le sue immagini rappresentano il prodotto nella lista
  setVarianteVetrina(productId: string, variantId: string, value: boolean): Observable<void> {
    const url = `${this.endpoint}/${productId}/variants/${variantId}/vetrina`;
    return value ? this.apiService.put<void>(url, {}) : this.apiService.delete<void>(url);
  }

  // Support: Colors
  getColors(): Observable<ColorDto[]> {
    return this.apiService.get<ColorDto[]>(`${this.supportEndpoint}/colors`);
  }

  // Support: Sizes
  getSizes(): Observable<SizeDto[]> {
    return this.apiService.get<SizeDto[]>(`${this.supportEndpoint}/sizes`);
  }

  // Support: Categories
  getCategories(): Observable<CategoryDto[]> {
    return this.apiService.get<CategoryDto[]>(`${this.supportEndpoint}/categories`);
  }

  // Support: Collections
  getCollections(): Observable<CollectionDto[]> {
    return this.apiService.get<CollectionDto[]>(`${this.supportEndpoint}/collections`);
  }

  // Support: Materials
  getMaterials(): Observable<MaterialDto[]> {
    return this.apiService.get<MaterialDto[]>(`${this.supportEndpoint}/materials`);
  }

  // Support: MacroCategories
  getMacroCategories(): Observable<MacroCategoryDto[]> {
    return this.apiService.get<MacroCategoryDto[]>(`${this.supportEndpoint}/macro-categories`);
  }
}
