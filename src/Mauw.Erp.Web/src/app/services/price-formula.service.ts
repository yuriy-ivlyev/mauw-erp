import { Injectable } from '@angular/core';
import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';
import {
  PriceFormula,
  PriceFormulaCalculation,
  SavePriceFormulaRequest
} from '../models/price-formula.model';

@Injectable({ providedIn: 'root' })
export class PriceFormulaService {
  private readonly settingsUrl = `${environment.apiUrl}/v1/settings/price-formula`;
  private readonly calculateUrl = `${environment.apiUrl}/v1/products/price-formula/calculate`;

  constructor(private http: HttpClient) {}

  get(): Observable<PriceFormula> {
    return this.http.get<PriceFormula>(this.settingsUrl);
  }

  save(request: SavePriceFormulaRequest): Observable<PriceFormula> {
    return this.http.put<PriceFormula>(this.settingsUrl, request);
  }

  calculate(purchaseCost: number): Observable<PriceFormulaCalculation> {
    return this.http.post<PriceFormulaCalculation>(this.calculateUrl, { purchaseCost });
  }
}

/** Extracts the user-facing message from a ProblemDetails (`detail`), plain-text or unknown error. */
export function priceFormulaErrorMessage(error: unknown, fallback: string): string {
  if (!(error instanceof HttpErrorResponse)) return fallback;
  const body: unknown = error.error;
  if (typeof body === 'string' && body.trim()) return body;
  if (body && typeof body === 'object') {
    const problem = body as { detail?: unknown; title?: unknown };
    if (typeof problem.detail === 'string' && problem.detail.trim()) return problem.detail;
    if (typeof problem.title === 'string' && problem.title.trim()) return problem.title;
  }
  return fallback;
}

export function isPriceFormulaNotConfigured(message: string): boolean {
  return /non configurata/i.test(message);
}
