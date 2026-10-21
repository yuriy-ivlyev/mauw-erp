import { Injectable } from '@angular/core';
import { BehaviorSubject, EMPTY, Observable, Subject, of, timer, throwError } from 'rxjs';
import { catchError, map, switchMap } from 'rxjs/operators';
import { ApiService } from './api.service';
import { PriceListService, priceError } from './price-list.service';
import { ItemPricingRequest } from '../models/price-list.model';
import { ContactDetail } from '../models/contact.model';
import { guidPattern } from './price-list.validators';

export type CashDiscountMode = 'percent' | 'amount';

/** Sconto deciso alla cassa: percentuale oppure importo in euro sul totale di riga, sempre con causale. */
export interface CashDiscount {
  mode: CashDiscountMode;
  value: number;
  reason: string;
}

export interface CartItem {
  priceListId?: string | null;
  ruleId?: string | null;
  listUnitPrice?: number;
  agreedUnitNetPrice?: number | null;
  /** Sconto della cassa: si aggiunge a quello del listino e ha una propria causale. */
  cashDiscount?: CashDiscount | null;
  /** Causale dello sconto di listino, quando il listino ne applica uno. */
  priceListDiscountReason?: string | null;
  isCustomerReturn?: boolean;
  sourceLineId?: string | null;
  natureCode?: string | null;
  discountPercent?: number;
  barcode: string;
  variantId: string;
  productId: string;
  productName: string;
  size: string;
  color: string;
  unitNetPrice: number;
  vatRate: number;
  quantityAvailable: number;
  weightGrams?: number | null;
  quantity: number;
  maxQuantity: number;
}

/** Uno degli sconti di una riga, calcolato dal backend. */
export interface CassaPreviewDiscount {
  source: 'PriceList' | 'Cashier';
  reason: string;
  percent: number | null;
  amount: number | null;
  savedAmount: number;
}

export interface CassaPreviewLine {
  lineNumber: number;
  unitNetPrice: number;
  discountPercent: number;
  vatRate: number;
  natureCode: string | null;
  lineNetTotal: number;
  /** Prezzo unitario dopo tutti gli sconti, calcolato dal backend. */
  discountedUnitPrice?: number;
  discounts?: CassaPreviewDiscount[];
}

export interface CassaPreviewVat {
  vatRate: number;
  natureCode: string | null;
  taxableAmount: number;
  vatAmount: number;
}

/** Fiscal result computed by the backend calculator; the POS only displays it. */
export interface CassaPreview {
  lines: CassaPreviewLine[];
  vatSummary: CassaPreviewVat[];
  totalTaxable: number;
  totalVat: number;
  totalGross: number;
}

export interface CassaPreviewState {
  data: CassaPreview | null;
  error: string | null;
}

export interface CassaCartRequestItem extends ItemPricingRequest {
  unitNetPrice: number;
  discountPercent: number;
  isManualPriceOverride: boolean;
  variantId: string;
  quantity: number;
  natureCode?: string | null;
  vatRate: number;
  maxQuantity: number;
  cashDiscount?: { percent: number | null; amount: number | null; reason: string } | null;
  priceListDiscountReason?: string | null;
}

export interface CassaContact {
  id: string;
  businessName: string;
  vatNumber: string;
  defaultDiscountRate: number;
  priceListId?: string | null;
}

export interface CassaSessionState {
  priceListId?: string | null;
  sessionId: string | null;
  sessionCode: string | null;
  cart: CartItem[];
  selectedContact: CassaContact | null;
  documentNumber: string;
}

export interface PausedSessionSummary {
  id: string;
  sessionCode: string;
  contactName: string | null;
  cartItemCount: number;
  updatedAt: string;
}

export interface SavedSessionDetail {
  priceListId?: string | null;
  id: string;
  sessionCode: string;
  contactId: string | null;
  contactName: string | null;
  contactVatNumber: string | null;
  documentReference: string | null;
  items: SavedCartItemDto[];
}

