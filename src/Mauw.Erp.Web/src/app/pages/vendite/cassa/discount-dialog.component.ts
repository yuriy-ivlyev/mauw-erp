import { Component, EventEmitter, Input, OnInit, Output } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import {
  CartItem, CashDiscount, CashDiscountMode, CassaPreviewDiscount, CassaPreviewLine, cashDiscountError
} from '../../../services/cassa-session.service';

/**
 * Sconti di una riga della cassa. Mostra gli sconti del listino (sola lettura) e permette di aggiungere,
 * modificare o togliere lo sconto della cassa, in percentuale o a valore fisso, sempre con la sua causale.
 * Nessun calcolo qui: prezzi e importi risparmiati arrivano dal backend.
 */
@Component({
  selector: 'app-discount-dialog',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
<div class="fixed inset-0 bg-black/50 z-50 flex items-center justify-center p-4"
  role="dialog" aria-modal="true" aria-labelledby="discount-dialog-title" (keydown.escape)="closed.emit()">
  <div class="bg-white rounded-2xl shadow-2xl w-full max-w-md p-5">
    <h2 id="discount-dialog-title" class="text-lg font-bold text-gray-800">Sconti sulla riga</h2>
    <p class="text-sm text-gray-500 mb-3">{{ item.productName }} · {{ item.color }} {{ item.size }}</p>

    <div class="flex items-baseline gap-3 mb-4" data-testid="dialog-prices">
      <span class="text-sm text-gray-500">Prezzo</span>
      <ng-container *ngIf="line?.discountedUnitPrice !== undefined && (line?.discountPercent ?? 0) > 0; else listPriceOnly">
        <span class="text-red-600 line-through">{{ item.unitNetPrice | number:'1.2-8' }} €</span>
        <span class="font-bold text-green-700">{{ line!.discountedUnitPrice | number:'1.2-8' }} €</span>
      </ng-container>
      <ng-template #listPriceOnly>
        <span class="font-semibold text-gray-800">{{ item.unitNetPrice | number:'1.2-8' }} €</span>
      </ng-template>
    </div>

    <h3 class="text-xs font-semibold uppercase tracking-wide text-gray-500 mb-1">Sconti applicati</h3>
    <ol class="mb-4 space-y-1 text-sm" aria-label="Sconti applicati">
      <li *ngFor="let discount of listDiscounts; let n = index" data-testid="list-discount"
        class="flex justify-between gap-3">
        <span>{{ n + 1 }}) {{ discount.reason }} <span class="text-xs text-gray-400">(listino)</span></span>
        <span class="font-medium text-green-700 whitespace-nowrap">{{ describe(discount) }}</span>
      </li>
      <li *ngIf="item.cashDiscount" data-testid="cash-discount" class="flex justify-between gap-3">
        <span>{{ listDiscounts.length + 1 }}) {{ item.cashDiscount.reason }}
          <span class="text-xs text-gray-400">(cassa)</span></span>
        <span class="font-medium text-green-700 whitespace-nowrap">{{ describeCash(item.cashDiscount) }}</span>
      </li>
      <li *ngIf="listDiscounts.length === 0 && !item.cashDiscount" class="text-gray-400">Nessuno sconto applicato</li>
    </ol>

    <fieldset class="border rounded-lg p-3">
      <legend class="px-1 text-sm font-semibold text-gray-700">
        {{ item.cashDiscount ? 'Modifica sconto alla cassa' : 'Aggiungi sconto alla cassa' }}
      </legend>
      <div class="flex items-center gap-2 mb-2">
        <div role="group" aria-label="Tipo di sconto" class="inline-flex rounded-lg border overflow-hidden text-sm">
          <button type="button" (click)="setMode('percent')" [attr.aria-pressed]="mode === 'percent'"
            [class]="'px-3 py-1.5 ' + (mode === 'percent' ? 'bg-blue-600 text-white' : 'bg-white text-gray-700')">
            Percentuale %
          </button>
          <button type="button" (click)="setMode('amount')" [attr.aria-pressed]="mode === 'amount'"
            [class]="'px-3 py-1.5 border-l ' + (mode === 'amount' ? 'bg-blue-600 text-white' : 'bg-white text-gray-700')">
            Valore €
          </button>
        </div>
        <input type="number" min="0" [attr.max]="mode === 'percent' ? 100 : null" step="0.01"
          aria-label="Valore sconto" [(ngModel)]="value" (ngModelChange)="error = null"
          class="border rounded-lg px-2 py-1.5 w-28 text-right" [attr.placeholder]="mode === 'percent' ? '0 – 100' : '0,00'">
        <span class="text-gray-500">{{ mode === 'percent' ? '%' : '€' }}</span>
      </div>
      <input aria-label="Causale sconto" placeholder="Causale (obbligatoria)" maxlength="500"
        [(ngModel)]="reason" (ngModelChange)="error = null" class="border rounded-lg px-2 py-1.5 w-full">
      <p *ngIf="error" role="alert" class="mt-2 text-sm text-red-600">{{ error }}</p>
      <p *ngIf="mode === 'amount'" class="mt-2 text-xs text-gray-500">
        Il valore è uno sconto sul totale della riga, calcolato dopo gli sconti di listino.
      </p>
    </fieldset>

    <div class="mt-4 flex items-center justify-end gap-2">
      <button type="button" *ngIf="item.cashDiscount" (click)="applied.emit(null)"
        class="mr-auto px-3 py-2 text-sm text-red-600 hover:bg-red-50 rounded-lg">Rimuovi sconto cassa</button>
      <button type="button" (click)="closed.emit()"
        class="px-3 py-2 text-sm text-gray-600 hover:bg-gray-100 rounded-lg">Annulla</button>
      <button type="button" (click)="apply()"
        class="px-4 py-2 text-sm font-semibold text-white bg-blue-600 hover:bg-blue-700 rounded-lg">Applica</button>
    </div>
  </div>
