import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormsModule, FormBuilder, FormGroup, FormArray, Validators } from '@angular/forms';
import { Router, ActivatedRoute } from '@angular/router';
import { ContactService } from '../../../services/contact.service';
import { CountryService } from '../../../services/country.service';
import { ContactDetail } from '../../../models/contact.model';
import { PriceList } from '../../../models/price-list.model';
import { PriceListService, priceError } from '../../../services/price-list.service';
import { CountryDto, VatValidationResult } from '../../../models/country.model';
import { VatLookupComponent } from '../../../components/vat-lookup/vat-lookup.component';
import { AddressAutocompleteComponent, ParsedAddress } from '../../../components/address-autocomplete/address-autocomplete.component';
import { ConfirmDeleteModalComponent } from '../../../shared/confirm-delete-modal/confirm-delete-modal.component';

type Tab = 'fiscal' | 'addresses' | 'persons' | 'customer' | 'supplier' | 'agent';

@Component({
  selector: 'app-anagrafica-modifica',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, FormsModule, VatLookupComponent, AddressAutocompleteComponent, ConfirmDeleteModalComponent],
  templateUrl: './anagrafica-modifica.component.html'
})
export class AnagraficaModificaComponent implements OnInit {
  form!: FormGroup;
  activeTab: Tab = 'fiscal';
  loading = false;
  saving = false;
  editId: string | null = null;
  successMessage = '';
  errorMessage = '';
  vatResult: VatValidationResult | null = null;

  // Tab-visibility toggles for role tabs
  hasCustomer = false;
  hasSupplier = false;
  hasAgent    = false;

  countries: CountryDto[] = [];
  priceLists: PriceList[] = [];
  priceListsError = '';
  get selectedCountry(): string { return this.form?.get('countryCode')?.value ?? 'IT'; }
  get selectedVatNumber(): string { return this.form?.get('vatNumber')?.value ?? ''; }
  get isExtraEu(): boolean {
    return this.selectedCountry !== '' &&
      !this.countries.find(c => c.isoCode === this.selectedCountry)?.isEuMember;
  }

  constructor(
    private fb: FormBuilder,
    private contactService: ContactService,
    private countryService: CountryService,
    private priceListService: PriceListService,
    private router: Router,
    private route: ActivatedRoute
  ) {}

  ngOnInit(): void {
    this.buildForm();
    this.priceListService.getAll().subscribe({
      next: lists => this.priceLists = lists,
      error: e => this.priceListsError = priceError(e)
    });
    this.countryService.getCountries().subscribe(c => {
      this.countries = c;
      this.countryService.preload(); // popola cache per VatLookup
    });
    this.editId = this.route.snapshot.paramMap.get('id');
    if (this.editId) this.loadContact(this.editId);
  }

  buildForm(): void {
    this.form = this.fb.group({
      // Tab 1: Dati fiscali
      businessName: ['', [Validators.required, Validators.maxLength(255)]],
      vatNumber:    ['', Validators.maxLength(20)],
      taxCode:      ['', Validators.maxLength(16)],
      sdiCode:      ['', Validators.maxLength(7)],
      pecEmail:     ['', [Validators.maxLength(255), Validators.email]],
      countryCode:  ['IT', Validators.maxLength(3)],
      fiscalRegime: [''],
      status:       ['Draft'],

      // Tab 3 – Cliente
      customer: this.fb.group({
        paymentTermId:         [null],
        priceListId:           [null],
        financialDiscountRate: [0, [Validators.min(0), Validators.max(100)]],
        creditLimit:           [0, Validators.min(0)],
        defaultAgentId:        [null],
        deliveryNotes:         [''],
        isBlocked:             [false],
        blockReason:           ['']
      }),

      // Tab 4 – Fornitore
      supplier: this.fb.group({
        paymentTermId: [null],
        iban:          [''],
        bicSwift:      ['']
      }),

      // Tab 5 – Agente
      agent: this.fb.group({
        defaultCommissionRate: [0, [Validators.min(0), Validators.max(100)]],
        operatingArea:         ['']
      }),

      addresses: this.fb.array([]),
      persons:   this.fb.array([])
    });
  }

