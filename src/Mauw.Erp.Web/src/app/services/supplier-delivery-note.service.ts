import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { environment } from '../../environments/environment';
import { PagedResult } from '../models/pagination.model';
import { SaveSupplierDeliveryNoteLines, SupplierArticleMatch, SupplierDeliveryNote, SupplierDeliveryNoteListItem } from '../models/supplier-delivery-note.model';

export interface SupplierDeliveryNoteFilters {
  search?: string;
  supplierId?: string;
  status?: string;
  dateFrom?: string;
  dateTo?: string;
}

@Injectable({ providedIn: 'root' })
export class SupplierDeliveryNoteService {
  private readonly base = `${environment.apiUrl}/supplier-delivery-notes`;
  constructor(private readonly http: HttpClient) {}

  list(page: number, filters: SupplierDeliveryNoteFilters = {}) {
    let params = new HttpParams().set('page', page).set('pageSize', 20);
    for (const [key, value] of Object.entries(filters)) {
      if (value) params = params.set(key, value);
    }
    return this.http.get<PagedResult<SupplierDeliveryNoteListItem>>(this.base, { params });
  }

  get(id: string) {
    return this.http.get<SupplierDeliveryNote>(`${this.base}/${id}`);
  }

  findArticle(id: string, supplierItemCode: string) {
    const params = new HttpParams().set('supplierItemCode', supplierItemCode);
    return this.http.get<SupplierArticleMatch>(`${this.base}/${id}/article-match`, { params });
  }

  upload(data: FormData) {
    return this.http.post<SupplierDeliveryNote>(this.base, data);
  }

  save(id: string, request: SaveSupplierDeliveryNoteLines, parsed = false) {
    return parsed
      ? this.http.post<SupplierDeliveryNote>(`${this.base}/${id}/parse`, request)
      : this.http.put<SupplierDeliveryNote>(`${this.base}/${id}/lines`, request);
  }

  action(id: string, version: string, action: 'confirm' | 'cancel') {
    return this.http.post<SupplierDeliveryNote>(`${this.base}/${id}/${action}`, { version });
  }

  download(id: string) {
    return this.http.get(`${this.base}/${id}/file`, { responseType: 'blob' });
  }
}
