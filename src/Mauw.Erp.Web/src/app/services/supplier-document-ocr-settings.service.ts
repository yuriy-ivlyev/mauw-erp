import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { environment } from '../../environments/environment';
import {
  CreateSupplierDocumentOcrHeaderAliasRequest,
  SupplierDocumentOcrHeaderAlias
} from '../models/supplier-document-ocr-alias.model';

@Injectable({ providedIn: 'root' })
export class SupplierDocumentOcrSettingsService {
  private readonly base = `${environment.apiUrl}/v1/settings/supplier-document-ocr/aliases`;

  constructor(private readonly http: HttpClient) {}

  list() {
    return this.http.get<SupplierDocumentOcrHeaderAlias[]>(this.base);
  }

  add(request: CreateSupplierDocumentOcrHeaderAliasRequest) {
    return this.http.post<SupplierDocumentOcrHeaderAlias>(this.base, request);
  }

  delete(id: string) {
    return this.http.delete<void>(`${this.base}/${id}`);
  }
}
