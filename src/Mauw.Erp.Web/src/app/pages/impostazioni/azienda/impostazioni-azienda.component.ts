import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormsModule, FormBuilder, FormGroup, Validators } from '@angular/forms';
import { CompanySettingsService } from '../../../services/company-settings.service';
import { CompanySettings, CompanyAddressSite, CompanyAddressSiteRequest } from '../../../models/company-settings.model';
import { ConfirmDeleteModalComponent } from '../../../shared/confirm-delete-modal/confirm-delete-modal.component';
import { CompanyBrandingService, CompanyLogoKind } from '../../../services/company-branding.service';

@Component({
  selector: 'app-impostazioni-azienda',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, FormsModule, ConfirmDeleteModalComponent],
  templateUrl: './impostazioni-azienda.component.html'
})
export class ImpostazioniAziendaComponent implements OnInit {
  form!: FormGroup;
  company: CompanySettings | null = null;
  loading = false;
  saving  = false;
  successMessage = '';
  errorMessage   = '';
  savingLogo = false;
  readonly suggestedInvoiceNotes = 'La merce viaggia con regolare imballaggio originale e identificativi ' +
    'barcode/SKU per showroom/negozio. Trascorsi 8 giorni dalla ricezione merce non si accettano resi o ' +
    'contestazioni su differenze taglia o capo non conforme.';

  // Operational sites state
  showSiteModal = false;
  editingSiteId: string | null = null;
  siteForm!: FormGroup;
  savingSite = false;

  constructor(
    private fb: FormBuilder,
    private service: CompanySettingsService,
    readonly branding: CompanyBrandingService
  ) {}

  async replaceLogo(kind: CompanyLogoKind, event: Event): Promise<void> {
    const input = event.target;
    if (!(input instanceof HTMLInputElement) || !input.files?.length) return;
    this.savingLogo = true;
    this.errorMessage = '';
    this.successMessage = '';
    try {
      await this.branding.replaceLogo(kind, input.files[0]);
      this.successMessage = 'Logo aggiornato per interfaccia e nuove stampe.';
    } catch (error: unknown) {
      const response = error as { error?: unknown };
      this.errorMessage = typeof response?.error === 'string' ? response.error
        : error instanceof Error ? error.message : 'Errore durante il salvataggio del logo.';
    } finally {
      this.savingLogo = false;
      input.value = '';
    }
  }

  ngOnInit(): void {
    this.buildForm();
    this.buildSiteForm();
    this.loadCompany();
  }

  buildForm(): void {
    this.form = this.fb.group({
      businessName:      ['', Validators.required],
      vatNumber:         [''],
      taxCode:           [''],
      sdiCode:           ['', Validators.maxLength(7)],
      pecEmail:          ['', Validators.email],
      countryCode:       ['IT'],
      fiscalRegime:      [''],
      reaNumber:         [''],
      reaOffice:         [''],
      shareCapital:      [null],
      isShareCapitalFullyPaid: [false],
      bankName:          ['', Validators.maxLength(250)],
      iban:              ['', Validators.maxLength(42)],
      bicSwift:          ['', Validators.maxLength(11)],
      invoiceNotes:      ['', Validators.maxLength(4000)],
      isSoleShareholder: [null],
      isInLiquidation:   [false],
      logoUrl:           [''],
      // Sede Legale
      hqStreet:   ['', Validators.required],
      hqZipCode:  ['', Validators.required],
      hqCity:     ['', Validators.required],
      hqProvince: [''],
      hqCountry:  ['IT'],
      hqPhone:    [''],
      hqEmail:    ['', Validators.email]
    });
  }

  buildSiteForm(site?: CompanyAddressSite): void {
    this.siteForm = this.fb.group({
      purpose:             [site?.purpose ?? 'OperationalSite'],
      siteName:            [site?.siteName ?? ''],
      street:              [site?.street   ?? '', Validators.required],
      zipCode:             [site?.zipCode  ?? '', Validators.required],
      city:                [site?.city     ?? '', Validators.required],
      province:            [site?.province ?? ''],
      countryCode:         [site?.countryCode ?? 'IT'],
      isMainOperationalSite: [site?.isMainOperationalSite ?? false],
      phone:               [site?.phone       ?? ''],
      email:               [site?.email       ?? '', Validators.email],
      accessNotes:         [site?.accessNotes ?? '']
    });
  }

  loadCompany(): void {
    this.loading = true;
    this.service.getCompany().subscribe({
      next: (c) => {
        this.company = c;
        this.patchFormFromCompany(c);
        this.loading = false;
      },
      error: () => { this.loading = false; } // 404 = not yet configured
    });
  }

