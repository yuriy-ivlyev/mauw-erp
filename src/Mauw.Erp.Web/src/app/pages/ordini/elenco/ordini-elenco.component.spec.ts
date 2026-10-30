import '../../../locale';
import { HttpErrorResponse } from '@angular/common/http';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { of, Subject } from 'rxjs';
import { Order } from '../../../models/order.model';
import { OrderService } from '../../../services/order.service';
import { OrdiniElencoComponent } from './ordini-elenco.component';

describe('OrdiniElencoComponent', () => {
  let fixture: ComponentFixture<OrdiniElencoComponent>;
  let service: jasmine.SpyObj<OrderService>;
  let pdf: Subject<Blob>;

  beforeEach(async () => {
    pdf = new Subject<Blob>();
    service = jasmine.createSpyObj<OrderService>('OrderService', [
      'getList', 'downloadOrderPdf', 'downloadInvoicePdf', 'downloadDdtPdf'
    ]);
    const orders: Order[] = Array.from({ length: 3 }, (_, index) => ({
      id: `order-${index}`,
      orderNumber: `ORD-${index}`,
      contactId: 'customer',
      contactName: 'Cliente',
      orderDate: '2026-10-18',
      customerDiscountRate: 0,
      status: 'Draft',
      taxableTotal: 100,
      vatTotal: 22,
      grossTotal: 122,
      orderType: 'Standard',
      fulfillmentType: 'StandardShipping',
      items: [],
      linkedDocuments: null,
      statusDescription: 'Bozza',
      isReadOnly: false,
      permittedActions: []
    }));
    service.getList.and.returnValue(of({
      items: orders, pageNumber: 1, pageSize: 20, totalCount: 3, totalPages: 1
    }));
    service.downloadOrderPdf.and.returnValue(pdf);
    await TestBed.configureTestingModule({
      imports: [OrdiniElencoComponent],
      providers: [
        provideRouter([]),
        { provide: OrderService, useValue: service }
      ]
    }).compileComponents();
    fixture = TestBed.createComponent(OrdiniElencoComponent);
  });

  it('renders every row, Italian amounts and PDF labels without partial rows', () => {
    expect(() => fixture.detectChanges()).not.toThrow();
    const newOrderButton = fixture.nativeElement.querySelector('button') as HTMLButtonElement;
    expect(newOrderButton.getAttribute('aria-label')).toBe('Nuovo ordine');
    expect(newOrderButton.className).toContain('bg-green-600');
    const rows = fixture.nativeElement.querySelectorAll('tbody tr') as NodeListOf<HTMLElement>;
    expect(rows.length).toBe(3);
    rows.forEach((row, index) => {
      expect(row.textContent).toContain(`ORD-${index}`);
      expect(row.querySelector('td:first-child a')).toBeNull();
      expect(row.querySelector('td:last-child app-table-action-button button')?.getAttribute('aria-label'))
        .toBe(`Modifica ordine ORD-${index}`);
      expect(row.textContent).toContain('100,00');
      expect(row.textContent).toContain('122,00');
      expect(Array.from(row.querySelectorAll('button')).some(button => button.textContent?.includes('ORD PDF'))).toBeTrue();
    });

    const navigation = spyOn(TestBed.inject(Router), 'navigate').and.returnValue(Promise.resolve(true));
    const editButton = fixture.nativeElement.querySelector(
      'tbody app-table-action-button button'
    ) as HTMLButtonElement;
    editButton.click();
    expect(navigation).toHaveBeenCalledWith(['/vendite/ordini', 'order-0']);
  });

  it('keeps the row count stable after repeated failed PDF downloads and displays the error', () => {
    fixture.detectChanges();
    for (let attempt = 0; attempt < 3; attempt++) {
      pdf = new Subject<Blob>();
      service.downloadOrderPdf.and.returnValue(pdf);
      const buttons = fixture.nativeElement.querySelectorAll('tbody td:last-child button') as NodeListOf<HTMLButtonElement>;
      const button = Array.from(buttons).find(candidate => candidate.textContent?.includes('ORD PDF'))!;
      button.click();
      fixture.detectChanges();
      expect(button.disabled).toBeTrue();
      expect(fixture.nativeElement.querySelectorAll('tbody tr').length).toBe(3);
      pdf.error(new Error('PDF generation failed'));
      fixture.detectChanges();
      expect(fixture.nativeElement.querySelectorAll('tbody tr').length).toBe(3);
      expect(fixture.nativeElement.querySelector('[role="alert"]').textContent)
        .toContain('Impossibile scaricare il PDF');
      expect(button.disabled).toBeFalse();
      expect(button.textContent).toContain('ORD PDF');
    }
    expect(service.getList).toHaveBeenCalledTimes(1);
  });

  it('prevents concurrent PDF requests and cancels the subscription on destruction', () => {
    fixture.detectChanges();
    const component = fixture.componentInstance;
    component.downloadPdf('order', 'order-0', 'ordine.pdf');
    component.downloadPdf('order', 'order-1', 'ordine.pdf');
    expect(service.downloadOrderPdf).toHaveBeenCalledTimes(1);
    expect(pdf.observed).toBeTrue();
    fixture.destroy();
    expect(pdf.observed).toBeFalse();
    expect(component.downloadingPdf).toBeNull();
  });

  it('displays the API reason when the PDF request fails with a server validation error', async () => {
    fixture.detectChanges();
    fixture.componentInstance.downloadPdf('order', 'order-0', 'ordine.pdf');
    const body = new Blob(['Configurare il capitale sociale in Impostazioni > Azienda.']);
    pdf.error(new HttpErrorResponse({
      status: 400,
      error: body
    }));

    await body.text();
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('[role="alert"]').textContent)
      .toContain('Configurare il capitale sociale in Impostazioni > Azienda');
  });
});
