import { Injectable } from '@angular/core';
import { ApiService } from './api.service';
import { ProductDto, CreateProductRequest, PageResponse } from '../models/product.model';
import { Observable } from 'rxjs';

@Injectable({
  providedIn: 'root'
})
export class ProductService {
  private endpoint = 'v1/products';

  constructor(private apiService: ApiService) { }

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

  updateProduct(id: string, request: CreateProductRequest): Observable<ProductDto> {
    return this.apiService.put<ProductDto>(`${this.endpoint}/${id}`, request);
  }

  deleteProduct(id: string): Observable<void> {
    return this.apiService.delete<void>(`${this.endpoint}/${id}`);
  }

  getVariantByBarcode(barcode: string): Observable<any> {
    return this.apiService.get<any>(`${this.endpoint}/barcode/${barcode}`);
  }
}
