import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';
import {
  WarehouseSummary,
  WarehouseDetail,
  WarehouseFilterParams,
  CreateWarehouseRequest,
  UpdateWarehouseRequest,
  PagedResult
} from '../models/warehouse.model';

@Injectable({ providedIn: 'root' })
export class WarehouseService {
  private readonly base = `${environment.apiUrl}/v1/warehouses`;

  constructor(private http: HttpClient) {}

  getList(params: WarehouseFilterParams): Observable<PagedResult<WarehouseSummary>> {
    let httpParams = new HttpParams()
      .set('page', params.page.toString())
      .set('pageSize', params.pageSize.toString());

    if (params.search) httpParams = httpParams.set('search', params.search);
    if (params.type != null) httpParams = httpParams.set('type', params.type.toString());
    if (params.isActive != null) httpParams = httpParams.set('isActive', params.isActive.toString());

    return this.http.get<PagedResult<WarehouseSummary>>(this.base, { params: httpParams });
  }

  getById(id: string): Observable<WarehouseDetail> {
    return this.http.get<WarehouseDetail>(`${this.base}/${id}`);
  }

  create(request: CreateWarehouseRequest): Observable<WarehouseDetail> {
    return this.http.post<WarehouseDetail>(this.base, request);
  }

  update(id: string, request: UpdateWarehouseRequest): Observable<WarehouseDetail> {
    return this.http.put<WarehouseDetail>(`${this.base}/${id}`, request);
  }

  delete(id: string): Observable<void> {
    return this.http.delete<void>(`${this.base}/${id}`);
  }

  setDefault(id: string): Observable<WarehouseDetail> {
    return this.http.patch<WarehouseDetail>(`${this.base}/${id}/set-default`, {});
  }
}
