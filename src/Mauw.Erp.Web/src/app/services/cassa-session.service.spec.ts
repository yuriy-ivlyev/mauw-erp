import { fakeAsync, tick } from '@angular/core/testing';
import { HttpErrorResponse } from '@angular/common/http';
import { Subject, of, throwError } from 'rxjs';
import { ApiService } from './api.service';
import { PriceListService } from './price-list.service';
import { PriceResolution } from '../models/price-list.model';
import {
  CASH_DISCOUNT_ERROR, CartItem, CashDiscount, CassaEmissionItem, CassaPreviewState, CassaSessionService,
  NOT_A_CUSTOMER_ERROR, SavedSessionDetail, cashDiscountError
} from './cassa-session.service';

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
        discountPercent: 10, agreedUnitNetPrice: 16.956, vatRate: 22, discountReason: 'Sconto magliette'
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

  it('never sends a manual price: a legacy stored override is discarded and repriced from the list', fakeAsync(() => {
    localStorage.setItem('mauw_cassa_session', JSON.stringify({
      sessionId: null, sessionCode: null, selectedContact: null, documentNumber: '',
      cart: [{ ...cartItem(), unitNetPrice: 12.12345678, isPriceOverride: true, overrideReason: 'Accordo' }]
    }));

    const restored = new CassaSessionService(api, prices);
    tick(180);

    const [item] = restored.snapshot.cart as Array<CartItem & { isPriceOverride?: boolean; overrideReason?: string }>;
    expect(item.isPriceOverride).toBeUndefined();
    expect(item.overrideReason).toBeUndefined();
    expect(item.unitNetPrice).toBe(18.84);
    expect(restored.cartRequestItems()[0]).toEqual(jasmine.objectContaining({
      isManualPriceOverride: false, overrideReason: null
    }));
    expect('unitNetPrice' in restored.documentRequestItems()[0]).toBeFalse();
    expect(restored.canSubmit).toBeTrue();
  }));

  it('sends the cash discount next to the list discount and keeps the server price', fakeAsync(() => {
    ready();
    const discount: CashDiscount = { mode: 'percent', value: 5, reason: ' Sconto alla cassa ' };
    service.setCart([{ ...cartItem(), cashDiscount: discount }]);
    tick(180);

    const [item] = service.snapshot.cart;
    expect(item.unitNetPrice).toBe(18.84);
    expect(item.discountPercent).toBe(10);
    expect(item.priceListDiscountReason).toBe('Sconto magliette');
    expect(service.canSubmit).toBeTrue();
    const request = jasmine.objectContaining({
      unitNetPrice: 18.84, discountPercent: 10, priceListDiscountReason: 'Sconto magliette',
      isManualPriceOverride: false, overrideReason: null,
      cashDiscount: { percent: 5, amount: null, reason: 'Sconto alla cassa' }
    });
    expect(service.cartRequestItems()[0]).toEqual(request);
    expect(api.post).toHaveBeenCalledWith('v1/cassa/sessions/preview', jasmine.objectContaining({ items: [request] }));
  }));

  it('sends a fixed-amount cash discount as an amount, never converted to a percentage locally', fakeAsync(() => {
    ready();
    service.setCart([{ ...cartItem(), cashDiscount: { mode: 'amount', value: 4.2, reason: 'Difetto estetico' } }]);
    tick(180);

    expect(service.cartRequestItems()[0].cashDiscount).toEqual({ percent: null, amount: 4.2, reason: 'Difetto estetico' });
    expect(service.snapshot.cart[0].discountPercent).toBe(10);
    expect(service.canSubmit).toBeTrue();
  }));

  it('blocks the cart while a cash discount is invalid and never asks the backend to price it', fakeAsync(() => {
    ready();
    let error: string | null = null;
    service.pricing$.subscribe(state => error = state.error);
    const invalid: CashDiscount[] = [
      { mode: 'percent', value: 0, reason: 'Motivo' },
      { mode: 'percent', value: 100.01, reason: 'Motivo' },
      { mode: 'percent', value: -5, reason: 'Motivo' },
      { mode: 'percent', value: Number.NaN, reason: 'Motivo' },
      { mode: 'amount', value: 1.005, reason: 'Motivo' },
      { mode: 'amount', value: 0, reason: 'Motivo' },
      { mode: 'percent', value: 5, reason: '   ' },
      { mode: 'percent', value: 5, reason: 'x'.repeat(501) }
    ];

    for (const cashDiscount of invalid) {
      api.post.calls.reset();
      service.setCart([{ ...cartItem(), cashDiscount }]);
      tick(180);
      expect(error as string | null).toBe(CASH_DISCOUNT_ERROR);
      expect(service.canSubmit).toBeFalse();
      expect(api.post).not.toHaveBeenCalled();
    }
  }));

  it('accepts a justified full discount and returns to the list discount once the cash one is removed', fakeAsync(() => {
    ready();
    service.setCart([{ ...cartItem(), cashDiscount: { mode: 'percent', value: 100, reason: 'Omaggio autorizzato' } }]);
    tick(180);
    expect(service.canSubmit).toBeTrue();

    service.setCart([{ ...service.snapshot.cart[0], cashDiscount: null }]);
    tick(180);

    expect(service.cartRequestItems()[0].cashDiscount).toBeNull();
    expect(service.snapshot.cart[0].discountPercent).toBe(10);
    expect(service.canSubmit).toBeTrue();
  }));

  it('never sends a cash discount for invoice returns', fakeAsync(() => {
    const invoiceReturn: CartItem = {
      ...cartItem(), quantity: -1, isCustomerReturn: true, sourceLineId: 'invoice-line',
      cashDiscount: { mode: 'percent', value: 5, reason: 'Motivo' }
    };
    service.setCart([invoiceReturn]);
    tick(180);

    expect(service.cartRequestItems()[0].cashDiscount).toBeNull();
  }));

  it('restores cash discounts and list reasons from the saved snapshot when a session is resumed', fakeAsync(() => {
    const saved = detail();
    saved.items[0].discountPercent = 14.5;
    saved.items[0].pricingSnapshot = {
      priceListId: 'list', ruleId: 'rule', listUnitPrice: 18.84, agreedUnitNetPrice: 16.1,
      isManualOverride: true, overrideReason: 'Sconto alla cassa', isCustomerReturn: false, sourceLineId: null,
      discounts: [
        { source: 'PriceList', reason: 'Sconto magliette', percent: 10, amount: null },
        { source: 'Cashier', reason: 'Sconto alla cassa', percent: null, amount: 2.5 }
      ]
    };

    service.applyLoadedSession(saved);

    expect(service.snapshot.cart[0].cashDiscount).toEqual({ mode: 'amount', value: 2.5, reason: 'Sconto alla cassa' });
    expect(service.snapshot.cart[0].priceListDiscountReason).toBe('Sconto magliette');
    tick(180);
  }));

  it('restores the single manual discount of sessions saved before the discount list existed', fakeAsync(() => {
    const saved = detail();
    saved.items[0].discountPercent = 15;
    saved.items[0].pricingSnapshot = {
      priceListId: 'list', ruleId: 'rule', listUnitPrice: 18.84, agreedUnitNetPrice: 16.014,
      isManualOverride: true, overrideReason: 'Cliente storico', isCustomerReturn: false, sourceLineId: null
    };

    service.applyLoadedSession(saved);

    expect(service.snapshot.cart[0].cashDiscount).toEqual({ mode: 'percent', value: 15, reason: 'Cliente storico' });
    tick(180);
  }));

  it('moves a cart stored with the previous single-discount format to the new cash discount', fakeAsync(() => {
    localStorage.setItem('mauw_cassa_session', JSON.stringify({
      sessionId: null, sessionCode: null, selectedContact: null, documentNumber: '',
      cart: [{ ...cartItem(), manualDiscountPercent: 12, discountReason: 'Cliente storico' }]
    }));

    const restored = new CassaSessionService(api, prices);
    tick(180);

    const [item] = restored.snapshot.cart as Array<CartItem & { manualDiscountPercent?: number }>;
    expect(item.manualDiscountPercent).toBeUndefined();
    expect(item.cashDiscount).toEqual({ mode: 'percent', value: 12, reason: 'Cliente storico' });
  }));

  it('explains why a cash discount is not valid', () => {
    expect(cashDiscountError({ mode: 'percent', value: 5, reason: 'Motivo' })).toBeNull();
    expect(cashDiscountError({ mode: 'amount', value: 4.2, reason: 'Motivo' })).toBeNull();
    expect(cashDiscountError({ mode: 'percent', value: 100, reason: 'Motivo' })).toBeNull();
    expect(cashDiscountError({ mode: 'percent', value: 100.5, reason: 'Motivo' })).toContain('percentuale');
    expect(cashDiscountError({ mode: 'amount', value: 1.005, reason: 'Motivo' })).toContain('due decimali');
    expect(cashDiscountError({ mode: 'amount', value: 5, reason: ' ' })).toContain('causale');
    expect(cashDiscountError({ mode: 'amount', value: 5, reason: 'x'.repeat(501) })).toContain('500');
  });

  it('keeps list price and discount for every line, so a quantity change never needs a reason', fakeAsync(() => {
    ready();
    service.setCart([{ ...service.snapshot.cart[0], unitNetPrice: 1, discountPercent: 99, quantity: 7 }]);
    tick(180);

    expect(service.snapshot.cart[0].unitNetPrice).toBe(18.84);
    expect(service.snapshot.cart[0].discountPercent).toBe(10);
    expect(service.canSubmit).toBeTrue();
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

  it('refuses to sell to a contact without a customer profile', fakeAsync(() => {
    api.get.and.returnValue(of({ customerProfile: null }));
    let error: string | null = null;
    service.pricing$.subscribe(state => error = state.error);

    service.setCart([cartItem()]);
    service.setContact({ id: 'agent', businessName: 'Agente', vatNumber: '', defaultDiscountRate: 0 });
    tick(180);

    expect(error as string | null).toBe(NOT_A_CUSTOMER_ERROR);
    expect(service.canSubmit).toBeFalse();
    expect(prices.resolve).not.toHaveBeenCalled();
    expect(api.post).not.toHaveBeenCalled();
  }));

  it('exposes all backend line and document totals for a multi-line register preview', fakeAsync(() => {
    const multiLinePreview = {
      lines: [
        { lineNumber: 1, unitNetPrice: 18.84, discountPercent: 10, vatRate: 22, natureCode: null, lineNetTotal: 84.78 },
        { lineNumber: 2, unitNetPrice: 18.84, discountPercent: 10, vatRate: 22, natureCode: null, lineNetTotal: 33.91 }
      ],
      vatSummary: [{ vatRate: 22, natureCode: null, taxableAmount: 118.69, vatAmount: 26.11 }],
      totalTaxable: 118.69, totalVat: 26.11, totalGross: 144.80
    };
    api.post.and.callFake(endpoint => of(endpoint.endsWith('/preview') ? multiLinePreview : detail()) as never);
    let state: CassaPreviewState | undefined;
    service.preview$.subscribe(value => state = value);

    service.setCart([cartItem(), { ...cartItem(), variantId: 'second', quantity: 2 }]);
    service.setContact({ id: 'contact', businessName: 'Cliente', vatNumber: '', defaultDiscountRate: 0 });
    tick(180);

    expect(state?.data?.lines.map(line => line.lineNetTotal)).toEqual([84.78, 33.91]);
    expect(state?.data?.totalTaxable).toBe(118.69);
    expect(state?.data?.totalVat).toBe(26.11);
    expect(state?.data?.totalGross).toBe(144.80);
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
