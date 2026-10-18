import { of, throwError } from 'rxjs';
import { ApiService } from './api.service';
import { CartItem, CassaPreviewState, CassaSessionService, SavedSessionDetail } from './cassa-session.service';

describe('CassaSessionService fiscal inputs', () => {
  let api: jasmine.SpyObj<ApiService>;
  let service: CassaSessionService;

  beforeEach(() => {
    localStorage.removeItem('mauw_cassa_session');
    api = jasmine.createSpyObj<ApiService>('ApiService', ['post', 'patch']);
    api.post.and.returnValue(of(detail()));
    api.patch.and.returnValue(of(detail()));
    service = new CassaSessionService(api);
  });

  afterEach(() => localStorage.removeItem('mauw_cassa_session'));

  it('sends net price, discount and VAT without any gross price', () => {
    const item: CartItem = { ...cartItem(), discountPercent: 10 };
    service.setCart([item]);
    service.setContact({ id: 'contact', businessName: 'Cliente', vatNumber: '', defaultDiscountRate: 0 });

    service.createOrPauseSession();

    const body = api.post.calls.mostRecent().args[1] as { items: Record<string, unknown>[] };
    expect(body.items[0]).toEqual(jasmine.objectContaining({
      unitNetPrice: 18.84, vatRate: 22, discountPercent: 10, quantity: 5
    }));
    expect('unitGrossPrice' in body.items[0]).toBeFalse();
  });

  it('round-trips persisted net prices unchanged', () => {
    service.applyLoadedSession(detail());
    const restoredService = new CassaSessionService(api);

    restoredService.createOrPauseSession();

    expect(api.patch).toHaveBeenCalledWith('v1/cassa/sessions/session/pause', jasmine.objectContaining({
      items: [jasmine.objectContaining({ unitNetPrice: 18.84, vatRate: 22, discountPercent: 0 })]
    }));
  });

  it('preserves zero-rate Nature and discounts across session reloads', () => {
    const saved = detail();
    saved.items[0] = { ...saved.items[0], vatRate: 0, natureCode: 'N3.2', discountPercent: 12.5 };
    service.applyLoadedSession(saved);

    service.createOrPauseSession();

    expect(api.patch).toHaveBeenCalledWith('v1/cassa/sessions/session/pause', jasmine.objectContaining({
      items: [jasmine.objectContaining({ vatRate: 0, natureCode: 'N3.2', discountPercent: 12.5 })]
    }));
    expect(service.snapshot.cart[0].discountPercent).toBe(12.5);
  });

  it('requests the preview from the backend and exposes its result without local calculation', () => {
    const preview = {
      lines: [{ lineNumber: 1, unitNetPrice: 18.84, discountPercent: 0, vatRate: 22, natureCode: null, lineNetTotal: 94.2 }],
      vatSummary: [{ vatRate: 22, natureCode: null, taxableAmount: 94.2, vatAmount: 20.72 }],
      totalTaxable: 94.2, totalVat: 20.72, totalGross: 114.92
    };
    api.post.and.returnValue(of(preview) as never);
    let state: CassaPreviewState | undefined;
    service.preview$.subscribe(s => state = s);

    service.setCart([cartItem()]);

    expect(api.post).toHaveBeenCalledWith('v1/cassa/sessions/preview', {
      items: [jasmine.objectContaining({ unitNetPrice: 18.84, vatRate: 22, quantity: 5 })]
    });
    expect(state?.data).toEqual(preview);
    expect(state?.error).toBeNull();
  });

  it('exposes the backend validation error and clears the preview for an empty cart', () => {
    api.post.and.returnValue(throwError(() => ({ error: 'Natura obbligatoria' })) as never);
    let state: CassaPreviewState | undefined;
    service.preview$.subscribe(s => state = s);

    service.setCart([cartItem()]);
    expect(state).toEqual({ data: null, error: 'Natura obbligatoria' });

    api.post.calls.reset();
    service.setCart([]);
    expect(api.post).not.toHaveBeenCalled();
    expect(state).toEqual({ data: null, error: null });
  });

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
        variantId: 'variant', barcode: '123', productId: 'product', productName: 'Articolo',
        size: 'M', color: 'Blu', unitNetPrice: 18.84,
        vatRate: 22, quantity: 5, maxQuantity: 10
      }]
    };
  }
});
