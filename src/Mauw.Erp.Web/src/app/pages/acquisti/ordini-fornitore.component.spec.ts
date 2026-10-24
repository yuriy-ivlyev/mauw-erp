import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { of } from 'rxjs';
import { OrdiniFornitoreComponent } from './ordini-fornitore.component';
import { ContactService } from '../../services/contact.service';
import { ProductService } from '../../services/product.service';
import { SupplierPurchaseOrderService } from '../../services/supplier-purchase-order.service';
import { SupplierPurchaseOrder, SupplierReconciliation } from '../../models/supplier-purchase-order.model';

describe('OrdiniFornitoreComponent', () => {
  let fixture: ComponentFixture<OrdiniFornitoreComponent>;
  let component: OrdiniFornitoreComponent;
  let service: jasmine.SpyObj<SupplierPurchaseOrderService>;
  let products: jasmine.SpyObj<ProductService>;

  const order: SupplierPurchaseOrder = {
    id: 'order-1', orderNumber: 'OF-20261024-000001', supplierId: 'supplier-1',
    supplierName: 'Fornitore test', orderDate: '2026-10-24T00:00:00Z',
    status: 'Confirmed', notes: null, version: 'version-1',
    lines: [{
      id: 'line-1', articleVariantId: 'variant-1', sku: 'SKU-1',
      articleDescription: 'Maglia blu / M', quantityOrdered: 10, quantityReceived: 8,
      unitCost: '12.34567890'
    }]
  };
  const reconciliation: SupplierReconciliation = {
    supplierPurchaseOrderId: order.id, orderNumber: order.orderNumber, status: order.status,
    lines: [{
      supplierPurchaseOrderLineId: 'line-1', articleVariantId: 'variant-1', sku: 'SKU-1',
      articleDescription: 'Maglia blu / M', quantityOrdered: 10, quantityReceived: 8,
      delta: -2, status: 'Shortage', receipts: []
    }]
  };

  beforeEach(async () => {
    service = jasmine.createSpyObj<SupplierPurchaseOrderService>(
      'SupplierPurchaseOrderService',
      ['list', 'get', 'create', 'confirm', 'reconcile', 'shortages', 'returnDrafts',
        'createReturnDraft', 'issueReturnDraft', 'close']
    );
    service.list.and.returnValue(of([order]));
    service.get.and.returnValue(of(order));
    service.create.and.returnValue(of(order));
    service.reconcile.and.returnValue(of(reconciliation));
    service.returnDrafts.and.returnValue(of([]));
    products = jasmine.createSpyObj<ProductService>('ProductService', ['searchVariants']);
    products.searchVariants.and.returnValue(of([]));

    await TestBed.configureTestingModule({
      imports: [OrdiniFornitoreComponent],
      providers: [
        provideRouter([]),
        { provide: SupplierPurchaseOrderService, useValue: service },
        { provide: ContactService, useValue: { getList: () => of({ items: [] }) } },
        { provide: ProductService, useValue: products }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(OrdiniFornitoreComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('lists the received order and displays shortages with the correct visual state', () => {
    expect(component.orders).toEqual([order]);
    expect(component.rowClass(reconciliation.lines[0])).toBe('shortage');
    expect(component.rowLabel('Shortage')).toBe('Mancanza');
    expect(component.statusLabel('PartiallyReceived')).toBe('Parzialmente ricevuto');
  });

  it('sends decimal unit costs as strings when creating a draft order', () => {
    component.supplierId = order.supplierId;
    component.draftLines = [{
      articleVariantId: 'variant-1', articleDescription: 'Maglia blu / M',
      quantityOrdered: 3, unitCost: '12.34567890'
    }];

    component.createOrder();

    expect(service.create).toHaveBeenCalledWith(jasmine.objectContaining({
      supplierId: order.supplierId,
      lines: [{ articleVariantId: 'variant-1', quantityOrdered: 3, unitCost: '12.34567890' }]
    }));
  });

  it('uses the found article purchase cost as the draft unit cost', () => {
    component.addVariant({
      variantId: 'variant-2', productId: 'product-1', productName: 'Scarpa',
      sku: 'SKU-2', barcode: 'SKU-2', colorName: null, sizeName: null,
      currentStock: 0, supplierCode: 'FORN-22', purchaseCost: 18.75
    });

    expect(component.draftLines[0].unitCost).toBe('18.75');
    expect(component.articleSearchOpen).toBeFalse();
  });

  it('passes the selected supplier to article search', () => {
    component.supplierId = order.supplierId;
    component.variantQuery = 'FORN-22';

    component.searchVariants();

    expect(products.searchVariants).toHaveBeenCalledWith('FORN-22', 30, order.supplierId);
    expect(component.searchDone).toBeTrue();
  });
});
