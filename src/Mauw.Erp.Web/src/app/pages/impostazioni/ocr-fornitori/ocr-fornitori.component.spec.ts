import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { of } from 'rxjs';
import { OcrFornitoriComponent } from './ocr-fornitori.component';
import { SupplierDocumentOcrSettingsService } from '../../../services/supplier-document-ocr-settings.service';
import { SupplierDocumentOcrHeaderAlias } from '../../../models/supplier-document-ocr-alias.model';

describe('OcrFornitoriComponent', () => {
  let component: OcrFornitoriComponent;
  let fixture: ComponentFixture<OcrFornitoriComponent>;
  let service: jasmine.SpyObj<SupplierDocumentOcrSettingsService>;

  const codeAlias: SupplierDocumentOcrHeaderAlias = {
    id: 'alias-code',
    column: 'SupplierCode',
    alias: 'Codice articolo',
    position: 0
  };

  beforeEach(async () => {
    service = jasmine.createSpyObj<SupplierDocumentOcrSettingsService>(
      'SupplierDocumentOcrSettingsService', ['list', 'add', 'delete']);
    service.list.and.returnValue(of([codeAlias]));
    service.add.and.returnValue(of({
      id: 'alias-new',
      column: 'SupplierCode',
      alias: 'N. Art.',
      position: 1
    }));
    service.delete.and.returnValue(of(undefined));

    await TestBed.configureTestingModule({
      imports: [OcrFornitoriComponent],
      providers: [
        provideRouter([]),
        { provide: SupplierDocumentOcrSettingsService, useValue: service }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(OcrFornitoriComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('loads global aliases grouped by document field', () => {
    expect(service.list).toHaveBeenCalled();
    expect(component.aliasesFor('SupplierCode')).toEqual([codeAlias]);
    expect(component.aliasesFor('Description')).toEqual([]);
  });

  it('adds an alias to the selected field', () => {
    component.newAliases.SupplierCode = ' N. Art. ';
    component.add('SupplierCode');

    expect(service.add).toHaveBeenCalledOnceWith({
      column: 'SupplierCode',
      alias: 'N. Art.'
    });
    expect(component.aliasesFor('SupplierCode').map(alias => alias.alias))
      .toEqual(['Codice articolo', 'N. Art.']);
    expect(component.newAliases.SupplierCode).toBe('');
  });

  it('requires confirmation before deleting an alias', () => {
    component.requestDelete(codeAlias);
    expect(component.deleteConfirmationOpen).toBeTrue();
    expect(service.delete).not.toHaveBeenCalled();

    component.confirmDelete();

    expect(service.delete).toHaveBeenCalledOnceWith(codeAlias.id);
    expect(component.aliases).toEqual([]);
    expect(component.deleteConfirmationOpen).toBeFalse();
  });

  it('preserves aliases when delete confirmation is cancelled', () => {
    component.requestDelete(codeAlias);
    component.cancelDelete();

    expect(service.delete).not.toHaveBeenCalled();
    expect(component.aliases).toEqual([codeAlias]);
  });
});
