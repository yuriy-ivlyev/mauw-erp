import { TestBed, fakeAsync, tick } from '@angular/core/testing';
import { of } from 'rxjs';
import { Router } from '@angular/router';
import { ApiService } from '../../../services/api.service';
import { ProductService } from '../../../services/product.service';
import { PriceListService } from '../../../services/price-list.service';
import { CartItem, CassaPreview, CassaPreviewLine, CassaSessionService } from '../../../services/cassa-session.service';
import { BancoCassaComponent } from './banco-cassa.component';

function cartItem(overrides: Partial<CartItem> = {}): CartItem {
  return {
    barcode: '123', variantId: 'variant', productId: 'product', productName: 'Articolo', size: 'M', color: 'Blu',
    unitNetPrice: 31, vatRate: 22, quantityAvailable: 10, quantity: 5, maxQuantity: 10, ...overrides
  };
}

function previewLine(overrides: Partial<CassaPreviewLine> = {}): CassaPreviewLine {
  return {
    lineNumber: 1, unitNetPrice: 31, discountPercent: 0, vatRate: 22, natureCode: null, lineNetTotal: 155,
    discountedUnitPrice: 31, discounts: [], ...overrides
  };
}

function preview(lines: CassaPreviewLine[]): CassaPreview {
  return { lines, vatSummary: [], totalTaxable: 84.78, totalVat: 18.65, totalGross: 103.43 };
}

async function setup(cart: CartItem[], data: CassaPreview | null = null) {
  const state = { sessionId: null, sessionCode: null, cart, selectedContact: null, documentNumber: '' };
  const sessionService = {
    snapshot: state, state$: of(state), pricing$: of({ pending: false, error: null }),
    preview$: of({ data, error: null }), canSubmit: true,
    setCart: jasmine.createSpy('setCart'), setContact: jasmine.createSpy('setContact'),
    setDocumentNumber: jasmine.createSpy('setDocumentNumber')
  };
  await TestBed.configureTestingModule({
    imports: [BancoCassaComponent],
    providers: [
      { provide: ApiService, useValue: { get: () => of({ items: [] }) } },
      { provide: ProductService, useValue: {} },
      { provide: Router, useValue: {} },
      { provide: PriceListService, useValue: { getAll: () => of([]) } },
      { provide: CassaSessionService, useValue: sessionService }
    ]
  }).compileComponents();
  const fixture = TestBed.createComponent(BancoCassaComponent);
  fixture.detectChanges();
  return { fixture, element: fixture.nativeElement as HTMLElement, sessionService };
}

describe('BancoCassaComponent fiscal footer', () => {
  it('renders backend line, taxable, VAT and document totals', async () => {
    const { element } = await setup([cartItem({ unitNetPrice: 18.84 })],
      preview([previewLine({ unitNetPrice: 18.84, discountPercent: 10, lineNetTotal: 84.78 })]));
    const text = element.textContent as string;

    expect(text).toContain('84.78 €');
    expect(text).toContain('18.65 €');
    expect(text).toContain('103.43 €');
  });

  it('searches only customer contacts, which are the only ones with a price profile', fakeAsync(() => {
    const api = jasmine.createSpyObj<ApiService>('ApiService', ['get']);
    api.get.and.returnValue(of({ items: [], totalCount: 0 }) as never);
    const state = { sessionId: null, sessionCode: null, cart: [], selectedContact: null, documentNumber: '' };
    TestBed.configureTestingModule({
      imports: [BancoCassaComponent],
      providers: [
        { provide: ApiService, useValue: api },
        { provide: ProductService, useValue: {} },
        { provide: Router, useValue: {} },
        { provide: PriceListService, useValue: { getAll: () => of([]) } },
        { provide: CassaSessionService, useValue: {
          snapshot: state, state$: of(state), pricing$: of({ pending: false, error: null }),
          preview$: of({ data: null, error: null }), canSubmit: false
        } }
      ]
    });
    const component = TestBed.createComponent(BancoCassaComponent).componentInstance;
    component.contactSearch = 'Rossi & C';

    component.onContactSearch();
    tick(300);

    const url = api.get.calls.allArgs().map(args => args[0]).find(value => value.includes('v1/contacts'))!;
    expect(url).toContain('isCustomer=true');
    expect(url).toContain('search=Rossi%20%26%20C');
  }));
});

