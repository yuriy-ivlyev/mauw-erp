import '../../../locale';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { of } from 'rxjs';
import { ContactDetail } from '../../../models/contact.model';
import { CustomerStatementDto } from '../../../models/customer-statement.models';
import { ContactService } from '../../../services/contact.service';
import { AnagraficaDettaglioComponent } from './anagrafica-dettaglio.component';

describe('AnagraficaDettaglioComponent', () => {
  let contactsApi: jasmine.SpyObj<ContactService>;
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
    totalInvoiced: 500,
    totalCredited: 0,
    totalPaid: 500,
    netBalance: 0,
    availableCreditNotesAmount: 0,
    movements: [
      {
        dateUtc: '2026-01-01T09:00:00Z',
        type: 'Invoice',
        documentId: 'invoice-1',
        documentNumber: 'FAT-202601-00001',
        description: 'Fattura di vendita FAT-202601-00001',
        debit: 500,
        credit: 0,
        runningBalance: 500,
        paymentMethod: null,
        sourceCreditNoteNumber: null,
        sdiStatus: 'Accepted'
      },
      {
        dateUtc: '2026-01-02T09:00:00Z',
        type: 'CreditNote',
        documentId: 'note-1',
        documentNumber: 'NC-202601-00001',
        description: 'Nota di credito NC-202601-00001',
        debit: 0,
        credit: 0,
        runningBalance: 500,
        paymentMethod: null,
        sourceCreditNoteNumber: null,
        sdiStatus: 'Accepted'
      },
      {
        dateUtc: '2026-01-03T09:00:00Z',
        type: 'Payment',
        documentId: 'invoice-1',
        documentNumber: 'FAT-202601-00001',
        description: 'Compensazione da NC NC-202601-00001',
        debit: 0,
        credit: 500,
        runningBalance: 0,
        paymentMethod: 'CreditNoteOffset',
        sourceCreditNoteNumber: 'NC-202601-00001',
        sdiStatus: null,
        sourceCreditNoteId: 'note-1'
      }
    ]
  };

  beforeEach(async () => {
    contactsApi = jasmine.createSpyObj<ContactService>('ContactService', [
      'getById',
      'getCustomerStatement'
    ]);
    contactsApi.getById.and.returnValue(of(contact));
    contactsApi.getCustomerStatement.and.returnValue(of(statement));

    await TestBed.configureTestingModule({
      imports: [AnagraficaDettaglioComponent],
      providers: [
        provideRouter([{ path: 'anagrafiche/:id', component: AnagraficaDettaglioComponent }]),
        { provide: ContactService, useValue: contactsApi }
      ]
    }).compileComponents();
    harness = await RouterTestingHarness.create();
  });

  it('loads the customer statement and links the offset to its source credit note', async () => {
    await harness.navigateByUrl('/anagrafiche/customer-1', AnagraficaDettaglioComponent);
    harness.detectChanges();

    expect(contactsApi.getById).toHaveBeenCalledWith(contact.id);
    expect(contactsApi.getCustomerStatement).toHaveBeenCalledWith(contact.id);
    const root = harness.routeNativeElement as HTMLElement;
    expect(root.textContent).toContain('Estratto Conto / Partitario');
    expect(root.textContent).toContain('Totale fatturato');
    expect(root.textContent).toContain('Compensazione NC');
    const sourceNoteLink = root.querySelector(
      'a[href*="/fatturazione/nota-credito/note-1"]') as HTMLAnchorElement;
    expect(sourceNoteLink?.textContent).toContain('NC-202601-00001');
    expect(root.textContent).toContain('Saldo netto attuale');
  });
});
