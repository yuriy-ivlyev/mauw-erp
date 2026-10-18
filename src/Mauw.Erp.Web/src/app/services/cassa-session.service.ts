import { Injectable } from '@angular/core';
import { BehaviorSubject, Observable, Subject, of } from 'rxjs';
import { catchError, map, switchMap } from 'rxjs/operators';
import { ApiService } from './api.service';

export interface CartItem {
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

export interface CassaCartRequestItem {
  variantId: string;
  quantity: number;
  unitNetPrice: number;
  natureCode?: string | null;
  discountPercent: number;
  vatRate: number;
  maxQuantity: number;
}

export interface CassaContact {
  id: string;
  businessName: string;
  vatNumber: string;
  defaultDiscountRate: number;
}

export interface CassaSessionState {
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
  id: string;
  sessionCode: string;
  contactId: string | null;
  contactName: string | null;
  contactVatNumber: string | null;
  documentReference: string | null;
  items: SavedCartItemDto[];
}

export interface SavedCartItemDto {
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
  private previewSubject = new BehaviorSubject<CassaPreviewState>({ data: null, error: null });
  preview$: Observable<CassaPreviewState> = this.previewSubject.asObservable();

  constructor(private api: ApiService) {
    this.previewTrigger.pipe(
      switchMap(items => items.length === 0
        ? of<CassaPreviewState>({ data: null, error: null })
        : this.api.post<CassaPreview>('v1/cassa/sessions/preview', { items }).pipe(
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
    this.state = { ...this.state, selectedContact: contact };
    this.persist();
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
    this.refreshPreview();
  }

  createOrPauseSession() {
    const items = this.cartRequestItems();
    const contactId = this.state.selectedContact!.id;

    if (this.state.sessionId) {
      const body = { contactId, documentReference: this.state.documentNumber || null, terminalId: null, items };
      return this.api.patch<SavedSessionDetail>(`v1/cassa/sessions/${this.state.sessionId}/pause`, body);
    }
    const body = { contactId, documentReference: this.state.documentNumber || null, terminalId: null, items };
    return this.api.post<SavedSessionDetail>('v1/cassa/sessions', body);
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
    const cart: CartItem[] = detail.items.map(i => ({
      barcode: i.barcode,
      variantId: i.variantId,
      productId: i.productId,
      productName: i.productName,
      size: i.size,
      color: i.color,
      unitNetPrice: i.unitNetPrice,
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
      documentNumber: detail.documentReference ?? ''
    };
    this.persist();
    this.refreshPreview();
  }

  private cartRequestItems(): CassaCartRequestItem[] {
    return this.state.cart.map(i => ({
      variantId: i.variantId,
      quantity: i.quantity,
      unitNetPrice: i.unitNetPrice,
      natureCode: i.natureCode,
      discountPercent: i.discountPercent ?? 0,
      vatRate: i.vatRate,
      maxQuantity: i.maxQuantity
    }));
  }

  private refreshPreview(): void {
    this.previewTrigger.next(this.cartRequestItems());
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
