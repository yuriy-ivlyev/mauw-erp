import '../../../locale';
import { provideHttpClient } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { of } from 'rxjs';
import { ContactDetail, ContactSummary, PagedResult } from '../../../models/contact.model';
import { CustomerStatementDto } from '../../../models/customer-statement.models';
import { ContactService } from '../../../services/contact.service';
import { CountryService } from '../../../services/country.service';
import { PriceListService } from '../../../services/price-list.service';
import { AnagraficaModificaComponent } from './anagrafica-modifica.component';

describe('AnagraficaModificaComponent statement tab', () => {
  let contactsApi: jasmine.SpyObj<ContactService>;
  let countriesApi: jasmine.SpyObj<CountryService>;
  let priceListsApi: jasmine.SpyObj<PriceListService>;
  let harness: RouterTestingHarness;

  const contact: ContactDetail = {
    id: 'customer-1',
    businessName: 'Cliente Test',
    tradeName: null,
    vatNumber: null,
    taxCode: null,
    sdiCode: null,
    pecEmail: null,
    countryCode: 'IT',
    fiscalRegime: null,
    status: 'Active',
    isDeleted: false,
    createdAt: '2026-01-01T00:00:00Z',
    updatedAt: '2026-01-01T00:00:00Z',
    customerProfile: {
      priceListId: null,
      paymentTermId: null,
      financialDiscountRate: 0,
      creditLimit: 0,
      isBlocked: false,
      blockReason: null,
      defaultAgentId: null,
      deliveryNotes: null
    },
    supplierProfile: null,
    agentProfile: null,
    addresses: [],
    persons: []
  };

  const statement: CustomerStatementDto = {
    customerId: contact.id,
    customerName: contact.businessName,
    totalInvoiced: 0,
    totalCredited: 0,
    totalPaid: 0,
    netBalance: 0,
    availableCreditNotesAmount: 0,
    movements: []
  };

  beforeEach(async () => {
    contactsApi = jasmine.createSpyObj<ContactService>('ContactService', [
      'getById',
      'getList',
      'getCustomerStatement'
    ]);
    contactsApi.getById.and.returnValue(of(contact));
    contactsApi.getList.and.returnValue(of({
      items: [],
      pageNumber: 1,
      pageSize: 100,
      totalCount: 0,
      totalPages: 0
    } satisfies PagedResult<ContactSummary>));
    contactsApi.getCustomerStatement.and.returnValue(of(statement));

    countriesApi = jasmine.createSpyObj<CountryService>(
      'CountryService', ['getCountries', 'preload', 'isEuMember']);
    countriesApi.getCountries.and.returnValue(of([]));
    countriesApi.isEuMember.and.returnValue(true);
    priceListsApi = jasmine.createSpyObj<PriceListService>('PriceListService', ['getAll']);
    priceListsApi.getAll.and.returnValue(of([]));

    await TestBed.configureTestingModule({
      imports: [AnagraficaModificaComponent],
      providers: [
        provideHttpClient(),
        provideRouter([
          { path: 'anagrafiche/:id/modifica', component: AnagraficaModificaComponent }
        ]),
        { provide: ContactService, useValue: contactsApi },
        { provide: CountryService, useValue: countriesApi },
        { provide: PriceListService, useValue: priceListsApi }
      ]
    }).compileComponents();
    harness = await RouterTestingHarness.create();
  });

  it('shows the read-only statement tab for a saved customer and hides save actions there', async () => {
    await harness.navigateByUrl(
      '/anagrafiche/customer-1/modifica',
      AnagraficaModificaComponent);
    await harness.fixture.whenStable();
    harness.detectChanges();

    const root = harness.routeNativeElement as HTMLElement;
    const statementTab = Array.from(root.querySelectorAll('button')).find(button =>
      button.textContent?.includes('Estratto Conto / Partitario'));
    expect(statementTab).toBeDefined();

    statementTab!.click();
    await harness.fixture.whenStable();
    harness.detectChanges();

    expect(contactsApi.getCustomerStatement).toHaveBeenCalledWith(contact.id);
    expect(root.textContent).toContain('Saldo netto attuale');
    const saveButtons = Array.from(root.querySelectorAll('button')).filter(button =>
      button.textContent?.includes('Salva') || button.textContent?.includes('Aggiorna'));
    expect(saveButtons.length).toBe(0);
  });
});
