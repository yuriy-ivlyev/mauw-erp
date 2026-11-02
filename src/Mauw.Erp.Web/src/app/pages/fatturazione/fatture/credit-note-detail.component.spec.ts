import '../../../locale';
import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap, provideRouter } from '@angular/router';
import { of } from 'rxjs';
import { CreditNoteDetail } from '../../../models/credit-note.model';
import { CreditNoteService } from '../../../services/credit-note.service';
import { CreditNoteDetailComponent } from './credit-note-detail.component';

describe('CreditNoteDetailComponent', () => {
  let service: jasmine.SpyObj<CreditNoteService>;
  let note: CreditNoteDetail;

  beforeEach(async () => {
    note = {
      id: 'credit-note-1',
      creditNoteNumber: 'NC-202610-00001',
      creditNoteDate: '2026-10-31T00:00:00Z',
      invoiceId: 'invoice-1',
      relatedInvoiceNumber: 'FAT-202610-00001',
      relatedInvoiceDate: '2026-10-15T00:00:00Z',
      contactId: 'contact-1',
      contactName: 'Cliente',
      taxableTotal: 10,
      vatTotal: 2.2,
      grossTotal: 12.2,
      reason: 'GoodsReturn',
      returnToWarehouse: false,
      warehouseId: null,
      sdiStatus: 'NotTransmitted',
      sdiFileName: null,
      sdiTransmissionId: null,
      sdiTransmittedAtUtc: null,
      items: []
    };
    service = jasmine.createSpyObj<CreditNoteService>(
      'CreditNoteService', ['getById', 'transmitToSdi', 'downloadXml']);
    service.getById.and.returnValue(of(note));

    await TestBed.configureTestingModule({
      imports: [CreditNoteDetailComponent],
      providers: [
        provideRouter([]),
        {
          provide: ActivatedRoute,
          useValue: {
            paramMap: of(convertToParamMap({ id: note.id })),
            queryParamMap: of(convertToParamMap({ returnTo: '/fatturazione/fatture' }))
          }
        },
        { provide: CreditNoteService, useValue: service }
      ]
    }).compileComponents();
  });

  it('shows the XML download and allows transmission only while NotTransmitted', () => {
    const fixture = TestBed.createComponent(CreditNoteDetailComponent);
    fixture.detectChanges();
    const buttons = Array.from(fixture.nativeElement.querySelectorAll('button')) as HTMLButtonElement[];

    expect(fixture.nativeElement.textContent).toContain('Non trasmessa');
    expect(buttons.some(button => button.textContent?.includes('Scarica XML TD04'))).toBeTrue();
    expect(buttons.some(button => button.textContent?.includes('Invia a SDI'))).toBeTrue();

    service.getById.and.returnValue(of({ ...note, sdiStatus: 'Transmitted' }));
    fixture.componentInstance.loadCreditNote(note.id);
    fixture.detectChanges();

    const transmittedButtons = Array.from(
      fixture.nativeElement.querySelectorAll('button')) as HTMLButtonElement[];
    expect(transmittedButtons.some(button => button.textContent?.includes('Scarica XML TD04'))).toBeTrue();
    expect(transmittedButtons.some(button => button.textContent?.includes('Invia a SDI'))).toBeFalse();
    fixture.destroy();
  });

  it('transmits a note and reloads its SDI state', () => {
    service.transmitToSdi.and.returnValue(of({
      id: note.id,
      creditNoteNumber: note.creditNoteNumber,
      creditNoteDate: note.creditNoteDate,
      taxableTotal: note.taxableTotal,
      vatTotal: note.vatTotal,
      grossTotal: note.grossTotal,
      reason: note.reason,
      sdiStatus: 'Transmitted'
    }));
    let loadCount = 0;
    service.getById.and.callFake(() => of({
      ...note,
      sdiStatus: loadCount++ === 0 ? 'NotTransmitted' : 'Transmitted'
    }));
    const fixture = TestBed.createComponent(CreditNoteDetailComponent);
    fixture.detectChanges();

    fixture.componentInstance.openTransmitConfirmation();
    fixture.componentInstance.confirmTransmission();
    fixture.detectChanges();

    expect(service.transmitToSdi).toHaveBeenCalledWith(note.id);
    expect(service.getById).toHaveBeenCalledTimes(2);
    expect(fixture.componentInstance.creditNote?.sdiStatus).toBe('Transmitted');
    expect(fixture.componentInstance.successMessage).toContain('trasmessa allo SDI');
    fixture.destroy();
  });
});
