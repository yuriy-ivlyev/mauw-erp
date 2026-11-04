import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, Router, provideRouter } from '@angular/router';
import { of } from 'rxjs';
import { ContactService } from '../../services/contact.service';
import { AuthService } from '../../services/auth.service';
import { PayableDocumentService } from '../../services/payable-document.service';
import { PayableDocumentsComponent } from './payable-documents.component';
import { PayableDocument } from '../../models/payable-document.model';

describe('PayableDocumentsComponent', () => {
  let fixture: ComponentFixture<PayableDocumentsComponent>;
  let payableApi: jasmine.SpyObj<PayableDocumentService>;
  let contactsApi: jasmine.SpyObj<ContactService>;
  let auth: jasmine.SpyObj<AuthService>;

  beforeEach(async () => {
    payableApi = jasmine.createSpyObj<PayableDocumentService>('PayableDocumentService', [
      'list', 'listMyReimbursements', 'get', 'getMyReimbursement',
      'createSupplierInvoice', 'createReimbursement', 'update', 'uploadAttachment',
      'downloadAttachment', 'submit', 'approve', 'reject', 'cancel', 'recordPayment'
    ]);
    contactsApi = jasmine.createSpyObj<ContactService>('ContactService', ['getList']);
    auth = jasmine.createSpyObj<AuthService>('AuthService', ['hasRole']);
    auth.hasRole.and.callFake(role => role === 'Admin');
    payableApi.list.and.returnValue(of({
      items: [], pageNumber: 1, pageSize: 20, totalCount: 0, totalPages: 0
    }));
    payableApi.listMyReimbursements.and.returnValue(of({
      items: [], pageNumber: 1, pageSize: 20, totalCount: 0, totalPages: 0
    }));
    contactsApi.getList.and.returnValue(of({
      items: [], pageNumber: 1, pageSize: 100, totalCount: 0, totalPages: 0
    }));

    await TestBed.configureTestingModule({
      imports: [PayableDocumentsComponent],
      providers: [
        provideRouter([]),
        {
          provide: ActivatedRoute,
          useValue: {
            data: of({}),
            snapshot: {
              data: {},
              paramMap: { get: () => null },
              queryParamMap: { get: () => null }
            }
          }
        },
        { provide: PayableDocumentService, useValue: payableApi },
        { provide: ContactService, useValue: contactsApi },
        { provide: AuthService, useValue: auth }
      ]
    }).compileComponents();
    fixture = TestBed.createComponent(PayableDocumentsComponent);
    fixture.detectChanges();
  });

  it('loads the payable list and opens creation on a separate route', () => {
    expect(payableApi.list).toHaveBeenCalledWith(1, 20, {
      search: undefined,
      kind: undefined,
      status: undefined,
      dateFrom: undefined,
      dateTo: undefined
    });
    expect(contactsApi.getList).toHaveBeenCalledWith({
      isSupplier: true, status: 'Active', page: 1, pageSize: 100
    });

    const addButton = fixture.nativeElement.querySelector(
      'button[aria-label="Nuova fattura passiva"]'
    ) as HTMLButtonElement;
    expect(addButton).not.toBeNull();
    const navigation = spyOn(TestBed.inject(Router), 'navigate').and.returnValue(Promise.resolve(true));
    addButton.click();

    expect(navigation).toHaveBeenCalledWith(['/acquisti/passivi', 'nuovo'], {
      queryParams: { returnTo: '/acquisti/passivi?page=1&pageSize=20' }
    });
    expect(fixture.componentInstance.editing).toBeFalse();
  });

  it('creates a payable draft using amounts and VAT copied from the supplier document', () => {
    const component = fixture.componentInstance;
    const invoice: PayableDocument = {
      id: 'payable-1',
      documentCode: 'AP-1234567890ABCDEF',
      kind: 'SupplierInvoice',
      supplierId: 'supplier-1',
      supplierName: 'Fornitore',
      sourceDocumentNumber: 'FT-1',
      documentDate: '2026-11-04',
      dueDate: '2026-12-04',
      beneficiaryName: null,
      status: 'Draft',
      paymentStatus: 'Unpaid',
      taxableTotal: 100,
      vatTotal: 22,
      grossTotal: 122,
      paidAmount: 0,
      balanceDue: 122,
      hasAttachment: false,
      notes: null,
      attachmentFileName: null,
      attachmentContentType: null,
      attachmentLength: null,
      createdByUserId: 'accounting',
      approvedByUserId: null,
      submittedAtUtc: null,
      approvedAtUtc: null,
      rejectionReason: null,
      version: 'version-1',
      lines: [],
      payments: []
    };
    payableApi.createSupplierInvoice.and.returnValue(of(invoice));
    component.editorMode = true;
    component.editing = true;
    component.kind = 'SupplierInvoice';
    component.supplierId = 'supplier-1';
    component.sourceDocumentNumber = 'FT-1';
    component.documentDate = '2026-11-04';
    component.dueDate = '2026-12-04';
    component.draftLines = [{
      description: 'Bolletta energia',
      category: 'Utilities',
      taxableAmount: 100,
      vatRate: 22,
      natureCode: null,
      vatAmount: 22,
      grossAmount: 122,
      costCenter: 'Sede',
      project: null
    }];

    component.saveDraft();

    expect(payableApi.createSupplierInvoice).toHaveBeenCalledWith(jasmine.objectContaining({
      kind: 'SupplierInvoice',
      supplierId: 'supplier-1',
      sourceDocumentNumber: 'FT-1',
      lines: [jasmine.objectContaining({
        taxableAmount: 100, vatAmount: 22, grossAmount: 122, vatRate: 22
      })]
    }));
    expect(component.selected?.documentCode).toBe(invoice.documentCode);
  });
});
