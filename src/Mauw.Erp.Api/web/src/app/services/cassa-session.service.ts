import { Injectable } from '@angular/core';
import { BehaviorSubject, Observable } from 'rxjs';
import { ApiService } from './api.service';

export interface CartItem {
  barcode: string;
  variantId: string;
  productId: string;
  productName: string;
  size: string;
  color: string;
  unitNetPrice: number;
  unitGrossPrice: number;
  vatRate: number;
  quantityAvailable: number;
  weightGrams?: number | null;
  quantity: number;
  lineNetTotal: number;
  lineGrossTotal: number;
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
  cartJson: string;
  contactId: string | null;
  contactName: string | null;
  documentNumber: string | null;
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

  constructor(private api: ApiService) {
    this.restoreFromStorage();
  }

  get snapshot(): CassaSessionState {
    return { ...this.state, cart: [...this.state.cart] };
  }

  setCart(cart: CartItem[]): void {
    this.state = { ...this.state, cart };
    this.persist();
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
  }

  createOrPauseSession(terminalId?: string) {
    const body = {
      cartJson: JSON.stringify(this.state.cart),
      contactId: this.state.selectedContact?.id ?? null,
      contactName: this.state.selectedContact?.businessName ?? null,
      documentNumber: this.state.documentNumber || null,
      terminalId: terminalId ?? null
    };
    if (this.state.sessionId) {
      return this.api.patch<SavedSessionDetail>(`v1/cassa/sessions/${this.state.sessionId}/pause`, body);
    }
    return this.api.post<SavedSessionDetail>('v1/cassa/sessions', body);
  }

  resumeSession(sessionId: string, terminalId?: string) {
    return this.api.patch<SavedSessionDetail>(`v1/cassa/sessions/${sessionId}/resume`, { terminalId: terminalId ?? null });
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
    let cart: CartItem[] = [];
    try { cart = JSON.parse(detail.cartJson) as CartItem[]; } catch { }

    const contact = detail.contactId
      ? { id: detail.contactId, businessName: detail.contactName ?? '', vatNumber: '', defaultDiscountRate: 0 }
      : null;

    this.state = {
      sessionId: detail.id,
      sessionCode: detail.sessionCode,
      cart,
      selectedContact: contact,
      documentNumber: detail.documentNumber ?? ''
    };
    this.persist();
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
      }
    } catch { /* ignore */ }
  }
}
