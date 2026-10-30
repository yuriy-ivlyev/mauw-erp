import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';
import {
  Invoice,
  InvoiceListParams,
  InvoicePagedResult,
  RecordInvoicePaymentRequest
} from '../models/invoice.model';

@Injectable({ providedIn: 'root' })
export class InvoiceService {
  private readonly base = `${environment.apiUrl}/v1/invoices`;

  constructor(private readonly http: HttpClient) {}

  getList(filters: InvoiceListParams): Observable<InvoicePagedResult> {
    let params = new HttpParams()
      .set('page', String(filters.page))
      .set('pageSize', String(filters.pageSize))
      .set('sortBy', filters.sortBy ?? 'invoiceDate')
      .set('sortDescending', String(filters.sortDescending ?? true));
    if (filters.search?.trim()) params = params.set('search', filters.search.trim());
    if (filters.status) params = params.set('status', filters.status);
    if (filters.dateFrom) params = params.set('dateFrom', filters.dateFrom);
    if (filters.dateTo) params = params.set('dateTo', filters.dateTo);
    if (filters.unpaidOnly) params = params.set('unpaidOnly', 'true');
    return this.http.get<InvoicePagedResult>(this.base, { params });
  }

  getById(id: string): Observable<Invoice> {
    return this.http.get<Invoice>(`${this.base}/${id}`);
  }

  generateFromOrder(orderId: string): Observable<Invoice> {
    return this.http.post<Invoice>(`${this.base}/from-order/${orderId}`, {});
  }

  recordPayment(invoiceId: string, request: RecordInvoicePaymentRequest): Observable<Invoice> {
    return this.http.post<Invoice>(`${this.base}/${invoiceId}/payments`, request);
  }

  downloadPdf(invoiceId: string): Observable<Blob> {
    return this.http.get(`${this.base}/${invoiceId}/pdf`, { responseType: 'blob' });
  }
}