  get addresses(): FormArray { return this.form.get('addresses') as FormArray; }
  get persons():   FormArray { return this.form.get('persons')   as FormArray; }

  loadContact(id: string): void {
    this.loading = true;
    this.contactService.getById(id).subscribe({
      next: (c: ContactDetail) => {
        this.form.patchValue({
          businessName: c.businessName,
          vatNumber:    c.vatNumber,
          taxCode:      c.taxCode,
          sdiCode:      c.sdiCode,
          pecEmail:     c.pecEmail,
          countryCode:  c.countryCode,
          fiscalRegime: c.fiscalRegime,
          status:       c.status
        });

        if (c.customerProfile) {
          this.hasCustomer = true;
          this.form.get('customer')!.patchValue(c.customerProfile);
        }
        if (c.supplierProfile) {
          this.hasSupplier = true;
          this.form.get('supplier')!.patchValue(c.supplierProfile);
        }
        if (c.agentProfile) {
          this.hasAgent = true;
          this.form.get('agent')!.patchValue(c.agentProfile);
        }

        c.addresses.forEach(a => this.addresses.push(this.buildAddressGroup(a)));
        c.persons.forEach(p   => this.persons.push(this.buildPersonGroup(p)));

        this.loading = false;
      },
      error: () => {
        this.errorMessage = 'Errore nel caricamento dell\'anagrafica.';
        this.loading = false;
      }
    });
  }

  // ── Confirm delete modal ──────────────────────────────────────────────────
  confirmDeleteType: 'address' | 'person' | null = null;
  confirmDeleteIdx: number | null = null;

  askRemoveAddress(i: number): void {
    this.confirmDeleteType = 'address';
    this.confirmDeleteIdx  = i;
  }

  askRemovePerson(i: number): void {
    this.confirmDeleteType = 'person';
    this.confirmDeleteIdx  = i;
  }

  onDeleteConfirmed(): void {
    const type = this.confirmDeleteType;
    const idx  = this.confirmDeleteIdx;
    this.confirmDeleteType = null;
    this.confirmDeleteIdx  = null;

    if (idx === null) return;

    if (type === 'address') this.doRemoveAddress(idx);
    if (type === 'person')  this.doRemovePerson(idx);
  }

  onDeleteCancelled(): void {
    this.confirmDeleteType = null;
    this.confirmDeleteIdx  = null;
  }

  // ── Addresses ─────────────────────────────────────────────────────────────

  buildAddressGroup(data?: any): FormGroup {
    return this.fb.group({
      // id tracks the server-side ID (empty string = not yet saved to DB)
      id:                   [data?.id ?? ''],
      purpose:              [data?.purpose ?? 'LegalHeadquarters'],
      siteName:             [data?.siteName ?? ''],
      street:               [data?.street  ?? '', Validators.required],
      zipCode:              [data?.zipCode  ?? '', Validators.required],
      city:                 [data?.city     ?? '', Validators.required],
      province:             [data?.province ?? ''],
      countryCode:          [data?.countryCode ?? 'IT'],
      isMainOperationalSite:[data?.isMainOperationalSite ?? false],
      phone:                [data?.phone  ?? ''],
      email:                [data?.email  ?? '', Validators.email],
      accessNotes:          [data?.accessNotes ?? ''],
      latitude:             [data?.latitude    ?? null],
      longitude:            [data?.longitude   ?? null],
      formattedAddress:     [data?.formattedAddress ?? ''],
      externalPlaceId:      [data?.externalPlaceId  ?? null]
    });
  }

  addAddress(): void { this.addresses.push(this.buildAddressGroup()); }

  /** Rimuove un indirizzo: se già salvato in DB chiama l'API di delete; se nuovo rimuove solo dal FormArray. */
  private doRemoveAddress(i: number): void {
    const ctrl = this.addresses.at(i);
    const addressId: string = ctrl.get('id')?.value ?? '';

    if (addressId && this.editId) {
      // Già in DB → chiamata API immediata
      this.contactService.deleteAddress(this.editId, addressId).subscribe({
        next: ()  => { this.addresses.removeAt(i); },
        error: (e) => { this.errorMessage = e?.error ?? 'Errore durante l\'eliminazione dell\'indirizzo.'; }
      });
    } else {
      // Non ancora salvato → rimuove solo dalla UI
      this.addresses.removeAt(i);
    }
  }

