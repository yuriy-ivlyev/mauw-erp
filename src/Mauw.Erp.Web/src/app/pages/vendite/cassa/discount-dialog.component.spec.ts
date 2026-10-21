import { ComponentFixture, TestBed } from '@angular/core/testing';
import { CartItem, CashDiscount, CassaPreviewLine } from '../../../services/cassa-session.service';
import { DiscountDialogComponent } from './discount-dialog.component';

function cartItem(overrides: Partial<CartItem> = {}): CartItem {
  return {
    barcode: '123', variantId: 'variant', productId: 'product', productName: 'Maglietta', size: 'M', color: 'Blu',
    unitNetPrice: 31, vatRate: 22, quantityAvailable: 10, quantity: 5, maxQuantity: 10, ...overrides
  };
}

function line(overrides: Partial<CassaPreviewLine> = {}): CassaPreviewLine {
  return {
    lineNumber: 1, unitNetPrice: 31, discountPercent: 0, vatRate: 22, natureCode: null, lineNetTotal: 155,
    discountedUnitPrice: 31, discounts: [], ...overrides
  };
}

describe('DiscountDialogComponent', () => {
  let fixture: ComponentFixture<DiscountDialogComponent>;
  let applied: Array<CashDiscount | null>;
  let closed: number;

  async function open(item: CartItem, previewLine: CassaPreviewLine | null = null) {
    await TestBed.configureTestingModule({ imports: [DiscountDialogComponent] }).compileComponents();
    fixture = TestBed.createComponent(DiscountDialogComponent);
    fixture.componentRef.setInput('item', item);
    fixture.componentRef.setInput('line', previewLine);
    applied = [];
    closed = 0;
    fixture.componentInstance.applied.subscribe(value => applied.push(value));
    fixture.componentInstance.closed.subscribe(() => closed++);
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
  }

  const element = () => fixture.nativeElement as HTMLElement;
  const query = <T extends HTMLElement>(selector: string) => element().querySelector(selector) as T;
  const text = (selector: string) => query(selector)?.textContent ?? '';

  async function type(selector: string, value: string): Promise<void> {
    const input = query<HTMLInputElement>(selector);
    input.value = value;
    input.dispatchEvent(new Event('input'));
    fixture.detectChanges();
    await fixture.whenStable();
  }

  function click(selector: string): void {
    query<HTMLButtonElement>(selector).click();
    fixture.detectChanges();
  }

  const button = (label: string) =>
    Array.from(element().querySelectorAll('button')).find(item => item.textContent?.trim() === label) as HTMLButtonElement;

  it('tells that no discount is applied yet and offers to add one to the cash register', async () => {
    await open(cartItem());

    expect(element().textContent).toContain('Nessuno sconto applicato');
    expect(element().textContent).toContain('Aggiungi sconto alla cassa');
    expect(button('Rimuovi sconto cassa')).toBeUndefined();
  });

  it('lists each list discount with its own reason, then the cash discount, all numbered', async () => {
    await open(
      cartItem({ cashDiscount: { mode: 'percent', value: 5, reason: 'Sconto alla cassa' } }),
      line({
        discountPercent: 12.4575, discountedUnitPrice: 27.138,
        discounts: [
          { source: 'PriceList', reason: 'Sconto magliette', percent: 5, amount: null, savedAmount: 7.75 },
          { source: 'PriceList', reason: 'Sconto catalogo', percent: 3, amount: null, savedAmount: 4.41 },
          { source: 'Cashier', reason: 'Sconto alla cassa', percent: 5, amount: null, savedAmount: 7.1 }
        ]
      }));

    const items = Array.from(element().querySelectorAll('ol li')).map(li => li.textContent!.replace(/\s+/g, ' ').trim());
    expect(items.length).toBe(3);
    expect(items[0]).toContain('1) Sconto magliette (listino)');
    expect(items[0]).toContain('−5% (−7.75 €)');
    expect(items[1]).toContain('2) Sconto catalogo (listino)');
    expect(items[2]).toContain('3) Sconto alla cassa (cassa)');
    expect(items[2]).toContain('−5% (−7.10 €)');
    expect(element().textContent).toContain('Modifica sconto alla cassa');
  });

  it('shows the struck list price in red and the discounted one in green', async () => {
    await open(cartItem(), line({ discountPercent: 10, discountedUnitPrice: 27.9 }));

    const prices = query('[data-testid="dialog-prices"]');
    const struck = prices.querySelector('.line-through') as HTMLElement;
    const final = prices.querySelector('.text-green-700') as HTMLElement;
    expect(struck.textContent).toContain('31.00 €');
    expect(struck.classList).toContain('text-red-600');
    expect(final.textContent).toContain('27.90 €');
  });

  it('shows the price without strike when nothing is discounted', async () => {
    await open(cartItem(), line());

    expect(query('[data-testid="dialog-prices"] .line-through')).toBeNull();
    expect(text('[data-testid="dialog-prices"]')).toContain('31.00 €');
  });

  it('uses the item list discount and reason when the preview is not available yet', async () => {
    await open(cartItem({ discountPercent: 10, priceListDiscountReason: 'Sconto magliette' }));

    expect(text('[data-testid="list-discount"]')).toContain('1) Sconto magliette');
    expect(text('[data-testid="list-discount"]')).toContain('−10%');
  });

  it('applies a percentage discount with its trimmed reason', async () => {
    await open(cartItem());
    await type('[aria-label="Valore sconto"]', '7.5');
    await type('[aria-label="Causale sconto"]', '  Cliente storico  ');

    click('button.bg-blue-600.text-white.font-semibold');

    expect(applied).toEqual([{ mode: 'percent', value: 7.5, reason: 'Cliente storico' }]);
  });

  it('applies a fixed amount discount after switching to euros', async () => {
    await open(cartItem());
    click('[aria-label="Tipo di sconto"] button:last-child');
    await type('[aria-label="Valore sconto"]', '4.2');
    await type('[aria-label="Causale sconto"]', 'Difetto estetico');

    click('button.bg-blue-600.text-white.font-semibold');

    expect(applied).toEqual([{ mode: 'amount', value: 4.2, reason: 'Difetto estetico' }]);
    expect(element().textContent).toContain('sul totale della riga');
  });

  it('does not apply an invalid discount and explains why', async () => {
    await open(cartItem());
    const apply = () => click('button.bg-blue-600.text-white.font-semibold');

    apply();
    expect(text('[role="alert"]')).toContain('Inserire il valore');

    await type('[aria-label="Valore sconto"]', '0');
    apply();
    expect(text('[role="alert"]')).toContain('percentuale');

    await type('[aria-label="Valore sconto"]', '100.5');
    apply();
    expect(text('[role="alert"]')).toContain('percentuale');

    await type('[aria-label="Valore sconto"]', '10');
    apply();
    expect(text('[role="alert"]')).toContain('causale');

    click('[aria-label="Tipo di sconto"] button:last-child');
    await type('[aria-label="Valore sconto"]', '1.005');
    await type('[aria-label="Causale sconto"]', 'Motivo');
    apply();
    expect(text('[role="alert"]')).toContain('due decimali');

    expect(applied).toEqual([]);
  });

  it('prefills the existing cash discount and removes it on request', async () => {
    await open(cartItem({ cashDiscount: { mode: 'amount', value: 2.5, reason: 'Sconto alla cassa' } }));

    expect(query<HTMLInputElement>('[aria-label="Valore sconto"]').value).toBe('2.5');
    expect(query<HTMLInputElement>('[aria-label="Causale sconto"]').value).toBe('Sconto alla cassa');
    expect(query('[aria-label="Tipo di sconto"] button:last-child').getAttribute('aria-pressed')).toBe('true');

    button('Rimuovi sconto cassa').click();

    expect(applied).toEqual([null]);
  });

  it('modifies the existing cash discount instead of adding another one', async () => {
    await open(cartItem({ cashDiscount: { mode: 'percent', value: 5, reason: 'Sconto alla cassa' } }));
    await type('[aria-label="Valore sconto"]', '8');

    click('button.bg-blue-600.text-white.font-semibold');

    expect(applied).toEqual([{ mode: 'percent', value: 8, reason: 'Sconto alla cassa' }]);
  });

  it('closes with Annulla or Escape without applying anything', async () => {
    await open(cartItem());

    button('Annulla').click();
    query('[role="dialog"]').dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape' }));

    expect(closed).toBe(2);
    expect(applied).toEqual([]);
  });

  it('is an accessible modal dialog with a labelled title', async () => {
    await open(cartItem());

    const dialog = query('[role="dialog"]');
    expect(dialog.getAttribute('aria-modal')).toBe('true');
    expect(text(`#${dialog.getAttribute('aria-labelledby')}`)).toContain('Sconti sulla riga');
  });
});
