import { ComponentFixture, TestBed } from '@angular/core/testing';
import { HttpErrorResponse } from '@angular/common/http';
import { provideRouter } from '@angular/router';
import { of, Subject, throwError } from 'rxjs';
import { CaricoMerceWizardComponent } from './carico-merce-wizard.component';
import { SupplierDeliveryNoteService } from '../../services/supplier-delivery-note.service';
import { SupplierPurchaseOrderService } from '../../services/supplier-purchase-order.service';
import { ContactService } from '../../services/contact.service';
import { WarehouseService } from '../../services/warehouse.service';
import { ProductService } from '../../services/product.service';
import { SupplierArticleMatch, SupplierDeliveryNote } from '../../models/supplier-delivery-note.model';

describe('CaricoMerceWizardComponent', () => {
  let component: CaricoMerceWizardComponent;
  let fixture: ComponentFixture<CaricoMerceWizardComponent>;
  let service: jasmine.SpyObj<SupplierDeliveryNoteService>;
  const draft = (): SupplierDeliveryNote => ({
    id: 'note', supplierId: 'supplier', supplierName: 'Fornitore', warehouseId: 'warehouse',
    warehouseName: 'Magazzino', documentNumber: 'B-1', documentDate: '2026-10-20T00:00:00Z',
    uploadedAt: '2026-10-20T10:00:00Z', supplierPurchaseOrderId: null, filePath: 'doc.pdf', status: 'Draft', version: 'version',
    lines: [{
      id: 'line', supplierItemCode: 'SUP-M', articleVariantId: 'variant',
      articleDescription: 'Maglia - Blu / M', description: 'Maglia M',
      supplierPurchaseOrderLineId: null,
      quantity: 5, unitPrice: '12.34567890', lineStatus: 'Matched', stockMovementId: null
    }]
  });

  beforeEach(async () => {
    service = jasmine.createSpyObj<SupplierDeliveryNoteService>('SupplierDeliveryNoteService', ['list', 'get', 'findArticle', 'upload', 'save', 'action', 'download']);
    service.list.and.returnValue(of({ items: [], pageNumber: 1, pageSize: 20, totalCount: 0, totalPages: 0 }));
    service.save.and.returnValue(of(draft()));
    await TestBed.configureTestingModule({
      imports: [CaricoMerceWizardComponent],
      providers: [
        provideRouter([]),
        { provide: SupplierDeliveryNoteService, useValue: service },
        { provide: ContactService, useValue: { getList: () => of({ items: [] }) } },
        { provide: WarehouseService, useValue: { getList: () => of({ items: [] }) } },
        { provide: ProductService, useValue: {} },
        { provide: SupplierPurchaseOrderService, useValue: { list: () => of([]) } }
      ]
    }).compileComponents();
    fixture = TestBed.createComponent(CaricoMerceWizardComponent);
    component = fixture.componentInstance;
    component.selected = draft();
  });

  it('requires saved, matched positive integer quantities before confirmation', () => {
    expect(component.canConfirm).toBeTrue();
    component.dirty = true;
    expect(component.canConfirm).toBeFalse();
    component.dirty = false;
    component.selected!.lines[0].quantity = 1.5;
    expect(component.canConfirm).toBeFalse();
    component.selected!.lines[0].quantity = 1;
    component.selected!.lines[0].articleVariantId = null;
    expect(component.canConfirm).toBeFalse();
    component.selected!.status = 'Confirmed';
    expect(component.editable).toBeFalse();
  });

  it('rejects fractional quantities without making any save request', () => {
    component.selected!.lines[0].quantity = 1.5;
    component.save();
    expect(service.save).not.toHaveBeenCalled();
    expect(component.error).toContain('intere');
  });

  it('preserves all unit price digits and accepts the Italian decimal separator', () => {
    component.selected!.lines[0].unitPrice = '12,34567890';
    component.save();
    expect(service.save).toHaveBeenCalledWith('note', jasmine.objectContaining({
      version: 'version',
      lines: [jasmine.objectContaining({ quantity: 5, unitPrice: '12.34567890' })]
    }), false);
    expect(component.dirty).toBeFalse();
  });

  it('asks for confirmation before removing a line', () => {
    component.removeLine(0);
    expect(component.deleteConfirmationOpen).toBeTrue();
    expect(component.selected!.lines).toHaveSize(1);

    component.cancelRemoveLine();
    expect(component.selected!.lines).toHaveSize(1);

    component.removeLine(0);
    component.confirmRemoveLine();
    expect(component.selected!.lines).toHaveSize(0);
    expect(component.dirty).toBeTrue();
    expect(component.deleteConfirmationOpen).toBeFalse();
  });

  it('searches the supplier code on click and immediately associates the article without saving', () => {
    service.findArticle.and.returnValue(of({
      articleVariantId: 'found-variant', articleDescription: 'Pantalone - Blu / M'
    }));
    fixture.detectChanges();
    component.selected = draft();
    component.activeStep = 2;
    component.addLine();
    const line = component.selected.lines[1];
    line.supplierItemCode = ' SUP-PANT-M ';
    fixture.detectChanges();

    const buttons = fixture.nativeElement.querySelectorAll('button[aria-label="Cerca per codice prodotto fornitore"]') as NodeListOf<HTMLButtonElement>;
    buttons[1].click();
    fixture.detectChanges();

    expect(service.findArticle).toHaveBeenCalledOnceWith('note', 'SUP-PANT-M');
    expect(line.articleVariantId).toBe('found-variant');
    expect(line.articleDescription).toBe('Pantalone - Blu / M');
    expect(line.description).toBe('Pantalone - Blu / M');
    expect(line.lineStatus).toBe('Matched');
    expect(service.save).not.toHaveBeenCalled();
    expect(component.dirty).toBeTrue();
    expect(component.busy).toBeFalse();
    expect(fixture.nativeElement.textContent).toContain('Pantalone - Blu / M');
  });

  it('clears the previous association when the supplier code changes', () => {
    const line = component.selected!.lines[0];
    line.supplierItemCode = 'NEW';
    component.supplierCodeChanged(line);
    expect(line.articleVariantId).toBeNull();
    expect(line.articleDescription).toBeNull();
    expect(line.lineStatus).toBe('NewArticleNeeded');
    expect(component.dirty).toBeTrue();
  });

  it('shows lookup errors without saving or overwriting the existing line', () => {
    const line = component.selected!.lines[0];
    service.findArticle.and.returnValue(throwError(() => new HttpErrorResponse({
      status: 400, error: { detail: 'Nessun articolo univoco trovato.' }
    })));
    component.findArticle(line);
    expect(component.error).toBe('Nessun articolo univoco trovato.');
    expect(line.articleVariantId).toBe('variant');
    expect(component.busy).toBeFalse();
    expect(service.save).not.toHaveBeenCalled();
  });

  it('does not search an empty supplier code or a read-only document', () => {
    const line = component.selected!.lines[0];
    line.supplierItemCode = ' ';
    component.findArticle(line);
    expect(component.error).toContain('codice prodotto fornitore');
    line.supplierItemCode = 'SUP-M';
    component.selected!.status = 'Confirmed';
    component.findArticle(line);
    expect(service.findArticle).not.toHaveBeenCalled();
  });

  it('ignores lookup results after changing the document', () => {
    const pending = new Subject<SupplierArticleMatch>();
    service.findArticle.and.returnValue(pending);
    const line = component.selected!.lines[0];
    component.findArticle(line);
    expect(component.busy).toBeTrue();
    component.selected = draft();
    pending.next({ articleVariantId: 'stale', articleDescription: 'Altro articolo' });
    pending.complete();
    expect(line.articleVariantId).toBe('variant');
    expect(component.dirty).toBeFalse();
    expect(component.busy).toBeFalse();
  });

  it('allows replacing a code-based association manually', () => {
    const line = component.selected!.lines[0];
    service.findArticle.and.returnValue(of({
      articleVariantId: 'automatic', articleDescription: 'Maglia'
    }));
    component.findArticle(line);
    component.matchingLine = line;
    component.match({
      variantId: 'manual', productId: 'product', productName: 'Maglia',
      sku: null, barcode: '123', colorName: 'Rosso', sizeName: 'L', currentStock: 0,
      supplierCode: null, purchaseCost: 0
    });
    expect(line.articleVariantId).toBe('manual');
    expect(line.articleDescription).toBe('Maglia - Rosso / L');
    expect(line.lineStatus).toBe('Matched');
  });

  it('imports structured OCR data without monetary arithmetic and requests matching', () => {
    component.ocrJson = '{"documentNumber":"OCR-1","lines":[{"supplierItemCode":"NEW","description":"Maglia","quantity":2,"unitPrice":"1.12345678"}]}';
    component.importOcr();
    expect(service.save).toHaveBeenCalledWith('note', jasmine.objectContaining({
      documentNumber: 'OCR-1',
      lines: [jasmine.objectContaining({ articleVariantId: null, quantity: 2, unitPrice: '1.12345678' })]
    }), true);
  });

  it('does not replace editable lines when OCR input is malformed or fractional', () => {
    const lines = component.selected!.lines;
    component.ocrJson = '{"lines":[{"supplierItemCode":"NEW","description":"Maglia","quantity":1.5,"unitPrice":"10.00"}]}';
    component.importOcr();
    expect(service.save).not.toHaveBeenCalled();
    expect(component.selected!.lines).toBe(lines);
    expect(component.error).toBeTruthy();
  });
});
