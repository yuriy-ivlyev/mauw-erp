import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { environment } from '../../environments/environment';
import {
  SupplierPurchaseOrder, SupplierReconciliation, SupplierReturnDraft
} from '../models/supplier-purchase-order.model';

export interface CreateSupplierPurchaseOrder {
  supplierId: string;
  orderDate: string;
  notes: string;
  lines: { articleVariantId: string; quantityOrdered: number; unitCost: string }[];
}

@Injectable({ providedIn: 'root' })
export class SupplierPurchaseOrderService {
  private readonly base = `${environment.apiUrl}/supplier-purchase-orders`;
  constructor(private readonly http: HttpClient) {}

  list(supplierId?: string, openOnly = false) {
    let params = new HttpParams().set('openOnly', openOnly);
    if (supplierId) params = params.set('supplierId', supplierId);
    return this.http.get<SupplierPurchaseOrder[]>(this.base, { params });
  }

  get(id: string) {
    return this.http.get<SupplierPurchaseOrder>(`${this.base}/${id}`);
  }

  create(request: CreateSupplierPurchaseOrder) {
    return this.http.post<SupplierPurchaseOrder>(this.base, request);
  }

  confirm(id: string, version: string) {
    return this.http.post<SupplierPurchaseOrder>(`${this.base}/${id}/confirm`, { version });
  }

  reconcile(id: string) {
    return this.http.get<SupplierReconciliation>(`${this.base}/${id}/reconciliation`);
  }

  shortages(id: string) {
    return this.http.get<{ articleVariantId: string; sku: string; articleDescription: string; quantityMissing: number }[]>(
      `${this.base}/${id}/shortages`);
  }

  createReturnDraft(id: string, lines: { supplierDeliveryNoteLineId: string; quantity: number }[]) {
    return this.http.post<SupplierReturnDraft>(`${this.base}/${id}/return-drafts`, { lines });
  }

  returnDrafts(id: string) {
    return this.http.get<SupplierReturnDraft[]>(`${this.base}/${id}/return-drafts`);
  }

  issueReturnDraft(id: string, draftId: string, version: string) {
    return this.http.post<SupplierReturnDraft>(`${this.base}/${id}/return-drafts/${draftId}/issue`, { version });
  }

  close(id: string, version: string) {
    return this.http.post<SupplierPurchaseOrder>(`${this.base}/${id}/close`, { version });
  }
}
