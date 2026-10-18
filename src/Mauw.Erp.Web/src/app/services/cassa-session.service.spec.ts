import { of } from 'rxjs';
import { ApiService } from './api.service';
import { CartItem, CassaSessionService, SavedSessionDetail } from './cassa-session.service';

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

  it('sends the original gross price for decimal scorporo in the backend', () => {
    const item: CartItem = {
      barcode: '123', variantId: 'variant', productId: 'product', productName: 'Articolo',
      size: 'M', color: 'Blu', unitNetPrice: 23 / 1.22, unitGrossPrice: 23,
      vatRate: 22, quantityAvailable: 10, quantity: 5, lineNetTotal: 5 * 23 / 1.22,
      lineGrossTotal: 115, maxQuantity: 10
    };
    service.setCart([item]);
    service.setContact({ id: 'contact', businessName: 'Cliente', vatNumber: '', defaultDiscountRate: 0 });

    service.createOrPauseSession();

    expect(api.post).toHaveBeenCalledWith('v1/cassa/sessions', jasmine.objectContaining({
      items: [jasmine.objectContaining({ unitGrossPrice: 23, vatRate: 22, discountPercent: 0 })]
    }));
  });

  it('round-trips calculated net prices without recomputing scorporo from display gross', () => {
    service.applyLoadedSession(detail());
    const restoredService = new CassaSessionService(api);

    restoredService.createOrPauseSession();

    expect(api.patch).toHaveBeenCalledWith('v1/cassa/sessions/session/pause', jasmine.objectContaining({
      items: [jasmine.objectContaining({
        unitNetPrice: 18.85245902, unitGrossPrice: undefined, vatRate: 22, discountPercent: 0
      })]
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

  function detail(): SavedSessionDetail {
    return {
      id: 'session', sessionCode: 'ORD-TEST', contactId: 'contact',
      contactName: 'Cliente', contactVatNumber: null, documentReference: null,
      items: [{
        variantId: 'variant', barcode: '123', productId: 'product', productName: 'Articolo',
        size: 'M', color: 'Blu', unitNetPrice: 18.85245902, unitGrossPrice: 23.0000000044,
        vatRate: 22, quantity: 5, maxQuantity: 10
      }]
    };
  }
});
