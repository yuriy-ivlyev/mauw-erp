import { Injectable } from '@angular/core';
import { BehaviorSubject, EMPTY, Observable, Subject, of, timer, throwError } from 'rxjs';
import { catchError, map, switchMap } from 'rxjs/operators';
import { ApiService } from './api.service';
import { PriceListService, priceError } from './price-list.service';
import { ItemPricingRequest } from '../models/price-list.model';
import { ContactDetail } from '../models/contact.model';
import { guidPattern, hasDecimal8Precision } from './price-list.validators';

export interface CartItem {
  priceListId?: string | null;
  ruleId?: string | null;
  listUnitPrice?: number;
  agreedUnitNetPrice?: number | null;
  isPriceOverride?: boolean;
  overrideReason?: string | null;
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

export interface CassaPreviewLine {
  lineNumber: number;
  unitNetPrice: number;
  discountPercent: number;
  vatRate: number;
  natureCode: string | null;
  lineNetTotal: number;
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
        map(detail => ({ contact: { ...contact, priceListId: detail.customerProfile?.priceListId ?? null }, error: null })),
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
        const invalidReturn = this.state.cart.some(item => item.isCustomerReturn &&
          (!item.sourceLineId || item.isPriceOverride));
        this.pricingSubject.next({ pending: false, error: invalidReturn ? 'Il reso richiede la riga fattura originale, senza override del prezzo.' : null });
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
          ...(!item.isPriceOverride ? { unitNetPrice: resolved.listUnitPrice, discountPercent: resolved.discountPercent } : {}),
          vatRate: resolved.vatRate
        };
      }) };
      const invalidReturn = this.state.cart.some(item => item.isCustomerReturn &&
        (!item.sourceLineId || item.isPriceOverride));
      const invalid = invalidReturn || this.state.cart.some(item => item.isPriceOverride &&
        (!item.overrideReason?.trim() || !Number.isFinite(item.unitNetPrice) || item.unitNetPrice < 0 ||
          !hasDecimal8Precision(item.unitNetPrice) ||
          !Number.isFinite(item.discountPercent ?? 0) || (item.discountPercent ?? 0) < 0 || (item.discountPercent ?? 0) > 100));
      this.pricingSubject.next({ pending: false, error: invalidReturn
        ? 'Il reso richiede la riga fattura originale, senza override del prezzo.'
        : invalid ? 'Override: prezzo valido (massimo 8 decimali), sconto 0–100 e motivo obbligatorio.' : null });
      this.persist();
      if (!invalid) this.previewTrigger.next(this.cartRequestItems());
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
      isPriceOverride: i.pricingSnapshot?.isManualOverride ?? i.isPriceOverride ?? !!i.overrideReason,
      overrideReason: i.pricingSnapshot?.overrideReason ?? i.overrideReason,
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
      isManualPriceOverride: i.isPriceOverride ?? false,
      overrideReason: i.isPriceOverride ? i.overrideReason?.trim() : null,
      isCustomerReturn: i.isCustomerReturn ?? false,
      sourceLineId: i.sourceLineId ?? null,
      natureCode: i.natureCode,
      vatRate: i.vatRate,
      maxQuantity: i.maxQuantity
    }));
  }

  documentRequestItems() {
    return this.cartRequestItems().map(item => {
      const { unitNetPrice, discountPercent, isManualPriceOverride, ...base } = item;
      return {
        ...base,
        ...(isManualPriceOverride ? { unitNetPrice, discountPercent } : {})
      };
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
        this.state = JSON.parse(raw) as CassaSessionState;
        this.stateSubject.next({ ...this.state });
        this.refreshPreview();
      }
    } catch { /* ignore */ }
  }
}
