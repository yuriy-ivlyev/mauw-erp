import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';
import {
  ContactSummary,
  ContactDetail,
  ContactFilterParams,
  PagedResult,
  CreateContactRequest,
  UpdateContactRequest,
  ToggleBlockRequest
} from '../models/contact.model';

@Injectable({ providedIn: 'root' })
export class ContactService {
  private readonly base = `${environment.apiUrl}/v1/contacts`;

  constructor(private http: HttpClient) {}

  getList(params: ContactFilterParams = {}): Observable<PagedResult<ContactSummary>> {
    let httpParams = new HttpParams();
    if (params.search)           httpParams = httpParams.set('search', params.search);
    if (params.isCustomer != null) httpParams = httpParams.set('isCustomer', String(params.isCustomer));
    if (params.isSupplier != null) httpParams = httpParams.set('isSupplier', String(params.isSupplier));
    if (params.isAgent    != null) httpParams = httpParams.set('isAgent',    String(params.isAgent));
    if (params.status)           httpParams = httpParams.set('status', params.status);
    if (params.sortBy)           httpParams = httpParams.set('sortBy', params.sortBy);
    if (params.sortDescending)   httpParams = httpParams.set('sortDescending', 'true');
    httpParams = httpParams
      .set('page',     String(params.page     ?? 1))
      .set('pageSize', String(params.pageSize ?? 20));
    return this.http.get<PagedResult<ContactSummary>>(this.base, { params: httpParams });
  }

  getById(id: string): Observable<ContactDetail> {
    return this.http.get<ContactDetail>(`${this.base}/${id}`);
  }

  create(request: CreateContactRequest): Observable<ContactDetail> {
    return this.http.post<ContactDetail>(this.base, request);
  }

  update(id: string, request: UpdateContactRequest): Observable<ContactDetail> {
    return this.http.put<ContactDetail>(`${this.base}/${id}`, request);
  }

  delete(id: string): Observable<void> {
    return this.http.delete<void>(`${this.base}/${id}`);
  }

  toggleBlock(id: string, request: ToggleBlockRequest): Observable<void> {
    return this.http.patch<void>(`${this.base}/${id}/customer-block`, request);
  }

  deleteAddress(contactId: string, addressId: string): Observable<void> {
    return this.http.delete<void>(`${this.base}/${contactId}/addresses/${addressId}`);
  }

  deletePerson(contactId: string, personId: string): Observable<void> {
    return this.http.delete<void>(`${this.base}/${contactId}/persons/${personId}`);
  }
}
