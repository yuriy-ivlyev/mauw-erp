import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';
import { CountryDto } from '../models/country.model';

@Injectable({ providedIn: 'root' })
export class CountryService {
  private readonly base = `${environment.apiUrl}/v1/lookups`;
  private cache: CountryDto[] | null = null;

  constructor(private http: HttpClient) {}

  getCountries(): Observable<CountryDto[]> {
    return this.http.get<CountryDto[]>(`${this.base}/countries`);
  }

  /** Pre-carica e mette in cache la lista nazioni per uso sincrono. */
  preload(): void {
    if (this.cache) return;
    this.getCountries().subscribe(c => (this.cache = c));
  }

  getCached(): CountryDto[] {
    return this.cache ?? [];
  }

  isEuMember(isoCode: string): boolean {
    return this.cache?.find(c => c.isoCode === isoCode)?.isEuMember ?? false;
  }
}
