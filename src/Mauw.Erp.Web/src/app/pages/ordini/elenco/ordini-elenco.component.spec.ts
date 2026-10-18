import '../../../locale';
import { ComponentFixture, TestBed } from '@angular/core/testing';
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
      items: [],
      linkedDocuments: null
    }));
    service.getList.and.returnValue(of({
      items: orders, pageNumber: 1, pageSize: 20, totalCount: 3, totalPages: 1
    }));
    service.downloadOrderPdf.and.returnValue(pdf);
    await TestBed.configureTestingModule({
      imports: [OrdiniElencoComponent],
      providers: [{ provide: OrderService, useValue: service }]
    }).compileComponents();
    fixture = TestBed.createComponent(OrdiniElencoComponent);
  });

  it('renders every row, Italian amounts and PDF labels without partial rows', () => {
    expect(() => fixture.detectChanges()).not.toThrow();
    const rows = fixture.nativeElement.querySelectorAll('tbody tr') as NodeListOf<HTMLElement>;
    expect(rows.length).toBe(3);
    rows.forEach((row, index) => {
      expect(row.textContent).toContain(`ORD-${index}`);
      expect(row.textContent).toContain('100,00');
      expect(row.textContent).toContain('122,00');
      expect(row.querySelector('button')?.textContent).toContain('ORD PDF');
    });
  });

  it('keeps the row count stable after repeated failed PDF downloads and displays the error', () => {
    fixture.detectChanges();
    for (let attempt = 0; attempt < 3; attempt++) {
      pdf = new Subject<Blob>();
      service.downloadOrderPdf.and.returnValue(pdf);
      const button = fixture.nativeElement.querySelector('tbody button') as HTMLButtonElement;
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
});
