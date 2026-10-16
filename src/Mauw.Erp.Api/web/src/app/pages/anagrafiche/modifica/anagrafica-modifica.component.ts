import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormsModule, FormBuilder, FormGroup, FormArray, Validators } from '@angular/forms';
import { Router, ActivatedRoute } from '@angular/router';
import { ContactService } from '../../../services/contact.service';
import { ContactDetail } from '../../../models/contact.model';

type Tab = 'fiscal' | 'addresses' | 'persons' | 'customer' | 'supplier' | 'agent';

@Component({
  selector: 'app-anagrafica-modifica',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, FormsModule],
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

  // Tab-visibility toggles for role tabs
  hasCustomer = false;
  hasSupplier = false;
  hasAgent    = false;

  constructor(
    private fb: FormBuilder,
    private contactService: ContactService,
    private router: Router,
    private route: ActivatedRoute
  ) {}

  ngOnInit(): void {
    this.buildForm();
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

  // ── Addresses ─────────────────────────────────────────────────────────────

  buildAddressGroup(data?: any): FormGroup {
    return this.fb.group({
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
      accessNotes:          [data?.accessNotes ?? '']
    });
  }

  addAddress(): void { this.addresses.push(this.buildAddressGroup()); }
  removeAddress(i: number): void { this.addresses.removeAt(i); }

  // ── Persons ───────────────────────────────────────────────────────────────

  buildPersonGroup(data?: any): FormGroup {
    return this.fb.group({
      firstName: [data?.firstName ?? '', Validators.required],
      lastName:  [data?.lastName  ?? '', Validators.required],
      role:      [data?.role      ?? ''],
      email:     [data?.email     ?? '', Validators.email],
      phone:     [data?.phone     ?? ''],
      isPrimary: [data?.isPrimary ?? false]
    });
  }

  addPerson(): void { this.persons.push(this.buildPersonGroup()); }
  removePerson(i: number): void { this.persons.removeAt(i); }

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