export interface SavedCartItemDto {
  orderItemId?: string;
  pricingSnapshot?: {
    priceListId: string | null;
    ruleId: string | null;
    listUnitPrice: number;
    agreedUnitNetPrice: number | null;
    isManualOverride: boolean;
    overrideReason: string | null;
    isCustomerReturn: boolean;
    sourceLineId: string | null;
    discounts?: Array<{ source: string; reason: string; percent: number | null; amount: number | null }> | null;
  } | null;
  priceListId?: string | null;
  overrideReason?: string | null;
  isPriceOverride?: boolean;
  isCustomerReturn?: boolean;
  sourceLineId?: string | null;
  natureCode?: string | null;
  discountPercent?: number;
  variantId: string;
  barcode: string;
  productId: string;
  productName: string;
  size: string;
  color: string;
  unitNetPrice: number;
  vatRate: number;
  quantity: number;
  maxQuantity: number;
  weightGrams?: number | null;
}

const STORAGE_KEY = 'mauw_cassa_session';
export const CASH_DISCOUNT_ERROR =
  'Sconto di cassa: percentuale da 0 a 100 o importo in euro valido, con causale obbligatoria.';

/** Messaggio di validazione di uno sconto di cassa, oppure null se e valido. Il calcolo resta sul backend. */
export function cashDiscountError(discount: CashDiscount): string | null {
  const { mode, value, reason } = discount;
  if (mode === 'percent' && !(Number.isFinite(value) && value > 0 && value <= 100)) {
    return 'La percentuale deve essere maggiore di 0 e al massimo 100.';
  }
  if (mode === 'amount' && !(Number.isFinite(value) && value > 0 && Math.round(value * 100) / 100 === value)) {
    return 'L\'importo deve essere maggiore di 0, con al massimo due decimali.';
  }
  if (reason.trim().length === 0) return 'La causale dello sconto è obbligatoria.';
  if (reason.trim().length > 500) return 'La causale non può superare 500 caratteri.';
  return null;
}

export function isValidCashDiscount(item: CartItem): boolean {
  return !item.cashDiscount || cashDiscountError(item.cashDiscount) === null;
}
export const NOT_A_CUSTOMER_ERROR = 'L\'anagrafica selezionata non è un cliente: non è possibile vendere.';

/** Sconto di cassa di una riga salvata: dall'elenco sconti, oppure dal vecchio formato con un solo sconto manuale. */
function savedCashDiscount(item: SavedCartItemDto): CashDiscount | null {
  const snapshot = item.pricingSnapshot;
  if (!snapshot || snapshot.isCustomerReturn) return null;
  const cashier = snapshot.discounts?.find(discount => discount.source === 'Cashier');
  if (cashier) {
    return cashier.percent !== null
      ? { mode: 'percent', value: cashier.percent, reason: cashier.reason }
      : { mode: 'amount', value: cashier.amount ?? 0, reason: cashier.reason };
  }
  if (snapshot.isManualOverride && !snapshot.discounts && (item.discountPercent ?? 0) > 0) {
    return { mode: 'percent', value: item.discountPercent as number, reason: snapshot.overrideReason ?? '' };
  }
  return null;
}

export interface CassaEmissionItem {
  variantId: string;
  quantity: number;
  sourceOrderItemId: string;
  vatRate: number;
  natureCode?: string | null;
}

@Injectable({ providedIn: 'root' })
export class CassaSessionService {
  private state: CassaSessionState = {
    sessionId: null,
    sessionCode: null,
    cart: [],
    selectedContact: null,
    documentNumber: ''
  };

  private stateSubject = new BehaviorSubject<CassaSessionState>(this.state);
  state$: Observable<CassaSessionState> = this.stateSubject.asObservable();

  private previewTrigger = new Subject<CassaCartRequestItem[]>();
  private pricingTrigger = new Subject<void>();
  private contactTrigger = new Subject<CassaContact | null>();
  private contactLoading = false;
  private contactError: string | null = null;
  private pricingSubject = new BehaviorSubject<{ pending: boolean; error: string | null }>({ pending: false, error: null });
  pricing$ = this.pricingSubject.asObservable();
  get canSubmit(): boolean {
    return !this.contactLoading && !this.contactError && !this.pricingSubject.value.pending && !this.pricingSubject.value.error &&
      !!this.previewSubject.value.data && !this.previewSubject.value.error;
  }
  private previewSubject = new BehaviorSubject<CassaPreviewState>({ data: null, error: null });
  preview$: Observable<CassaPreviewState> = this.previewSubject.asObservable();

