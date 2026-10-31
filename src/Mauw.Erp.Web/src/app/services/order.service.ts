import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';
import {
  Order,
  OrderActionResponse,
  CreateOrderItemRequest,
  AddCompletedOrderItemRequest,
  CreateOrderRequest,
  OrderItem,
  OrderFilterParams,
  DraftOrderPreview,
  OrderPermittedAction,
  UpdateOrderShippingChargeRequest,
  UpdateOrderAdjustmentLinesRequest,
  PagedResult,
  CreateShipmentRequest,
  OrderFulfillmentResult
} from '../models/order.model';

@Injectable({ providedIn: 'root' })
export class OrderService {
  private readonly base = `${environment.apiUrl}/v1/orders`;

  constructor(private http: HttpClient) {}

  getList(params: OrderFilterParams = {}): Observable<PagedResult<Order>> {
    let httpParams = new HttpParams();
    if (params.search)     httpParams = httpParams.set('search', params.search);
    if (params.status)     httpParams = httpParams.set('status', params.status);
    if (params.orderType)  httpParams = httpParams.set('orderType', params.orderType);
    if (params.sortBy)     httpParams = httpParams.set('sortBy', params.sortBy);
    if (params.sortDescending) httpParams = httpParams.set('sortDescending', 'true');
    httpParams = httpParams
      .set('page', String(params.page ?? 1))
      .set('pageSize', String(params.pageSize ?? 20));
    return this.http.get<PagedResult<Order>>(this.base, { params: httpParams });
  }

  getById(id: string): Observable<Order> {
    return this.http.get<Order>(`${this.base}/${id}`);
  }

  getFulfillmentQueue(page = 1, pageSize = 50): Observable<PagedResult<Order>> {
    const params = new HttpParams()
      .set('page', String(page))
      .set('pageSize', String(pageSize));
    return this.http.get<PagedResult<Order>>(`${this.base}/fulfillment-queue`, { params });
  }

  createShipment(orderId: string, request: CreateShipmentRequest): Observable<OrderFulfillmentResult> {
    return this.http.post<OrderFulfillmentResult>(`${this.base}/${orderId}/ship`, request);
  }

  createOrder(request: CreateOrderRequest): Observable<Order> {
    return this.http.post<Order>(this.base, request);
  }

  addItem(orderId: string, request: CreateOrderItemRequest): Observable<OrderItem> {
    return this.http.post<OrderItem>(`${this.base}/${orderId}/items`, request);
  }

  removeItem(orderId: string, itemId: string): Observable<Order> {
    return this.http.delete<Order>(`${this.base}/${orderId}/items/${itemId}`);
  }

  addBillingItem(orderId: string, request: AddCompletedOrderItemRequest): Observable<OrderItem> {
    return this.http.post<OrderItem>(`${this.base}/${orderId}/invoice-items`, request);
  }

  removeBillingItem(orderId: string, itemId: string): Observable<Order> {
    return this.http.delete<Order>(`${this.base}/${orderId}/invoice-items/${itemId}`);
  }

  updateCompletedInvoiceShippingCharge(orderId: string, request: UpdateOrderShippingChargeRequest): Observable<Order> {
    return this.http.put<Order>(`${this.base}/${orderId}/invoice-shipping-cost`, request);
  }

  updateAdjustmentLines(orderId: string, request: UpdateOrderAdjustmentLinesRequest): Observable<Order> {
    return this.http.put<Order>(`${this.base}/${orderId}/adjustment-lines`, request);
  }

  previewDraftItems(
    items: CreateOrderItemRequest[],
    shippingCost = 0,
    shippingVatRate: number | null = null,
    shippingNatureCode: string | null = null
  ): Observable<DraftOrderPreview> {
    return this.http.post<DraftOrderPreview>(`${environment.apiUrl}/v1/cassa/sessions/preview`, {
      items: items.map(item => ({ ...item, maxQuantity: item.quantity })),
      shippingCost,
      shippingVatRate,
      shippingNatureCode
    });
  }

  updateShippingCharge(orderId: string, request: UpdateOrderShippingChargeRequest): Observable<Order> {
    return this.http.put<Order>(`${this.base}/${orderId}/shipping-cost`, request);
  }

  postAction(action: OrderPermittedAction, payload?: unknown): Observable<OrderActionResponse> {
    const endpoint = `${environment.apiUrl}${action.endpoint.startsWith('/api')
      ? action.endpoint.slice('/api'.length)
      : action.endpoint}`;
    return this.http.post<OrderActionResponse>(endpoint, payload);
  }

  updateItem(orderId: string, itemId: string, payload: {
    variantId: string;
    quantity: number;
    unitNetPrice: number;
    vatRate: number;
    natureCode: string | null;
    discountPercent: number;
  }): Observable<Order> {
    return this.http.put<Order>(`${this.base}/${orderId}/items/${itemId}`, payload);
  }

  downloadOrderPdf(id: string): Observable<Blob> {
    return this.http.get(`${this.base}/${id}/pdf`, { responseType: 'blob' });
  }

  downloadInvoicePdf(invoiceId: string): Observable<Blob> {
    return this.http.get(`${environment.apiUrl}/v1/invoices/${invoiceId}/pdf`, { responseType: 'blob' });
  }

  downloadDdtPdf(ddtId: string): Observable<Blob> {
    return this.http.get(`${environment.apiUrl}/v1/ddts/${ddtId}/pdf`, { responseType: 'blob' });
  }
}
