import { TestBed } from '@angular/core/testing';
import { of, throwError } from 'rxjs';
import { CompanySettings } from '../../../models/company-settings.model';
import { CompanySettingsService } from '../../../services/company-settings.service';
import { CompanyBrandingService } from '../../../services/company-branding.service';
import { ImpostazioniAziendaComponent } from './impostazioni-azienda.component';

describe('ImpostazioniAziendaComponent invoice settings', () => {
  let service: jasmine.SpyObj<CompanySettingsService>;
  const company: CompanySettings = {
    id: 'company', businessName: 'Mauw', vatNumber: null, taxCode: null,
    sdiCode: null, pecEmail: null, countryCode: 'IT', fiscalRegime: null,
    reaNumber: null, reaOffice: null, shareCapital: 10000, isShareCapitalFullyPaid: true,
    bankName: 'Banca Prova', iban: 'IT60X0542811101000000123456', bicSwift: 'BPITITMM',
    invoiceNotes: 'Note personalizzate', isSoleShareholder: null, isInLiquidation: false,
    logoUrl: null, legalHeadquarters: {
      id: 'hq', purpose: 'LegalHeadquarters', siteName: null, street: 'Via Roma 1',
      zipCode: '00100', city: 'Roma', province: 'RM', countryCode: 'IT',
      isMainOperationalSite: false, phone: null, email: null, accessNotes: null, linkedWarehouseId: null
    }, operationalSites: []
  };

  beforeEach(async () => {
    service = jasmine.createSpyObj<CompanySettingsService>('CompanySettingsService', ['getCompany', 'upsertCompany']);
    service.getCompany.and.returnValue(of(company));
    service.upsertCompany.and.returnValue(of(company));
    await TestBed.configureTestingModule({
      imports: [ImpostazioniAziendaComponent],
      providers: [
        { provide: CompanySettingsService, useValue: service },
        { provide: CompanyBrandingService, useValue: { logoUrl: () => '' } }
      ]
    }).compileComponents();
  });

  it('loads, edits and saves the paid capital, banking details and invoice notes', () => {
    const fixture = TestBed.createComponent(ImpostazioniAziendaComponent);
    fixture.detectChanges();
    const component = fixture.componentInstance;
    expect(component.form.value.isShareCapitalFullyPaid).toBeTrue();
    expect(component.form.value.iban).toBe(company.iban);
    component.form.patchValue({ isShareCapitalFullyPaid: false, invoiceNotes: 'Nuove note' });
    component.save();
    expect(service.upsertCompany).toHaveBeenCalledWith(jasmine.objectContaining({
      isShareCapitalFullyPaid: false, bankName: company.bankName, iban: company.iban,
      bicSwift: company.bicSwift, invoiceNotes: 'Nuove note'
    }));
  });

  it('offers the proposed transport text without asserting XML transmission or replacing existing notes', () => {
    const fixture = TestBed.createComponent(ImpostazioniAziendaComponent);
    fixture.detectChanges();
    const element: HTMLElement = fixture.nativeElement;
    const button = Array.from(element.querySelectorAll('button'))
      .find(item => item.textContent?.includes('Inserisci le condizioni'))!;
    expect(button.disabled).toBeTrue();
    fixture.componentInstance.form.patchValue({ invoiceNotes: '' });
    fixture.detectChanges();
    button.click();
    expect(fixture.componentInstance.form.value.invoiceNotes).toContain('Trascorsi 8 giorni');
    expect(fixture.componentInstance.form.value.invoiceNotes).not.toContain('trasmesso');
    expect(service.upsertCompany).not.toHaveBeenCalled();
  });

  it('shows backend validation errors without reporting a successful save', () => {
    service.upsertCompany.and.returnValue(throwError(() => ({ error: 'IBAN non valido.' })));
    const fixture = TestBed.createComponent(ImpostazioniAziendaComponent);
    fixture.detectChanges();
    fixture.componentInstance.save();
    expect(fixture.componentInstance.errorMessage).toBe('IBAN non valido.');
    expect(fixture.componentInstance.successMessage).toBe('');
    expect(fixture.componentInstance.saving).toBeFalse();
  });

  it('rejects an IBAN with an invalid checksum before sending the save request', () => {
    const fixture = TestBed.createComponent(ImpostazioniAziendaComponent);
    fixture.detectChanges();
    const component = fixture.componentInstance;
    component.form.patchValue({ iban: 'IT60X0542811101000000123457' });

    component.save();

    expect(component.form.get('iban')?.hasError('ibanInvalid')).toBeTrue();
    expect(component.form.get('iban')?.touched).toBeTrue();
    expect(service.upsertCompany).not.toHaveBeenCalled();
  });
});