  constructor(private api: ApiService, private prices: PriceListService) {
    this.contactTrigger.pipe(switchMap(contact => contact
      ? this.api.get<ContactDetail>(`v1/contacts/${contact.id}`).pipe(
        map(detail => detail.customerProfile
          ? { contact: { ...contact, priceListId: detail.customerProfile.priceListId ?? null }, error: null }
          : { contact, error: NOT_A_CUSTOMER_ERROR }),
        catchError(error => of({ contact, error: priceError(error) })))
      : of({ contact: null, error: null })))
      .subscribe(result => {
        this.contactLoading = false;
        this.contactError = result.error;
        if (result.error) {
          this.pricingSubject.next({ pending: false, error: result.error });
          return;
        }
        this.state = { ...this.state, selectedContact: result.contact, priceListId: result.contact?.priceListId ?? null };
        this.persist();
        this.refreshPreview();
      });
    this.pricingTrigger.pipe(switchMap(() => this.contactLoading || this.contactError ? EMPTY : timer(180).pipe(switchMap(() => {
      const state = this.snapshot;
      const linesToQuote = state.cart.filter(item => !item.isCustomerReturn);
      if (!linesToQuote.length) return of(null);
      return this.prices.resolve({
        contactId: state.selectedContact?.id ?? null, priceListId: state.priceListId ?? null,
        at: new Date().toISOString(),
        lines: linesToQuote.map(item => ({ variantId: item.variantId, quantity: item.quantity }))
      }).pipe(
        map(result => ({ result, error: null })),
        catchError(error => of({ result: null, error: priceError(error) }))
      );
    })))).subscribe(response => {
      if (!response) {
        this.pricingSubject.next({ pending: false, error: null });
        const invalidReturn = this.state.cart.some(item => item.isCustomerReturn && !item.sourceLineId);
        this.pricingSubject.next({ pending: false, error: invalidReturn ? 'Il reso richiede la riga fattura originale.' : null });
        if (!invalidReturn) this.previewTrigger.next(this.cartRequestItems());
        return;
      }
      const resolvedByVariant = new Map(response.result?.lines.map(line => [line.variantId, line]));
      const quotedItems = this.state.cart.filter(item => !item.isCustomerReturn);
      if (!response.result || resolvedByVariant.size !== quotedItems.length ||
        quotedItems.some(item => !resolvedByVariant.has(item.variantId))) {
        this.pricingSubject.next({ pending: false, error: response.error ?? 'Risposta prezzi incompleta.' });
        return;
      }
      this.state = { ...this.state, cart: this.state.cart.map(item => {
        if (item.isCustomerReturn) return item;
        const resolved = resolvedByVariant.get(item.variantId)!;
        return {
          ...item, priceListId: resolved.priceListId, ruleId: resolved.ruleId,
          listUnitPrice: resolved.listUnitPrice, agreedUnitNetPrice: resolved.agreedUnitNetPrice,
          unitNetPrice: resolved.listUnitPrice, discountPercent: resolved.discountPercent,
          priceListDiscountReason: resolved.discountReason ?? null,
          vatRate: resolved.vatRate
        };
      }) };
      const invalidReturn = this.state.cart.some(item => item.isCustomerReturn && !item.sourceLineId);
      const invalidDiscount = this.state.cart.some(item => !item.isCustomerReturn && !isValidCashDiscount(item));
      this.pricingSubject.next({ pending: false, error: invalidReturn
        ? 'Il reso richiede la riga fattura originale.'
        : invalidDiscount ? CASH_DISCOUNT_ERROR : null });
      this.persist();
      if (!invalidReturn && !invalidDiscount) this.previewTrigger.next(this.cartRequestItems());
    });
    this.previewTrigger.pipe(
      switchMap(items => items.length === 0
        ? of<CassaPreviewState>({ data: null, error: null })
        : this.api.post<CassaPreview>('v1/cassa/sessions/preview', {
          items, contactId: this.state.selectedContact?.id ?? null, priceListId: this.state.priceListId ?? null
        }).pipe(
          map((data): CassaPreviewState => ({ data, error: null })),
          catchError(e => of<CassaPreviewState>({
            data: null,
            error: typeof e?.error === 'string' ? e.error : 'Calcolo del documento non disponibile'
          }))
        ))
    ).subscribe(state => this.previewSubject.next(state));
    this.restoreFromStorage();
  }

  get snapshot(): CassaSessionState {
    return { ...this.state, cart: [...this.state.cart] };
  }

  setCart(cart: CartItem[]): void {
    this.state = { ...this.state, cart };
    this.persist();
    this.refreshPreview();
  }

