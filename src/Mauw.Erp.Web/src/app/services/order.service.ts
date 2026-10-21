import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';
import { Order, OrderFilterParams, PagedResult } from '../models/order.model';

@Injectable({ providedIn: 'root' })
export class OrderService {
  private readonly base = `${environment.apiUrl}/v1/orders`;

  constructor(private http: HttpClient) {}

  getList(params: OrderFilterParams = {}): Observable<PagedResult<Order>> {
    let httpParams = new HttpParams();
    if (params.search)     httpParams = httpParams.set('search', params.search);
    if (params.status)     httpParams = httpParams.set('status', params.status);
    if (params.orderType)  httpParams = httpParams.set('orderType', params.orderType);
    if (params.sortBy)     httpParams = httpParams.set('sortBy', params.sortBy);
    if (params.sortDescending) httpParams = httpParams.set('sortDescending', 'true');
    httpParams = httpParams
      .set('page', String(params.page ?? 1))
      .set('pageSize', String(params.pageSize ?? 20));
    return this.http.get<PagedResult<Order>>(this.base, { params: httpParams });
  }

  getById(id: string): Observable<Order> {
    return this.http.get<Order>(`${this.base}/${id}`);
  }

  downloadOrderPdf(id: string): Observable<Blob> {
    return this.http.get(`${this.base}/${id}/pdf`, { responseType: 'blob' });
  }

  downloadInvoicePdf(invoiceId: string): Observable<Blob> {
    return this.http.get(`${environment.apiUrl}/v1/invoices/${invoiceId}/pdf`, { responseType: 'blob' });
  }

  downloadDdtPdf(ddtId: string): Observable<Blob> {
    return this.http.get(`${environment.apiUrl}/v1/ddts/${ddtId}/pdf`, { responseType: 'blob' });
  }
}
