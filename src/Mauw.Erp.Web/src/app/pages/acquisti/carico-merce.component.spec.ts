import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { of } from 'rxjs';
import { ComponentFixture } from '@angular/core/testing';
import { CaricoMerceComponent } from './carico-merce.component';
import { ContactService } from '../../services/contact.service';
import { SupplierDeliveryNoteService } from '../../services/supplier-delivery-note.service';

describe('CaricoMerceComponent', () => {
  let service: jasmine.SpyObj<SupplierDeliveryNoteService>;
  let fixture: ComponentFixture<CaricoMerceComponent>;
  const page = {
    items: [{
      id: 'note', documentNumber: 'DDT-17', documentDate: '2026-10-20T00:00:00Z',
      supplierName: 'Fornitore Moda', warehouseName: 'Milano', lineCount: 4, pieceCount: 27, status: 'Draft' as const
    }],
    pageNumber: 1, pageSize: 20, totalCount: 1, totalPages: 1
  };

  beforeEach(async () => {
    service = jasmine.createSpyObj<SupplierDeliveryNoteService>('SupplierDeliveryNoteService', ['list']);
    service.list.and.returnValue(of(page));
    const contacts = jasmine.createSpyObj<ContactService>('ContactService', ['getList']);
    contacts.getList.and.returnValue(of({
      items: [{
        id: 'supplier', businessName: 'Fornitore Moda', tradeName: null, vatNumber: null, taxCode: null,
        city: null, province: null, status: 'Active', isCustomer: false, isSupplier: true,
        isAgent: false, createdAt: '2026-10-20T00:00:00Z'
      }],
      pageNumber: 1, pageSize: 100, totalCount: 1, totalPages: 1
    }));
    await TestBed.configureTestingModule({
      imports: [CaricoMerceComponent],
      providers: [
        provideRouter([]),
        { provide: SupplierDeliveryNoteService, useValue: service },
        { provide: ContactService, useValue: contacts }
      ]
    }).compileComponents();
    fixture = TestBed.createComponent(CaricoMerceComponent);
    fixture.detectChanges();
  });

  it('shows only the filtered DDT list and navigates to the new-load wizard', () => {
    const pageElement: HTMLElement = fixture.nativeElement;
    expect(pageElement.textContent).toContain('Carico Merce');
    expect(pageElement.textContent).toContain('DDT registrati');
    expect(pageElement.textContent).not.toContain('Documento originale');
    expect(pageElement.textContent).not.toContain('Riconciliazione');
    expect(pageElement.querySelector('a[href="/acquisti/ddt-fornitore/nuovo"]')).toBeTruthy();
    expect(pageElement.textContent).toContain('4 / 27');
    expect(pageElement.textContent).toContain('Bozza');
  });

  it('sends all filters to the server and resets pagination when filters change', () => {
    const component = fixture.componentInstance;
    component.page = 3;
    component.search = '  DDT-17 ';
    component.supplierId = 'supplier';
    component.status = 'Draft';
    component.dateFrom = '2026-10-01';
    component.dateTo = '2026-10-31';
    component.applyFilters();
    expect(service.list).toHaveBeenCalledWith(1, {
      search: 'DDT-17', supplierId: 'supplier', status: 'Draft',
      dateFrom: '2026-10-01', dateTo: '2026-10-31'
    });
    component.resetFilters();
    expect(component.page).toBe(1);
    expect(component.search).toBe('');
    expect(component.supplierId).toBe('');
  });
});