  setContact(contact: CassaContact | null): void {
    if (contact?.id === this.state.selectedContact?.id && !this.contactError) return;
    this.state = { ...this.state, selectedContact: contact };
    this.contactLoading = true;
    this.contactError = null;
    this.persist();
    this.pricingSubject.next({ pending: true, error: null });
    this.previewTrigger.next([]);
    this.pricingTrigger.next();
    this.contactTrigger.next(contact);
  }

  setPriceList(priceListId: string | null): void {
    this.state = { ...this.state, priceListId };
    this.persist();
    this.refreshPreview();
  }

  setDocumentNumber(num: string): void {
    this.state = { ...this.state, documentNumber: num };
    this.persist();
  }

  setSessionRef(id: string, code: string): void {
    this.state = { ...this.state, sessionId: id, sessionCode: code };
    this.persist();
  }

  clearSession(): void {
    this.state = { sessionId: null, sessionCode: null, cart: [], selectedContact: null, documentNumber: '' };
    localStorage.removeItem(STORAGE_KEY);
    this.stateSubject.next({ ...this.state });
    this.contactTrigger.next(null);
    this.refreshPreview();
  }

  createOrPauseSession() {
    if (!this.state.selectedContact || !this.canSubmit) {
      return throwError(() => ({ error: 'Cliente, prezzi e anteprima fiscale validi sono necessari per salvare la sessione.' }));
    }
    const items = this.cartRequestItems();
    const contactId = this.state.selectedContact!.id;

    if (this.state.sessionId) {
      const body = { contactId, priceListId: this.state.priceListId ?? null, documentReference: this.state.documentNumber || null, terminalId: null, items };
      return this.api.patch<SavedSessionDetail>(`v1/cassa/sessions/${this.state.sessionId}/pause`, body);
    }
    const body = { contactId, priceListId: this.state.priceListId ?? null, documentReference: this.state.documentNumber || null, terminalId: null, items };
    return this.api.post<SavedSessionDetail>('v1/cassa/sessions', body);
  }

  prepareEmission() {
    const expected = this.snapshot;
    return this.createOrPauseSession().pipe(map(saved => {
      this.setSessionRef(saved.id, saved.sessionCode);
      const savedVariants = new Map(saved.items.map(item => [item.variantId, item]));
      const ids = saved.items.map(item => item.orderItemId);
      if (!saved.contactId || saved.contactId !== expected.selectedContact?.id ||
        saved.items.length !== expected.cart.length || savedVariants.size !== expected.cart.length ||
        new Set(ids).size !== ids.length ||
        ids.some(id => !id || !guidPattern.test(id) || id === '00000000-0000-0000-0000-000000000000') ||
        expected.cart.some(item => savedVariants.get(item.variantId)?.quantity !== item.quantity)) {
        throw new Error('Sessione salvata incompleta: necessari orderItemId validi e righe coerenti. Nessun documento creato.');
      }
      const items: CassaEmissionItem[] = saved.items.map(item => ({
        variantId: item.variantId,
        quantity: item.quantity,
        sourceOrderItemId: item.orderItemId!,
        vatRate: item.vatRate,
        natureCode: item.natureCode
      }));
      return {
        contactId: saved.contactId,
        documentNumber: saved.documentReference ?? '',
        items
      };
    }));
  }

  resumeSession(sessionId: string) {
    return this.api.patch<SavedSessionDetail>(`v1/cassa/sessions/${sessionId}/resume`, { terminalId: null });
  }

  terminateSession(sessionId: string) {
    return this.api.patch<void>(`v1/cassa/sessions/${sessionId}/terminate`, {});
  }

  getPausedSessions() {
    return this.api.get<PausedSessionSummary[]>('v1/cassa/sessions/paused');
  }

  loadSessionDetail(sessionId: string) {
    return this.api.get<SavedSessionDetail>(`v1/cassa/sessions/${sessionId}`);
  }

