import { Injectable } from '@angular/core';
import { HttpErrorResponse } from '@angular/common/http';
import { ApiService } from './api.service';
import { PriceList, PriceListRequest, PriceResolution, PriceResolutionRequest } from '../models/price-list.model';

export function priceError(error: unknown): string {
  if (error instanceof HttpErrorResponse) {
    const body: unknown = error.error;
    if (typeof body === 'string') return body;
    if (body && typeof body === 'object') {
      if ('detail' in body && typeof body.detail === 'string') return body.detail;
      if ('title' in body && typeof body.title === 'string') return body.title;
    }
  }
  return 'Operazione listino non disponibile. Verificare i dati e riprovare.';
}

@Injectable({ providedIn: 'root' })
export class PriceListService {
  private readonly endpoint = 'v1/price-lists';
  constructor(private api: ApiService) {}
  getAll() { return this.api.get<PriceList[]>(this.endpoint); }
  get(id: string) { return this.api.get<PriceList>(`${this.endpoint}/${id}`); }
  create(body: PriceListRequest) { return this.api.post<PriceList>(this.endpoint, body); }
  update(id: string, body: PriceListRequest) { return this.api.put<PriceList>(`${this.endpoint}/${id}`, body); }
  delete(id: string) { return this.api.delete<void>(`${this.endpoint}/${id}`); }
  activate(id: string, isActive: boolean) {
    return this.api.patch<void>(`${this.endpoint}/${id}/activation`, { isActive });
  }
  resolve(body: PriceResolutionRequest) { return this.api.post<PriceResolution>(`${this.endpoint}/resolve`, body); }
}
