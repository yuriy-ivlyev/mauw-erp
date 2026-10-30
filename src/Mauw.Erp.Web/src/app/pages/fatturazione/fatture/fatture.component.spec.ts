import '../../../locale';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { of } from 'rxjs';
import { Invoice } from '../../../models/invoice.model';
import { InvoiceService } from '../../../services/invoice.service';
import { FattureComponent } from './fatture.component';

describe('FattureComponent', () => {
  let fixture: ComponentFixture<FattureComponent>;
  let invoiceService: jasmine.SpyObj<InvoiceService>;
  let invoice: Invoice;

  beforeEach(async () => {
    invoice = {
      id: 'invoice-1',
      invoiceNumber: 'INV-102026-001',
      contactId: 'contact-1',
      orderId: null,
      contactName: 'Cliente',
      invoiceDate: '2026-10-29T00:00:00Z',
      taxableTotal: 100,
      vatTotal: 22,
      grossTotal: 122,
      shippingCost: 0,
      shippingVatRate: null,
      shippingNatureCode: null,
      sdiSubmittedAt: null,
      paidAmount: 0,
      balanceDue: 122,
      status: 'Confirmed',
      items: [],
      payments: []
    };
    invoiceService = jasmine.createSpyObj<InvoiceService>('InvoiceService', [
      'getList', 'generateFromOrder', 'downloadPdf'
    ]);
    invoiceService.getList.and.returnValue(of({
      items: [invoice], pageNumber: 1, pageSize: 20, totalCount: 1, totalPages: 1
    }));
    await TestBed.configureTestingModule({
      imports: [FattureComponent],
      providers: [
        provideRouter([{ path: 'fatturazione/fatture/:id', component: FattureComponent }]),
        { provide: InvoiceService, useValue: invoiceService }
      ]
    }).compileComponents();
    fixture = TestBed.createComponent(FattureComponent);
  });

  it('renders the default newest-first list and uses only the detail button for navigation', () => {
    fixture.detectChanges();

    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';
    const firstCell = fixture.nativeElement.querySelector('tbody tr td:first-child') as HTMLElement;
    const actionButtons = Array.from(fixture.nativeElement.querySelectorAll('tbody td:last-child button')) as HTMLButtonElement[];
    const detailButton = actionButtons[0];
    expect(text).toContain('INV-102026-001');
    expect(text).toContain('Cliente');
    expect(text).toContain('122,00');
    expect(firstCell.querySelector('a')).toBeNull();
    expect(detailButton.textContent).toContain('Dettaglio');
    expect(actionButtons.map(button => button.textContent?.trim())).toEqual(['Dettaglio', 'PDF']);
    expect(actionButtons.every(button => button.classList.contains('h-7'))).toBeTrue();
    expect(invoiceService.getList).toHaveBeenCalledWith({
      page: 1,
      pageSize: 20,
      search: undefined,
      status: undefined,
      dateFrom: undefined,
      dateTo: undefined,
      unpaidOnly: false,
      sortBy: 'invoiceDate',
      sortDescending: true
    });
  });

  it('passes status, date range and unpaid filter to the server', () => {
    fixture.detectChanges();
    const component = fixture.componentInstance;
    component.statusFilter = 'Confirmed';
    component.dateFrom = '2026-10-01';
    component.dateTo = '2026-10-31';
    component.unpaidOnly = true;

    component.onFilterChange();

    expect(invoiceService.getList).toHaveBeenCalledWith(jasmine.objectContaining({
      status: 'Confirmed',
      dateFrom: '2026-10-01',
      dateTo: '2026-10-31',
      unpaidOnly: true,
      sortBy: 'invoiceDate',
      sortDescending: true
    }));
  });

  it('does not request an invalid date range and displays a validation error', () => {
    fixture.detectChanges();
    const component = fixture.componentInstance;
    component.dateFrom = '2026-10-31';
    component.dateTo = '2026-10-01';
    const callsBefore = invoiceService.getList.calls.count();

    component.onFilterChange();
    fixture.detectChanges();

    expect(invoiceService.getList.calls.count()).toBe(callsBefore);
    expect(component.errorMessage).toContain('data iniziale');
  });

  it('updates server sorting when a column header is selected', () => {
    fixture.detectChanges();
    fixture.componentInstance.onSortChange({ sortBy: 'grossTotal', sortDescending: false });

    expect(invoiceService.getList).toHaveBeenCalledWith(jasmine.objectContaining({
      sortBy: 'grossTotal',
      sortDescending: false
    }));
  });

});