  applyLoadedSession(detail: SavedSessionDetail): void {
    this.contactTrigger.next(null);
    const cart: CartItem[] = detail.items.map(i => ({
      barcode: i.barcode,
      variantId: i.variantId,
      productId: i.productId,
      productName: i.productName,
      size: i.size,
      color: i.color,
      unitNetPrice: i.unitNetPrice,
      priceListId: i.pricingSnapshot?.priceListId ?? i.priceListId,
      ruleId: i.pricingSnapshot?.ruleId,
      listUnitPrice: i.pricingSnapshot?.listUnitPrice,
      agreedUnitNetPrice: i.pricingSnapshot?.agreedUnitNetPrice,
      cashDiscount: savedCashDiscount(i),
      priceListDiscountReason: i.pricingSnapshot?.discounts?.find(discount => discount.source === 'PriceList')?.reason ?? null,
      isCustomerReturn: i.pricingSnapshot?.isCustomerReturn ?? i.isCustomerReturn,
      sourceLineId: i.pricingSnapshot?.sourceLineId ?? i.sourceLineId,
      natureCode: i.natureCode,
      discountPercent: i.discountPercent ?? 0,
      vatRate: i.vatRate,
      quantityAvailable: i.maxQuantity,
      weightGrams: i.weightGrams,
      quantity: i.quantity,
      maxQuantity: i.maxQuantity
    }));

    const contact = detail.contactId
      ? { id: detail.contactId, businessName: detail.contactName ?? '', vatNumber: detail.contactVatNumber ?? '', defaultDiscountRate: 0 }
      : null;

    this.state = {
      sessionId: detail.id,
      sessionCode: detail.sessionCode,
      cart,
      selectedContact: contact,
      documentNumber: detail.documentReference ?? '',
      priceListId: detail.priceListId ?? detail.items.find(item => item.pricingSnapshot?.priceListId)?.pricingSnapshot?.priceListId ?? null
    };
    this.persist();
    this.refreshPreview();
  }

  cartRequestItems(): CassaCartRequestItem[] {
    return this.state.cart.map(i => ({
      variantId: i.variantId,
      quantity: i.quantity,
      priceListId: i.isCustomerReturn ? null : this.state.priceListId ?? null,
      unitNetPrice: i.unitNetPrice,
      discountPercent: i.discountPercent ?? 0,
      isManualPriceOverride: false,
      overrideReason: null,
      cashDiscount: !i.isCustomerReturn && i.cashDiscount
        ? {
          percent: i.cashDiscount.mode === 'percent' ? i.cashDiscount.value : null,
          amount: i.cashDiscount.mode === 'amount' ? i.cashDiscount.value : null,
          reason: i.cashDiscount.reason.trim()
        }
        : null,
      priceListDiscountReason: i.priceListDiscountReason ?? null,
      isCustomerReturn: i.isCustomerReturn ?? false,
      sourceLineId: i.sourceLineId ?? null,
      natureCode: i.natureCode,
      vatRate: i.vatRate,
      maxQuantity: i.maxQuantity
    }));
  }

  /** In cassa il prezzo e sempre risolto dal server: la richiesta documento non porta prezzi o sconti. */
  documentRequestItems() {
    return this.cartRequestItems().map(item => {
      const { unitNetPrice, discountPercent, isManualPriceOverride, cashDiscount, priceListDiscountReason, ...base } = item;
      return base;
    });
  }

  private refreshPreview(): void {
    this.previewTrigger.next([]);
    this.pricingSubject.next({ pending: !this.contactError && this.state.cart.length > 0, error: this.contactError });
    this.pricingTrigger.next();
  }

  private persist(): void {
    localStorage.setItem(STORAGE_KEY, JSON.stringify(this.state));
    this.stateSubject.next({ ...this.state, cart: [...this.state.cart] });
  }

  private restoreFromStorage(): void {
    try {
      const raw = localStorage.getItem(STORAGE_KEY);
      if (raw) {
        const stored = JSON.parse(raw) as CassaSessionState;
        // Carrelli salvati con le versioni precedenti: il prezzo manuale sparisce e lo sconto passa al nuovo formato.
        this.state = { ...stored, cart: stored.cart.map(item => {
          const {
            isPriceOverride, overrideReason, manualDiscountPercent, discountReason, ...rest
          } = item as CartItem & {
            isPriceOverride?: boolean; overrideReason?: string | null;
            manualDiscountPercent?: number | null; discountReason?: string | null;
          };
          if (!rest.cashDiscount && manualDiscountPercent !== null && manualDiscountPercent !== undefined) {
            rest.cashDiscount = { mode: 'percent', value: manualDiscountPercent, reason: discountReason ?? '' };
          }
          return rest;
        }) };
        this.stateSubject.next({ ...this.state });
        this.refreshPreview();
      }
    } catch { /* ignore */ }
  }
}