</div>
`
})
export class DiscountDialogComponent implements OnInit {
  @Input({ required: true }) item!: CartItem;
  @Input() line: CassaPreviewLine | null = null;
  @Output() applied = new EventEmitter<CashDiscount | null>();
  @Output() closed = new EventEmitter<void>();

  mode: CashDiscountMode = 'percent';
  value: number | null = null;
  reason = '';
  error: string | null = null;

  ngOnInit(): void {
    if (this.item.cashDiscount) {
      this.mode = this.item.cashDiscount.mode;
      this.value = this.item.cashDiscount.value;
      this.reason = this.item.cashDiscount.reason;
    }
  }

  /** Sconti del listino della riga, con causale; dal backend quando disponibile. */
  get listDiscounts(): CassaPreviewDiscount[] {
    const fromServer = this.line?.discounts?.filter(discount => discount.source === 'PriceList');
    if (fromServer?.length) return fromServer;
    return (this.item.discountPercent ?? 0) > 0
      ? [{
        source: 'PriceList', reason: this.item.priceListDiscountReason ?? 'Sconto listino',
        percent: this.item.discountPercent as number, amount: null, savedAmount: 0
      }]
      : [];
  }

  setMode(mode: CashDiscountMode): void {
    this.mode = mode;
    this.error = null;
  }

  describe(discount: CassaPreviewDiscount): string {
    const saved = discount.savedAmount > 0 ? ` (−${discount.savedAmount.toFixed(2)} €)` : '';
    return discount.percent !== null
      ? `−${this.trim(discount.percent)}%${saved}`
      : `−${(discount.amount ?? 0).toFixed(2)} €`;
  }

  describeCash(discount: CashDiscount): string {
    const cash = this.line?.discounts?.find(item => item.source === 'Cashier');
    const saved = cash && cash.savedAmount > 0 && discount.mode === 'percent' ? ` (−${cash.savedAmount.toFixed(2)} €)` : '';
    return discount.mode === 'percent'
      ? `−${this.trim(discount.value)}%${saved}`
      : `−${discount.value.toFixed(2)} €`;
  }

  apply(): void {
    const discount: CashDiscount = { mode: this.mode, value: Number(this.value), reason: this.reason.trim() };
    this.error = this.value === null || this.value === undefined
      ? 'Inserire il valore dello sconto.'
      : cashDiscountError(discount);
    if (this.error === null) this.applied.emit(discount);
  }

  private trim(value: number): string {
    return String(Number(value.toFixed(4)));
  }
}