  patchFormFromCompany(c: CompanySettings): void {
    this.form.patchValue({
      businessName:      c.businessName,
      vatNumber:         c.vatNumber,
      taxCode:           c.taxCode,
      sdiCode:           c.sdiCode,
      pecEmail:          c.pecEmail,
      countryCode:       c.countryCode,
      fiscalRegime:      c.fiscalRegime,
      reaNumber:         c.reaNumber,
      reaOffice:         c.reaOffice,
      shareCapital:      c.shareCapital,
      isShareCapitalFullyPaid: c.isShareCapitalFullyPaid,
      bankName:          c.bankName,
      iban:              c.iban,
      bicSwift:          c.bicSwift,
      invoiceNotes:      c.invoiceNotes,
      isSoleShareholder: c.isSoleShareholder,
      isInLiquidation:   c.isInLiquidation,
      logoUrl:           c.logoUrl,
      hqStreet:          c.legalHeadquarters?.street   ?? '',
      hqZipCode:         c.legalHeadquarters?.zipCode  ?? '',
      hqCity:            c.legalHeadquarters?.city     ?? '',
      hqProvince:        c.legalHeadquarters?.province ?? '',
      hqCountry:         c.legalHeadquarters?.countryCode ?? 'IT',
      hqPhone:           c.legalHeadquarters?.phone    ?? '',
      hqEmail:           c.legalHeadquarters?.email    ?? ''
    });
  }

  save(): void {
    if (this.form.invalid) { this.form.markAllAsTouched(); return; }
    this.saving = true;
    const v = this.form.value;

    const request = {
      businessName: v.businessName,
      vatNumber:    v.vatNumber  || null,
      taxCode:      v.taxCode    || null,
      sdiCode:      v.sdiCode    || null,
      pecEmail:     v.pecEmail   || null,
      countryCode:  v.countryCode || 'IT',
      fiscalRegime: v.fiscalRegime || null,
      reaNumber:    v.reaNumber  || null,
      reaOffice:    v.reaOffice  || null,
      shareCapital: v.shareCapital ?? null,
      isShareCapitalFullyPaid: v.isShareCapitalFullyPaid ?? false,
      bankName:     v.bankName || null,
      iban:         v.iban || null,
      bicSwift:     v.bicSwift || null,
      invoiceNotes: v.invoiceNotes || null,
      isSoleShareholder: v.isSoleShareholder ?? null,
      isInLiquidation:   v.isInLiquidation ?? false,
      logoUrl:      v.logoUrl    || null,
      legalHeadquarters: {
        street:    v.hqStreet,
        zipCode:   v.hqZipCode,
        city:      v.hqCity,
        province:  v.hqProvince || null,
        countryCode: v.hqCountry || 'IT',
        phone:     v.hqPhone    || null,
        email:     v.hqEmail    || null
      }
    };

    this.service.upsertCompany(request).subscribe({
      next: (c) => {
        this.company = c;
        this.patchFormFromCompany(c);
        this.successMessage = 'Dati aziendali salvati.';
        this.saving = false;
        setTimeout(() => (this.successMessage = ''), 3000);
      },
      error: (error: { error?: unknown }) => {
        this.errorMessage = typeof error.error === 'string' ? error.error : 'Errore durante il salvataggio.';
        this.saving = false;
      }
    });
  }

  // ── Sedi operative ────────────────────────────────────────────────────────

  openAddSite(): void {
    this.editingSiteId = null;
    this.buildSiteForm();
    this.showSiteModal = true;
  }

  openEditSite(site: CompanyAddressSite): void {
    this.editingSiteId = site.id;
    this.buildSiteForm(site);
    this.showSiteModal = true;
  }

  closeSiteModal(): void {
    this.showSiteModal = false;
    this.editingSiteId = null;
  }

  saveSite(): void {
    if (this.siteForm.invalid) { this.siteForm.markAllAsTouched(); return; }
    this.savingSite = true;
    const req: CompanyAddressSiteRequest = { ...this.siteForm.value };

    const obs = this.editingSiteId
      ? this.service.updateOperationalSite(this.editingSiteId, req)
      : this.service.addOperationalSite(req);

    obs.subscribe({
      next: () => {
        this.savingSite = false;
        this.showSiteModal = false;
        this.loadCompany();
      },
      error: () => {
        this.errorMessage = 'Errore durante il salvataggio della sede.';
        this.savingSite = false;
      }
    });
  }

  confirmDeleteId: string | null = null;

  askDelete(id: string): void { this.confirmDeleteId = id; }

  onDeleteConfirmed(): void {
    if (!this.confirmDeleteId) return;
    const id = this.confirmDeleteId;
    this.confirmDeleteId = null;
    this.deleteSite(id);
  }

  onDeleteCancelled(): void { this.confirmDeleteId = null; }

  deleteSite(siteId: string): void {
    this.service.deleteOperationalSite(siteId).subscribe({
      next: () => this.loadCompany(),
      error: (err) => (this.errorMessage = err?.error ?? 'Errore durante l\'eliminazione.')
    });
  }

  purposeLabel(p: string): string {
    const map: Record<string, string> = {
      OperationalSite: 'Sede Operativa',
      WarehouseSite:   'Magazzino',
      Administrative:  'Sede Amministrativa'
    };
    return map[p] ?? p;
  }
}
