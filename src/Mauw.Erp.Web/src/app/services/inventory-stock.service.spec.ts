import { HttpClientTestingModule, HttpTestingController } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import {
  InventoryStockFilters,
  InventoryStocksPage,
  InventoryStocksSummary
} from '../models/inventory-stock.model';
import { InventoryStockService } from './inventory-stock.service';

describe('InventoryStockService', () => {
  let service: InventoryStockService;
  let http: HttpTestingController;

  const filters: InventoryStockFilters = {
    search: 'SKU',
    warehouseId: 'warehouse-1',
    categoryId: 'category-1',
    atDate: '2026-10-25T12:00:00Z',
    onlyInStock: true,
    valuationMethod: 'LastPurchaseCost',
    page: 3,
    pageSize: 50,
    sortBy: 'sku',
    sortDescending: true
  };

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [HttpClientTestingModule],
      providers: [InventoryStockService]
    });
    service = TestBed.inject(InventoryStockService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('uses separate page and nested summary response contracts', () => {
    const pageResponse: InventoryStocksPage = {
      items: [],
      page: 3,
      pageSize: 50,
      generatedAtUtc: '2026-10-25T12:00:00Z',
      atDate: filters.atDate!,
      allocationNote: null
    };
    const summaryResponse: InventoryStocksSummary = {
      totalCount: 101,
      pageSize: 50,
      totalPages: 3,
      totals: {
        totalPhysicalQuantity: 10,
        totalReservedQuantity: 2,
        totalIncomingQuantity: 4,
        totalAvailableQuantity: 12,
        totalValue: 250
      }
    };

    let page: InventoryStocksPage | undefined;
    service.getStocks(filters).subscribe(response => page = response);
    const pageRequest = http.expectOne(request =>
      request.url === '/api/v1/inventory/stocks'
    );
    expect(pageRequest.request.method).toBe('GET');
    expect(pageRequest.request.params.get('page')).toBe('3');
    expect(pageRequest.request.params.get('pageSize')).toBe('50');
    pageRequest.flush(pageResponse);
    expect(page).toEqual(pageResponse);

    let summary: InventoryStocksSummary | undefined;
    service.getSummary(filters).subscribe(response => summary = response);
    const summaryRequest = http.expectOne(request =>
      request.url === '/api/v1/inventory/stocks/summary'
    );
    expect(summaryRequest.request.method).toBe('GET');
    expect(summaryRequest.request.params.has('page')).toBeFalse();
    expect(summaryRequest.request.params.get('pageSize')).toBe('50');
    expect(summaryRequest.request.params.get('warehouseId')).toBe('warehouse-1');
    summaryRequest.flush(summaryResponse);
    expect(summary).toEqual(summaryResponse);
  });
});
