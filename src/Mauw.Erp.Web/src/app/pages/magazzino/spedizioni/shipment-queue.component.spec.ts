import { ComponentFixture, TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { Order } from '../../../models/order.model';
import { OrderService } from '../../../services/order.service';
import { ShipmentQueueComponent } from './shipment-queue.component';

describe('ShipmentQueueComponent', () => {
  let fixture: ComponentFixture<ShipmentQueueComponent>;
  let service: jasmine.SpyObj<OrderService>;
  let order: Order;
  let orderLineId: string;

  beforeEach(async () => {
    orderLineId = 'line-1';
    order = {
      id: 'order-1',
      orderNumber: 'ORD-202610-001',
      contactId: 'customer-1',
      contactName: 'Cliente test',
      orderDate: '2026-10-29T00:00:00Z',
      customerDiscountRate: 0,
      status: 'Confirmed',
      taxableTotal: 100,
      vatTotal: 22,
      grossTotal: 122,
      orderType: 'Standard',
      fulfillmentType: 'StandardShipping',
      items: [{
        id: orderLineId,
        orderId: 'order-1',
        variantId: 'variant-1',
        quantity: 3,
        unitNetPrice: 10,
        vatRate: 22,
        lineNetTotal: 30,
        lineVatTotal: null,
        lineGrossTotal: null,
        natureCode: null,
        discountPercent: 0,
        shippedQuantity: 1,
        remainingQuantity: 2,
        sku: 'SKU-001',
        productName: 'Maglia',
        sizeName: 'M',
        colorName: 'Blu'
      }],
      linkedDocuments: null,
      statusDescription: 'Confermato',
      isReadOnly: true,
      permittedActions: [{
        action: 'StartPicking',
        label: 'Prendi in carico',
        endpoint: '/api/v1/orders/order-1/start-picking',
        method: 'POST',
        style: 'primary',
        requiresDialog: false,
        dialogType: null
      }]
    };

    service = jasmine.createSpyObj<OrderService>('OrderService', [
      'getFulfillmentQueue', 'getById', 'postAction', 'createShipment'
    ]);
    service.getFulfillmentQueue.and.returnValue(of({
      items: [order], pageNumber: 1, pageSize: 50, totalCount: 1, totalPages: 1
    }));
    service.getById.and.returnValue(of({ ...order, status: 'Picking' }));
    service.postAction.and.returnValue(of(order));
    service.createShipment.and.returnValue(of({
      order: { ...order, status: 'Shipped' },
      documentFileName: 'ddt-DDT-2026-000001.pdf',
      contentType: 'application/pdf',
      documentBytes: '',
      shipment: {
        id: 'shipment-1',
        shipmentNumber: 'SPED-2026-000001',
        ddtNumber: 'DDT-2026-000001',
        carrierName: 'GLS',
        trackingCode: null,
        boxCount: 1,
        totalWeightKg: null,
        shippedAtUtc: '2026-10-29T12:00:00Z',
        lines: [{
          orderLineId: orderLineId,
          productId: 'product-1',
          sku: 'SKU-001',
          quantityShipped: 1
        }]
      }
    }));

    await TestBed.configureTestingModule({
      imports: [ShipmentQueueComponent],
      providers: [{ provide: OrderService, useValue: service }]
    }).compileComponents();
    fixture = TestBed.createComponent(ShipmentQueueComponent);
  });

  it('renders the logistics queue and starts the picking workflow before fulfillment', () => {
    fixture.detectChanges();

    const rows = fixture.nativeElement.querySelectorAll('tbody tr') as NodeListOf<HTMLElement>;
    expect(rows.length).toBe(1);
    expect(rows[0].textContent).toContain('ORD-202610-001');
    expect(rows[0].textContent).toContain('Cliente test');
    expect(rows[0].textContent).toContain('2');
    expect(rows[0].textContent).toContain('Confermato');

    fixture.componentInstance.prepareShipment(order);
    expect(service.postAction).toHaveBeenCalledWith(order.permittedActions[0]);
    expect(service.getById).toHaveBeenCalledWith(order.id);
    expect(fixture.componentInstance.selectedOrder?.status).toBe('Picking');
    expect(fixture.componentInstance.totalToShip).toBe(2);
    expect(fixture.componentInstance.isShipmentAtBalance).toBeTrue();

    fixture.componentInstance.shipmentQuantities[orderLineId] = 1;
    fixture.componentInstance.onQuantityChange(fixture.componentInstance.selectedOrder!.items[0]);
    expect(fixture.componentInstance.shipmentTypeLabel()).toBe('Spedizione Parziale');
    expect(fixture.componentInstance.totalToShip).toBe(1);
  });

  it('submits the picked quantities, shows the generated DDT and refreshes the queue', () => {
    fixture.detectChanges();
    spyOn(window, 'open').and.returnValue(null);
    spyOn(URL, 'createObjectURL').and.returnValue('blob:ddt');
    spyOn(HTMLAnchorElement.prototype, 'click');

    fixture.componentInstance.prepareShipment({ ...order, status: 'Picking' });
    fixture.componentInstance.shipmentQuantities[orderLineId] = 1;
    fixture.componentInstance.submitShipment();

    expect(service.createShipment).toHaveBeenCalledWith('order-1', jasmine.objectContaining({
      carrierName: 'GLS',
      trackingCode: null,
      boxCount: 1,
      items: [{ orderItemId: orderLineId, quantity: 1 }]
    }));
    expect(fixture.componentInstance.successMessage).toContain('DDT-2026-000001');
    expect(fixture.componentInstance.selectedOrder).toBeNull();
    expect(service.getFulfillmentQueue).toHaveBeenCalledTimes(2);
    expect(HTMLAnchorElement.prototype.click).toHaveBeenCalled();
  });
});
