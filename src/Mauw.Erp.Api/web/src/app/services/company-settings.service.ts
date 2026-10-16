import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';
import {
  CompanySettings,
  UpdateCompanySettingsRequest,
  CompanyAddressSite,
  CompanyAddressSiteRequest
} from '../models/company-settings.model';

@Injectable({ providedIn: 'root' })
export class CompanySettingsService {
  private readonly base = `${environment.apiUrl}/v1/settings/company`;

  constructor(private http: HttpClient) {}

  getCompany(): Observable<CompanySettings> {
    return this.http.get<CompanySettings>(this.base);
  }

  upsertCompany(request: UpdateCompanySettingsRequest): Observable<CompanySettings> {
    return this.http.put<CompanySettings>(this.base, request);
  }

  addOperationalSite(request: CompanyAddressSiteRequest): Observable<CompanyAddressSite> {
    return this.http.post<CompanyAddressSite>(`${this.base}/operational-sites`, request);
  }

  updateOperationalSite(siteId: string, request: CompanyAddressSiteRequest): Observable<CompanyAddressSite> {
    return this.http.put<CompanyAddressSite>(`${this.base}/operational-sites/${siteId}`, request);
  }

  deleteOperationalSite(siteId: string): Observable<void> {
    return this.http.delete<void>(`${this.base}/operational-sites/${siteId}`);
  }
}
