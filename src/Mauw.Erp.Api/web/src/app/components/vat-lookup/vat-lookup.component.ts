import { Component, Input, Output, EventEmitter, OnInit, OnChanges, SimpleChanges } from '@angular/core';
import { CommonModule } from '@angular/common';
import { HttpClient, HttpParams } from '@angular/common/http';
import { environment } from '../../../environments/environment';
import { VatValidationResult } from '../../models/country.model';
import { CountryService } from '../../services/country.service';

@Component({
  selector: 'app-vat-lookup',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './vat-lookup.component.html'
})
export class VatLookupComponent implements OnInit, OnChanges {
  /** Codice ISO del paese selezionato (2 lettere). */
  @Input() countryCode = 'IT';
  /** Valore corrente della P.IVA / Tax ID. */
  @Input() vatNumber = '';
  /** Emesso dopo verifica con il risultato. */
  @Output() validated = new EventEmitter<VatValidationResult>();

  isEu = true;
  checking = false;
  result: VatValidationResult | null = null;

  private readonly base = `${environment.apiUrl}/v1/contacts/validate-vat`;

  constructor(private http: HttpClient, private countryService: CountryService) {}

  ngOnInit(): void { this.updateEuFlag(); }
  ngOnChanges(changes: SimpleChanges): void {
    if (changes['countryCode']) { this.updateEuFlag(); this.result = null; }
  }

  updateEuFlag(): void {
    this.isEu = this.countryService.isEuMember(this.countryCode);
  }

  verify(): void {
    if (!this.vatNumber?.trim()) return;
    this.checking = true;
    this.result   = null;

    const params = new HttpParams()
      .set('countryCode', this.countryCode)
      .set('vatNumber',   this.vatNumber.trim());

    this.http.get<VatValidationResult>(this.base, { params }).subscribe({
      next: r => {
        this.result   = r;
        this.checking = false;
        this.validated.emit(r);
      },
      error: () => {
        this.checking = false;
        this.result = { isFormatValid: false, isViesValid: null, isViesAvailable: false,
          companyName: null, companyAddress: null, errorMessage: 'Errore di rete.' };
      }
    });
  }

  get buttonLabel(): string {
    if (this.checking) return 'Verifica…';
    return this.isEu ? 'Verifica VIES' : 'Verifica Formato';
  }
}
