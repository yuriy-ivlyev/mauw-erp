import { Component, Input, Output, EventEmitter, OnDestroy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Subject } from 'rxjs';
import { debounceTime, distinctUntilChanged, switchMap, takeUntil, catchError } from 'rxjs/operators';
import { of } from 'rxjs';
import { environment } from '../../../environments/environment';

export interface ParsedAddress {
  street: string;
  zipCode: string;
  city: string;
  province: string | null;
  countryCode: string;
  formattedAddress: string;
  externalPlaceId: string | null;
  latitude: number;
  longitude: number;
}

interface AddressSuggestion {
  placeId: string;
  text: string;
  mainText: string;
  secondaryText: string;
  prePopulatedAddress: ParsedAddress | null;
}

@Component({
  selector: 'app-address-autocomplete',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './address-autocomplete.component.html'
})
export class AddressAutocompleteComponent implements OnDestroy {
  @Input() countryCode = 'IT';
  @Input() placeholder = 'Cerca indirizzo…';
  @Output() addressSelected = new EventEmitter<ParsedAddress>();

  query = '';
  suggestions: AddressSuggestion[] = [];
  loading = false;
  showDropdown = false;

  /** Session token Google Places – generato al primo focus/input, azzerato dopo selezione. */
  private sessionToken: string | null = null;

  private readonly base = `${environment.apiUrl}/v1/geo`;
  private input$ = new Subject<string>();
  private destroy$ = new Subject<void>();

  constructor(private http: HttpClient) {
    this.input$.pipe(
      debounceTime(300),
      distinctUntilChanged(),
      switchMap(q => {
        if (q.length < 3) { this.suggestions = []; this.showDropdown = false; return of([]); }
        this.loading = true;
        const params = new HttpParams()
          .set('input',        q)
          .set('sessionToken', this.sessionToken!)
          .set('countryCode',  this.countryCode);
        return this.http.get<AddressSuggestion[]>(`${this.base}/autocomplete`, { params })
          .pipe(catchError(() => of([])));
      }),
      takeUntil(this.destroy$)
    ).subscribe(res => {
      this.loading     = false;
      this.suggestions = res;
      this.showDropdown = res.length > 0;
    });
  }

  /** Genera il session token al primo input (inizio sessione Google Places). */
  onInput(): void {
    if (!this.sessionToken)
      this.sessionToken = crypto.randomUUID();
    this.input$.next(this.query);
  }

  /** Cancella la sessione se l'utente svuota il campo. */
  onClear(): void {
    if (!this.query) this.resetSession();
  }

  async select(suggestion: AddressSuggestion): Promise<void> {
    this.showDropdown = false;
    this.query        = suggestion.text;

    if (suggestion.prePopulatedAddress) {
      // Photon / OSM: dati già disponibili, nessuna chiamata extra
      this.addressSelected.emit(suggestion.prePopulatedAddress);
      this.resetSession();
      return;
    }

    // Google Places: chiama place-details con il session token corrente
    const params = new HttpParams()
      .set('placeId',      suggestion.placeId)
      .set('sessionToken', this.sessionToken!);

    this.loading = true;
    this.http.get<ParsedAddress>(`${this.base}/place-details`, { params })
      .pipe(catchError(() => of(null)))
      .subscribe(detail => {
        this.loading = false;
        if (detail) this.addressSelected.emit(detail);
        this.resetSession(); // fine sessione: Google la fattura adesso
      });
  }

  close(): void { setTimeout(() => (this.showDropdown = false), 150); }

  private resetSession(): void { this.sessionToken = null; }

  ngOnDestroy(): void { this.destroy$.next(); this.destroy$.complete(); }
}
