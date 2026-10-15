import { Injectable } from '@angular/core';
import { ApiService } from './api.service';
import {
  ProductDto,
  ProductImageDto,
  CreateProductRequest,
  UpdateProductRequest,
  AddProductImageRequest,
  CreateProductVariantRequest,
  ProductVariantDto,
  ColorDto,
  SizeDto,
  CategoryDto,
  CollectionDto,
  MaterialDto,
  MacroCategoryDto,
  PageResponse
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
  getProducts(page: number = 1, pageSize: number = 20, search?: string): Observable<PageResponse<ProductDto>> {
    let url = `${this.endpoint}?page=${page}&pageSize=${pageSize}`;
    if (search) {
      url += `&search=${encodeURIComponent(search)}`;
    }
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

  deleteProductVariant(productId: string, variantId: string): Observable<void> {
    return this.apiService.delete<void>(`${this.endpoint}/${productId}/variants/${variantId}`);
  }

  getVariantByBarcode(barcode: string): Observable<any> {
    return this.apiService.get<any>(`${this.endpoint}/barcode/${barcode}`);
  }

  // Images
  addImage(productId: string, request: AddProductImageRequest): Observable<ProductImageDto> {
    return this.apiService.post<ProductImageDto>(`${this.endpoint}/${productId}/images`, request);
  }

  removeImage(productId: string, imageId: string): Observable<void> {
    return this.apiService.delete<void>(`${this.endpoint}/${productId}/images/${imageId}`);
  }

  setMainImage(productId: string, imageId: string): Observable<void> {
    return this.apiService.put<void>(`${this.endpoint}/${productId}/images/${imageId}/main`, {});
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