describe('BancoCassaComponent line discounts', () => {
  it('shows an undiscounted price plainly, with a discount button and no manual price controls', async () => {
    const { element } = await setup([cartItem()], preview([previewLine()]));

    expect(element.querySelector('[data-testid="plain-price"]')?.textContent).toContain('31.00 €');
    expect(element.querySelector('[data-testid="old-price"]')).toBeNull();
    expect(element.querySelector('[data-testid="new-price"]')).toBeNull();
    expect(element.querySelector('[data-testid="discount-badge"]')).toBeNull();
    expect(element.querySelector('[data-testid="discount-button"]')).not.toBeNull();
    expect(element.textContent).not.toContain('Override');
    expect(element.querySelector('input[type="checkbox"]')).toBeNull();
    expect(element.querySelector('[aria-label="Prezzo netto override"]')).toBeNull();
  });

  it('strikes the price in red and shows the new price in green when the list already discounts it', async () => {
    const { element } = await setup(
      [cartItem({ discountPercent: 10, priceListDiscountReason: 'Sconto magliette' })],
      preview([previewLine({ discountPercent: 10, discountedUnitPrice: 27.9, lineNetTotal: 139.5 })]));

    const oldPrice = element.querySelector('[data-testid="old-price"]') as HTMLElement;
    const newPrice = element.querySelector('[data-testid="new-price"]') as HTMLElement;
    expect(oldPrice.textContent).toContain('31.00 €');
    expect(oldPrice.classList).toContain('line-through');
    expect(oldPrice.classList).toContain('text-red-600');
    expect(newPrice.textContent).toContain('27.90 €');
    expect(newPrice.classList).toContain('text-green-700');
    expect(oldPrice.parentElement).toBe(newPrice.parentElement);
    expect(oldPrice.parentElement?.classList).toContain('flex');
    expect(oldPrice.parentElement?.classList).toContain('whitespace-nowrap');
    expect(element.querySelector('[data-testid="plain-price"]')).toBeNull();
    expect(element.querySelector('[data-testid="discount-badge"]')?.textContent).toContain('10%');
  });

  it('uses compact cart rows and a red, accessible remove button', async () => {
    const { element } = await setup([cartItem()], preview([previewLine()]));
    const row = element.querySelector('[data-testid="cart-row"]') as HTMLElement;
    const removeButton = element.querySelector('[data-testid="remove-item"]') as HTMLButtonElement;

    expect(row.classList).toContain('py-2');
    expect(row.classList).not.toContain('py-3');
    expect(removeButton.getAttribute('aria-label')).toBe('Rimuovi riga');
    expect(removeButton.classList).toContain('text-red-600');
    expect(removeButton.classList).toContain('hover:bg-red-50');
    expect(removeButton.classList).toContain('focus-visible:ring-red-500');
  });

  it('shows the same strike and new price for a cash discount, using the backend effective values', async () => {
    const { element } = await setup(
      [cartItem({ discountPercent: 10, cashDiscount: { mode: 'percent', value: 5, reason: 'Sconto alla cassa' } })],
      preview([previewLine({ discountPercent: 14.5, discountedUnitPrice: 26.505, lineNetTotal: 132.53 })]));

    expect(element.querySelector('[data-testid="new-price"]')?.textContent).toContain('26.505 €');
    expect(element.querySelector('[data-testid="discount-badge"]')?.textContent).toContain('14.5%');
    expect(element.querySelector('[data-testid="discount-button"]')?.className).toContain('border-green-500');
  });

  it('falls back to the server-quoted discounted price while the preview is still loading', async () => {
    const { element } = await setup(
      [cartItem({ discountPercent: 10, agreedUnitNetPrice: 27.9 })], null);

    expect(element.querySelector('[data-testid="old-price"]')?.textContent).toContain('31.00 €');
    expect(element.querySelector('[data-testid="new-price"]')?.textContent).toContain('27.90 €');
  });

  it('does not offer a discount button on invoice return lines', async () => {
    const { element } = await setup(
      [cartItem({ isCustomerReturn: true, quantity: -1, sourceLineId: 'invoice-line' })], null);

    expect(element.querySelector('[data-testid="discount-button"]')).toBeNull();
  });

  it('opens the discount dialog with every discount and its reason, listing the list ones first', async () => {
    const item = cartItem({
      discountPercent: 10, priceListDiscountReason: 'Sconto magliette',
      cashDiscount: { mode: 'percent', value: 5, reason: 'Sconto alla cassa' }
    });
    const { fixture, element } = await setup([item], preview([previewLine({
      discountPercent: 14.5, discountedUnitPrice: 26.505,
      discounts: [
        { source: 'PriceList', reason: 'Sconto magliette', percent: 10, amount: null, savedAmount: 15.5 },
        { source: 'Cashier', reason: 'Sconto alla cassa', percent: 5, amount: null, savedAmount: 6.98 }
      ]
    })]));

    (element.querySelector('[data-testid="discount-button"]') as HTMLButtonElement).click();
    fixture.detectChanges();

    const dialog = element.querySelector('app-discount-dialog') as HTMLElement;
    expect(dialog).not.toBeNull();
    expect(dialog.textContent).toContain('1) Sconto magliette');
    expect(dialog.textContent).toContain('2) Sconto alla cassa');
    expect(dialog.textContent).toContain('(listino)');
    expect(dialog.textContent).toContain('(cassa)');
  });

  it('applies the cash discount chosen in the dialog and sends the cart to the server', async () => {
    const item = cartItem();
    const { fixture, element, sessionService } = await setup([item], preview([previewLine()]));
    (element.querySelector('[data-testid="discount-button"]') as HTMLButtonElement).click();
    fixture.detectChanges();

    fixture.componentInstance.onDiscountApplied({ mode: 'amount', value: 5, reason: 'Difetto estetico' });
    fixture.detectChanges();

    expect(item.cashDiscount).toEqual({ mode: 'amount', value: 5, reason: 'Difetto estetico' });
    expect(sessionService.setCart).toHaveBeenCalledWith([item]);
    expect(element.querySelector('app-discount-dialog')).toBeNull();
  });

  it('removes the cash discount and keeps the list discounts', async () => {
    const item = cartItem({
      discountPercent: 10, cashDiscount: { mode: 'percent', value: 5, reason: 'Sconto alla cassa' }
    });
    const { fixture, element, sessionService } = await setup([item], preview([previewLine()]));
    (element.querySelector('[data-testid="discount-button"]') as HTMLButtonElement).click();
    fixture.detectChanges();

    fixture.componentInstance.onDiscountApplied(null);

    expect(item.cashDiscount).toBeNull();
    expect(item.discountPercent).toBe(10);
    expect(sessionService.setCart).toHaveBeenCalled();
  });

  it('closes the dialog without changing the cart', async () => {
    const item = cartItem();
    const { fixture, element, sessionService } = await setup([item], null);
    (element.querySelector('[data-testid="discount-button"]') as HTMLButtonElement).click();
    fixture.detectChanges();

    fixture.componentInstance.closeDiscount();
    fixture.detectChanges();

    expect(element.querySelector('app-discount-dialog')).toBeNull();
    expect(item.cashDiscount).toBeUndefined();
    expect(sessionService.setCart).not.toHaveBeenCalled();
  });
});
