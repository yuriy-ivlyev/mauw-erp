import { fakeAsync, tick } from '@angular/core/testing';
import { HttpErrorResponse } from '@angular/common/http';
import { Subject, of, throwError } from 'rxjs';
import { ApiService } from './api.service';
import { PriceListService } from './price-list.service';
import { PriceResolution } from '../models/price-list.model';
import { CartItem, CassaEmissionItem, CassaPreviewState, CassaSessionService, SavedSessionDetail } from './cassa-session.service';

describe('CassaSessionService pricing and fiscal inputs', () => {
  let api: jasmine.SpyObj<ApiService>;
  let prices: jasmine.SpyObj<PriceListService>;
  let service: CassaSessionService;
  const preview = {
    lines: [{ lineNumber: 1, unitNetPrice: 18.84, discountPercent: 10, vatRate: 22, natureCode: null, lineNetTotal: 84.78 }],
    vatSummary: [{ vatRate: 22, natureCode: null, taxableAmount: 84.78, vatAmount: 18.65 }],
    totalTaxable: 84.78, totalVat: 18.65, totalGross: 103.43
  };

  beforeEach(() => {
    localStorage.removeItem('mauw_cassa_session');
    api = jasmine.createSpyObj<ApiService>('ApiService', ['get', 'post', 'patch']);
    api.get.and.returnValue(of({ customerProfile: { priceListId: 'list' } }));
    api.post.and.callFake(endpoint => of(endpoint.endsWith('/preview') ? preview : detail()) as never);
    api.patch.and.returnValue(of(detail()));
    prices = jasmine.createSpyObj<PriceListService>('PriceListService', ['resolve']);
    prices.resolve.and.callFake(request => of({
      priceListId: 'list',
      lines: request.lines.map(line => ({
        variantId: line.variantId, priceListId: 'list', ruleId: 'rule', listUnitPrice: 18.84,
        discountPercent: 10, agreedUnitNetPrice: 16.956, vatRate: 22
      }))
    }));
    service = new CassaSessionService(api, prices);
  });
  afterEach(() => localStorage.removeItem('mauw_cassa_session'));

  function ready(): void {
    service.setCart([cartItem()]);
    service.setContact({ id: 'contact', businessName: 'Cliente', vatNumber: '', defaultDiscountRate: 0 });
    tick(180);
  }

  it('loads the customer default and sends one batch for all cart mutations', fakeAsync(() => {
    service.setContact({ id: 'contact', businessName: 'Cliente', vatNumber: '', defaultDiscountRate: 0 });
    service.setCart([cartItem()]);
    service.setCart([{ ...cartItem(), quantity: 6 }, { ...cartItem(), variantId: 'second' }]);
    tick(180);

    expect(prices.resolve).toHaveBeenCalledTimes(1);
    const request = prices.resolve.calls.mostRecent().args[0];
    expect(request.priceListId).toBe('list');
    expect(request.lines.map(line => line.quantity)).toEqual([6, 5]);
    expect(request.at.endsWith('Z')).toBeTrue();
  }));

  it('keeps pre-discount price and discount once in compatible cash preview', fakeAsync(() => {
    ready();
    expect(service.canSubmit).toBeTrue();
    expect(api.post).toHaveBeenCalledWith('v1/cassa/sessions/preview', jasmine.objectContaining({
      items: [jasmine.objectContaining({
        unitNetPrice: 18.84, discountPercent: 10, priceListId: 'list', isManualPriceOverride: false
      })]
    }));
    expect(service.snapshot.cart[0].agreedUnitNetPrice).toBe(16.956);
    const item = service.documentRequestItems()[0];
    expect('unitNetPrice' in item).toBeFalse();
    expect('discountPercent' in item).toBeFalse();
    expect('unitGrossPrice' in item).toBeFalse();
  }));

  it('preserves explicit override precision and reason when repricing quantities', fakeAsync(() => {
    ready();
    service.setCart([{ ...service.snapshot.cart[0], isPriceOverride: true, unitNetPrice: 12.12345678,
      discountPercent: 2, overrideReason: ' Accordo cliente ', quantity: 7 }]);
    tick(180);
    expect(service.documentRequestItems()[0]).toEqual(jasmine.objectContaining({
      unitNetPrice: 12.12345678, discountPercent: 2, overrideReason: 'Accordo cliente'
    }));
    expect(service.cartRequestItems()[0].isManualPriceOverride).toBeTrue();
    expect(service.snapshot.cart[0].unitNetPrice).toBe(12.12345678);
  }));

  it('blocks emission and pause for an override without a reason', fakeAsync(() => {
    ready();
    service.setCart([{ ...cartItem(), isPriceOverride: true }]);
    tick(180);
    expect(service.canSubmit).toBeFalse();
    let failed = false;
    service.createOrPauseSession().subscribe({ error: () => failed = true });
    expect(failed).toBeTrue();
    expect(api.patch).not.toHaveBeenCalled();
  }));

  it('round-trips the backend pricing snapshot and return provenance', fakeAsync(() => {
    const saved = detail();
    saved.items[0].pricingSnapshot = {
      priceListId: 'list', ruleId: 'rule', listUnitPrice: 18.84, agreedUnitNetPrice: 16.956,
      isManualOverride: false, overrideReason: null, isCustomerReturn: true, sourceLineId: 'invoice-item'
    };
    saved.items[0].natureCode = 'N3.2';
    service.applyLoadedSession(saved);
    tick(180);
    const restored = new CassaSessionService(api, prices);
    tick(180);
    restored.createOrPauseSession().subscribe();
    expect(api.patch).toHaveBeenCalledWith('v1/cassa/sessions/session/pause', jasmine.objectContaining({
      items: [jasmine.objectContaining({
        unitNetPrice: 18.84, natureCode: 'N3.2', isManualPriceOverride: false,
        overrideReason: null, sourceLineId: 'invoice-item', isCustomerReturn: true
      })]
    }));
    expect(prices.resolve).not.toHaveBeenCalled();
  }));

  it('exposes backend fiscal totals without local calculation', fakeAsync(() => {
    let state: CassaPreviewState | undefined;
    service.preview$.subscribe(value => state = value);
    ready();
    expect(state?.data).toEqual(preview);
    expect(state?.error).toBeNull();
    service.setCart([]);
    expect(state?.data).toBeNull();
    tick(180);
  }));

  it('blocks returns without an invoice line instead of quoting a replacement price', fakeAsync(() => {
    service.setCart([{ ...cartItem(), isCustomerReturn: true, quantity: -1 }]);
    tick(180);
    expect(prices.resolve).not.toHaveBeenCalled();
    expect(service.canSubmit).toBeFalse();
  }));

  it('does not reprice invoice return lines in a mixed cart', fakeAsync(() => {
    const invoiceReturn = { ...cartItem(), variantId: 'returned', quantity: -2, unitNetPrice: 12.12345678,
      discountPercent: 5, vatRate: 10, isCustomerReturn: true, sourceLineId: 'invoice-line' };
    service.setCart([cartItem(), invoiceReturn]);
    tick(180);
    expect(prices.resolve.calls.mostRecent().args[0].lines).toEqual([{ variantId: cartItem().variantId, quantity: 5 }]);
    expect(service.snapshot.cart[1]).toEqual(invoiceReturn);
    expect(service.cartRequestItems()[1].priceListId).toBeNull();
  }));

  it('cancels stale quotes and keeps submission disabled while repricing', fakeAsync(() => {
    const stale = new Subject<PriceResolution>();
    prices.resolve.and.returnValue(stale);
    service.setCart([cartItem()]);
    tick(180);
    expect(service.canSubmit).toBeFalse();
    service.setCart([{ ...cartItem(), quantity: 9 }]);
    stale.next({ priceListId: null, lines: [] });
    expect(service.snapshot.cart[0].quantity).toBe(9);
    tick(180);
    stale.complete();
  }));

  it('does not fall back silently when the resolver rejects ambiguous rules', fakeAsync(() => {
    prices.resolve.and.returnValue(throwError(() => new HttpErrorResponse({
      status: 400, error: { detail: 'Priorità ambigua' }
    })));
    let error: string | null = null;
    service.pricing$.subscribe(state => error = state.error);
    service.setCart([cartItem()]);
    tick(180);
    expect(error as string | null).toBe('Priorità ambigua');
    expect(service.canSubmit).toBeFalse();
    expect(api.post).not.toHaveBeenCalled();
  }));

  it('clears stale totals on backend fiscal validation errors', fakeAsync(() => {
    api.post.and.returnValue(throwError(() => ({ error: 'Natura obbligatoria' })));
    let state: CassaPreviewState | undefined;
    service.preview$.subscribe(value => state = value);
    service.setCart([cartItem()]);
    tick(180);
    expect(state).toEqual({ data: null, error: 'Natura obbligatoria' });
    expect(service.canSubmit).toBeFalse();
  }));

  it('never prices against a stale customer when the customer detail lookup fails', fakeAsync(() => {
    ready();
    api.get.and.returnValue(throwError(() => new HttpErrorResponse({ status: 503 })));
    service.setContact({ id: 'other-contact', businessName: 'Altro cliente', vatNumber: '', defaultDiscountRate: 0 });
    tick(180);
    expect(service.canSubmit).toBeFalse();
    expect(service.snapshot.selectedContact?.id).toBe('other-contact');
    api.get.and.returnValue(of({ customerProfile: { priceListId: 'other-list' } }));
    service.setContact(service.snapshot.selectedContact);
    tick(180);
    expect(prices.resolve.calls.mostRecent().args[0].priceListId).toBe('other-list');
    expect(service.canSubmit).toBeTrue();
  }));

  it('autosaves before emission and references only server-returned order items', fakeAsync(() => {
    ready();
    let emitted: CassaEmissionItem | undefined;
    service.prepareEmission().subscribe(saved => emitted = saved.items[0]);
    expect(api.post).toHaveBeenCalledWith('v1/cassa/sessions', jasmine.objectContaining({ contactId: 'contact' }));
    expect(emitted as CassaEmissionItem | undefined).toEqual({
      variantId: 'variant', quantity: 5, sourceOrderItemId: '12345678-1234-1234-1234-123456789012',
      vatRate: 22, natureCode: undefined
    });
    expect(service.snapshot.sessionId).toBe('session');
    expect(prices.resolve).toHaveBeenCalledTimes(1);
  }));

  it('updates existing session before emission instead of trusting previously loaded IDs', fakeAsync(() => {
    ready();
    service.setSessionRef('session', 'ORD-TEST');
    service.prepareEmission().subscribe();
    expect(api.patch).toHaveBeenCalledWith('v1/cassa/sessions/session/pause', jasmine.objectContaining({
      items: [jasmine.objectContaining({ quantity: 5 })]
    }));
  }));

  it('blocks emission when saved order item IDs or quantities are missing or inconsistent', fakeAsync(() => {
    ready();
    for (const invalid of [
      { ...detail(), contactId: 'other' },
      { ...detail(), items: [{ ...detail().items[0], orderItemId: undefined }] },
      { ...detail(), items: [{ ...detail().items[0], quantity: 99 }] }
    ]) {
      api.patch.and.returnValue(of(invalid));
      api.post.and.returnValue(of(invalid));
      let error: unknown;
      let emitted = false;
      service.prepareEmission().subscribe({ next: () => emitted = true, error: value => error = value });
      expect(emitted).toBeFalse();
      expect(error instanceof Error).toBeTrue();
    }
  }));

  function cartItem(): CartItem {
    return {
      barcode: '123', variantId: 'variant', productId: 'product', productName: 'Articolo',
      size: 'M', color: 'Blu', unitNetPrice: 18.84, vatRate: 22,
      quantityAvailable: 10, quantity: 5, maxQuantity: 10
    };
  }
  function detail(): SavedSessionDetail {
    return {
      id: 'session', sessionCode: 'ORD-TEST', contactId: 'contact',
      contactName: 'Cliente', contactVatNumber: null, documentReference: null,
      items: [{
        orderItemId: '12345678-1234-1234-1234-123456789012',
        variantId: 'variant', barcode: '123', productId: 'product', productName: 'Articolo',
        size: 'M', color: 'Blu', unitNetPrice: 18.84,
        vatRate: 22, quantity: 5, maxQuantity: 10
      }]
    };
  }
});