  /** Popola automaticamente i campi dell'indirizzo dalla selezione Photon / Google. */
  onAddressSelected(i: number, parsed: ParsedAddress): void {
    this.addresses.at(i).patchValue({
      street:          parsed.street,
      zipCode:         parsed.zipCode,
      city:            parsed.city,
      province:        parsed.province,
      countryCode:     parsed.countryCode,
      latitude:        parsed.latitude,
      longitude:       parsed.longitude,
      formattedAddress:parsed.formattedAddress,
      externalPlaceId: parsed.externalPlaceId
    });
  }

  // ── Persons ───────────────────────────────────────────────────────────────

  buildPersonGroup(data?: any): FormGroup {
    return this.fb.group({
      id:        [data?.id ?? ''],
      firstName: [data?.firstName ?? '', Validators.required],
      lastName:  [data?.lastName  ?? '', Validators.required],
      role:      [data?.role      ?? ''],
      email:     [data?.email     ?? '', Validators.email],
      phone:     [data?.phone     ?? ''],
      isPrimary: [data?.isPrimary ?? false]
    });
  }

  addPerson(): void { this.persons.push(this.buildPersonGroup()); }

  /** Rimuove un referente: se già in DB chiama l'API; altrimenti solo UI. */
  private doRemovePerson(i: number): void {
    const ctrl     = this.persons.at(i);
    const personId: string = ctrl.get('id')?.value ?? '';

    if (personId && this.editId) {
      this.contactService.deletePerson(this.editId, personId).subscribe({
        next: ()  => { this.persons.removeAt(i); },
        error: (e) => { this.errorMessage = e?.error ?? 'Errore durante l\'eliminazione del referente.'; }
      });
    } else {
      this.persons.removeAt(i);
    }
  }

  /** Gestisce il risultato della verifica P.IVA VIES. */
  onVatValidated(result: VatValidationResult): void {
    this.vatResult = result;
    if (result.companyName && !this.form.get('businessName')?.value)
      this.form.patchValue({ businessName: result.companyName });
    if (this.isExtraEu) this.form.patchValue({ sdiCode: 'XXXXXXX' });
  }

  onCountryChange(): void {
    if (this.isExtraEu) this.form.patchValue({ sdiCode: 'XXXXXXX' });
    this.vatResult = null;
  }

  // ── Save ──────────────────────────────────────────────────────────────────

  save(): void {
    if (this.form.invalid) { this.form.markAllAsTouched(); return; }

    this.saving = true;
    const v = this.form.value;

    const payload: any = {
      businessName: v.businessName,
      vatNumber:    v.vatNumber    || null,
      taxCode:      v.taxCode      || null,
      sdiCode:      v.sdiCode      || null,
      pecEmail:     v.pecEmail     || null,
      countryCode:  v.countryCode  || 'IT',
      fiscalRegime: v.fiscalRegime || null,
      status:       v.status,
      customerProfile: this.hasCustomer ? v.customer : null,
      supplierProfile: this.hasSupplier ? v.supplier : null,
      agentProfile:    this.hasAgent    ? v.agent    : null,
      addresses: v.addresses,
      persons:   v.persons,
      disableCustomer: !this.hasCustomer && !!this.editId,
      disableSupplier: !this.hasSupplier && !!this.editId,
      disableAgent:    !this.hasAgent    && !!this.editId
    };

    const obs = this.editId
      ? this.contactService.update(this.editId, payload)
      : this.contactService.create(payload);

    obs.subscribe({
      next: () => {
        this.successMessage = this.editId ? 'Anagrafica aggiornata.' : 'Anagrafica creata.';
        this.saving = false;
        setTimeout(() => this.router.navigate(['/anagrafiche/elenco']), 1200);
      },
      error: (err) => {
        this.errorMessage = err?.error ?? 'Errore durante il salvataggio.';
        this.saving = false;
      }
    });
  }

  cancel(): void { this.router.navigate(['/anagrafiche/elenco']); }

  setTab(t: string): void { this.activeTab = t as Tab; }
}
