import { HttpClientTestingModule, HttpTestingController } from '@angular/common/http/testing';
import { HttpErrorResponse } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { PriceFormula, PriceFormulaCalculation } from '../models/price-formula.model';
import {
  PriceFormulaService,
  isPriceFormulaNotConfigured,
  priceFormulaErrorMessage
} from './price-formula.service';

describe('PriceFormulaService', () => {
  let service: PriceFormulaService;
  let http: HttpTestingController;

  const formula: PriceFormula = {
    isConfigured: true,
    steps: [{ label: 'Ricarico', percentage: 40 }, { label: 'Provvigione', percentage: 10 }],
    roundingStep: 0.1,
    updatedAt: '2026-10-25T10:00:00Z',
    updatedByUserId: 'user-1'
  };

  beforeEach(() => {
    TestBed.configureTestingModule({ imports: [HttpClientTestingModule] });
    service = TestBed.inject(PriceFormulaService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('gets the saved formula', () => {
    let result: PriceFormula | undefined;
    service.get().subscribe(value => result = value);
    const request = http.expectOne('/api/v1/settings/price-formula');
    expect(request.request.method).toBe('GET');
    request.flush(formula);
    expect(result).toEqual(formula);
  });

  it('gets the not configured state', () => {
    let result: PriceFormula | undefined;
    service.get().subscribe(value => result = value);
    http.expectOne('/api/v1/settings/price-formula').flush(
      { isConfigured: false, steps: [], roundingStep: 0.1, updatedAt: null, updatedByUserId: null });
    expect(result?.isConfigured).toBeFalse();
    expect(result?.steps).toEqual([]);
  });

  it('saves the formula with PUT and exactly the steps and rounding step', () => {
    let result: PriceFormula | undefined;
    const body = { steps: formula.steps, roundingStep: 0.05 };
    service.save(body).subscribe(value => result = value);
    const request = http.expectOne('/api/v1/settings/price-formula');
    expect(request.request.method).toBe('PUT');
    expect(request.request.body).toEqual(body);
    request.flush({ ...formula, roundingStep: 0.05 });
    expect(result?.roundingStep).toBe(0.05);
  });

  it('calculates with POST sending only the purchase cost', () => {
    const calculation: PriceFormulaCalculation = {
      purchaseCost: 20,
      steps: [{ label: 'Ricarico', percentage: 40, runningValue: 28 }],
      unroundedPrice: 28, roundingStep: 0.1, finalPrice: 28
    };
    let result: PriceFormulaCalculation | undefined;
    service.calculate(20).subscribe(value => result = value);
    const request = http.expectOne('/api/v1/products/price-formula/calculate');
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual({ purchaseCost: 20 });
    request.flush(calculation);
    expect(result).toEqual(calculation);
  });

  it('propagates ProblemDetails errors and maps them to the detail message', () => {
    let error: unknown;
    service.calculate(20).subscribe({ error: e => error = e });
    http.expectOne('/api/v1/products/price-formula/calculate').flush(
      { title: 'Bad Request', detail: 'Formula prezzi non configurata: impostarla in Impostazioni > Formula prezzi.' },
      { status: 400, statusText: 'Bad Request' });
    expect(error instanceof HttpErrorResponse).toBeTrue();
    const message = priceFormulaErrorMessage(error, 'fallback');
    expect(message).toContain('Formula prezzi non configurata');
    expect(isPriceFormulaNotConfigured(message)).toBeTrue();
  });

  it('falls back to title, plain text or the default message', () => {
    const make = (body: unknown) => new HttpErrorResponse({ status: 400, error: body });
    expect(priceFormulaErrorMessage(make({ title: 'Titolo' }), 'fallback')).toBe('Titolo');
    expect(priceFormulaErrorMessage(make('Testo'), 'fallback')).toBe('Testo');
    expect(priceFormulaErrorMessage(make(null), 'fallback')).toBe('fallback');
    expect(priceFormulaErrorMessage(new Error('x'), 'fallback')).toBe('fallback');
    expect(isPriceFormulaNotConfigured('Errore generico')).toBeFalse();
  });
});
