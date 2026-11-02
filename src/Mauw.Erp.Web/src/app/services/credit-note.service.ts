import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';
import {
  CreditNoteIssued,
  CreditNoteDetail,
  CreditNotePreview,
  CreditNoteSummary,
  CreateCreditNoteRequest,
  AvailableCreditNote
} from '../models/credit-note.model';

@Injectable({ providedIn: 'root' })
export class CreditNoteService {
  private readonly base = `${environment.apiUrl}/v1/credit-notes`;
  private readonly contactsBase = `${environment.apiUrl}/v1/contacts`;

  constructor(private readonly http: HttpClient) {}

  previewCreditNote(request: CreateCreditNoteRequest): Observable<CreditNotePreview> {
    return this.http.post<CreditNotePreview>(`${this.base}/preview`, request);
  }

  createCreditNote(request: CreateCreditNoteRequest): Observable<CreditNoteIssued> {
    return this.http.post<CreditNoteIssued>(this.base, request);
  }

  getById(id: string): Observable<CreditNoteDetail> {
    return this.http.get<CreditNoteDetail>(`${this.base}/${id}`);
  }

  transmitToSdi(id: string): Observable<CreditNoteSummary> {
    return this.http.post<CreditNoteSummary>(`${this.base}/${id}/transmit-sdi`, {});
  }

  downloadXml(id: string): Observable<Blob> {
    return this.http.get(`${this.base}/${id}/xml`, { responseType: 'blob' });
  }

  getAvailableForContact(contactId: string): Observable<AvailableCreditNote[]> {
    return this.http.get<AvailableCreditNote[]>(
      `${this.contactsBase}/${contactId}/available-credit-notes`);
  }
}
