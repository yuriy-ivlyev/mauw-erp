import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';
import {
  InventoryStock,
  InventoryStockFilters,
  InventoryStockMovement,
  InventoryStockMovementsResponse,
  InventoryStocksPage,
  InventoryStocksSummary
} from '../models/inventory-stock.model';

@Injectable({ providedIn: 'root' })
export class InventoryStockService {
  private readonly base = `${environment.apiUrl}/v1/inventory/stocks`;

  constructor(private readonly http: HttpClient) {}

  getStocks(filters: InventoryStockFilters): Observable<InventoryStocksPage> {
    return this.http.get<InventoryStocksPage>(this.base, { params: this.toParams(filters) });
  }

  getSummary(filters: InventoryStockFilters): Observable<InventoryStocksSummary> {
    return this.http.get<InventoryStocksSummary>(`${this.base}/summary`, {
      params: this.toFilterParams(filters).set('pageSize', String(filters.pageSize))
    });
  }

  getMovements(
    warehouseId: string,
    variantId: string,
    atDate?: string,
    page = 1,
    pageSize = 20
  ): Observable<InventoryStockMovementsResponse> {
    let params = new HttpParams()
      .set('page', String(page))
      .set('pageSize', String(pageSize));
    if (atDate) params = params.set('atDate', atDate);
    return this.http.get<InventoryStockMovementsResponse>(
      `${this.base}/${warehouseId}/${variantId}/movements`,
      { params }
    );
  }

  exportCsv(filters: InventoryStockFilters): Observable<Blob> {
    return this.http.get(`${this.base}/export`, {
      params: this.toParams(filters).set('format', 'csv'),
      responseType: 'blob'
    });
  }

  exportPdf(filters: InventoryStockFilters): Observable<Blob> {
    return this.http.get(`${this.base}/export`, {
      params: this.toParams(filters).set('format', 'pdf'),
      responseType: 'blob'
    });
  }

  private toParams(filters: InventoryStockFilters): HttpParams {
    return this.toFilterParams(filters)
      .set('page', String(filters.page))
      .set('pageSize', String(filters.pageSize))
      .set('sortBy', filters.sortBy)
      .set('sortDescending', String(filters.sortDescending));
  }

  private toFilterParams(filters: InventoryStockFilters): HttpParams {
    let params = new HttpParams()
      .set('onlyInStock', String(filters.onlyInStock))
      .set('valuationMethod', filters.valuationMethod);
    if (filters.search) params = params.set('search', filters.search);
    if (filters.warehouseId) params = params.set('warehouseId', filters.warehouseId);
    if (filters.categoryId) params = params.set('categoryId', filters.categoryId);
    if (filters.atDate) params = params.set('atDate', filters.atDate);
    return params;
  }
}
