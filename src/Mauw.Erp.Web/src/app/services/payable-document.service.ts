import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';
import {
  CreatePayableDocumentRequest,
  PayableDocument,
  PayableDocumentPage,
  PayableDocumentPaymentRequest
} from '../models/payable-document.model';

@Injectable({ providedIn: 'root' })
export class PayableDocumentService {
  private readonly base = `${environment.apiUrl}/v1/payables`;

  constructor(private readonly http: HttpClient) {}

  list(
    page: number,
    pageSize: number,
    filters: { search?: string; kind?: string; status?: string; dateFrom?: string; dateTo?: string }
  ): Observable<PayableDocumentPage> {
    let params = new HttpParams().set('page', String(page)).set('pageSize', String(pageSize));
    for (const [key, value] of Object.entries(filters)) {
      if (value) {
        params = params.set(key, value);
      }
    }
    return this.http.get<PayableDocumentPage>(this.base, { params });
  }

  listReimbursements(page: number, pageSize: number): Observable<PayableDocumentPage> {
    const params = new HttpParams()
      .set('page', String(page))
      .set('pageSize', String(pageSize));
    return this.http.get<PayableDocumentPage>(`${this.base}/reimbursements`, { params });
  }

  get(id: string): Observable<PayableDocument> {
    return this.http.get<PayableDocument>(`${this.base}/${id}`);
  }

  getReimbursement(id: string): Observable<PayableDocument> {
    return this.http.get<PayableDocument>(`${this.base}/reimbursements/${id}`);
  }

  createSupplierInvoice(request: CreatePayableDocumentRequest): Observable<PayableDocument> {
    return this.http.post<PayableDocument>(this.base, request);
  }

  createReimbursement(request: CreatePayableDocumentRequest): Observable<PayableDocument> {
    return this.http.post<PayableDocument>(`${this.base}/reimbursements`, request);
  }

  update(id: string, request: CreatePayableDocumentRequest, mine = false): Observable<PayableDocument> {
    return this.http.put<PayableDocument>(
      `${mine ? `${this.base}/reimbursements` : this.base}/${id}`,
      request
    );
  }

  uploadAttachment(id: string, version: string, file: File, mine = false): Observable<PayableDocument> {
    const form = new FormData();
    form.append('file', file, file.name);
    const params = new HttpParams().set('version', version);
    const path = `${mine ? `${this.base}/reimbursements` : this.base}/${id}/attachment`;
    return this.http.post<PayableDocument>(path, form, { params });
  }

  downloadAttachment(id: string, mine = false): Observable<Blob> {
    const path = `${mine ? `${this.base}/reimbursements` : this.base}/${id}/attachment`;
    return this.http.get(path, { responseType: 'blob' });
  }

  submit(id: string, version: string, mine = false): Observable<PayableDocument> {
    const path = `${mine ? `${this.base}/reimbursements` : this.base}/${id}/submit`;
    return this.http.post<PayableDocument>(path, { version });
  }

  approve(id: string, version: string): Observable<PayableDocument> {
    return this.http.post<PayableDocument>(`${this.base}/${id}/approve`, { version });
  }

  reject(id: string, version: string, reason: string): Observable<PayableDocument> {
    return this.http.post<PayableDocument>(`${this.base}/${id}/reject`, { version, reason });
  }

  cancel(id: string, version: string, mine = false): Observable<PayableDocument> {
    const path = `${mine ? `${this.base}/reimbursements` : this.base}/${id}/cancel`;
    return this.http.post<PayableDocument>(path, { version });
  }

  recordPayment(id: string, request: PayableDocumentPaymentRequest): Observable<PayableDocument> {
    return this.http.post<PayableDocument>(`${this.base}/${id}/payments`, request);
  }
}
