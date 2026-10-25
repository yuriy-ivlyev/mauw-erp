import { fakeAsync, tick, TestBed } from '@angular/core/testing';
import { HttpErrorResponse } from '@angular/common/http';
import { Subject, of, throwError } from 'rxjs';
import { AuthService } from '../../../services/auth.service';
import { InventoryStockService } from '../../../services/inventory-stock.service';
import { ProductService } from '../../../services/product.service';
import { WarehouseService } from '../../../services/warehouse.service';
import {
  InventoryStock,
  InventoryStockMovementsResponse,
  InventoryStocksPage,
  InventoryStocksSummary
} from '../../../models/inventory-stock.model';
import { InventarioComponent } from './inventario.component';

describe('InventarioComponent', () => {
  const authWithRoles = (roles: string[]) =>
    ({
      hasRole: (role: string) => roles.includes(role),
      canSeeCosts: () => roles.includes('Admin') || roles.includes('Accounting')
    }) as unknown as AuthService;
  let inventoryService: jasmine.SpyObj<InventoryStockService>;
  let warehouseService: jasmine.SpyObj<WarehouseService>;
  let productService: jasmine.SpyObj<ProductService>;
  let component: InventarioComponent;

  const stock: InventoryStock = {
    warehouseId: 'warehouse-1',
    warehouseCode: 'DEP-1',
    warehouseName: 'Deposito',
    productVariantId: 'variant-1',
    sku: 'SKU-1',
    barcode: 'BAR-1',
    description: 'Articolo',
    color: null,
    size: null,
    category: 'Abbigliamento',
    quantityOnHand: 5,
    quantityReserved: 0,
    quantityIncoming: 0,
    quantityAvailable: 5,
    unitOfMeasure: 'pz',
    unitCost: 10,
    totalValue: 50,
    costSource: 'StockMovements'
  };

  const stockPage: InventoryStocksPage = {
    items: [stock],
    page: 1,
    pageSize: 20,
    generatedAtUtc: '2026-10-24T12:00:00Z',
    atDate: null,
    allocationNote: null
  };

  const stockSummary: InventoryStocksSummary = {
    totalCount: 1,
    pageSize: 20,
    totalPages: 1,
    totals: {
      totalPhysicalQuantity: 5,
      totalReservedQuantity: 0,
      totalIncomingQuantity: 0,
      totalAvailableQuantity: 5,
      totalValue: 50
    }
  };

  const movementsResponse: InventoryStockMovementsResponse = {
    items: [{
      id: 'movement-1',
      movementDateUtc: '2026-10-24T12:00:00Z',
      movementType: 'Carico fornitore',
      reason: 'Carico Fornitore',
      quantity: 5,
      quantityDelta: 5,
      unitCost: 10,
      documentReference: 'DDT-1',
      notes: null,
      runningBalance: 5
    }],
    page: 1,
    pageSize: 20,
    totalCount: 1,
    totalPages: 1
  };

  beforeEach(() => {
    inventoryService = jasmine.createSpyObj<InventoryStockService>(
      'InventoryStockService',
      ['getStocks', 'getSummary', 'getMovements', 'exportCsv', 'exportPdf']
    );
    warehouseService = jasmine.createSpyObj<WarehouseService>('WarehouseService', ['getList']);
    productService = jasmine.createSpyObj<ProductService>('ProductService', ['getCategories']);
    inventoryService.getStocks.and.returnValue(of(stockPage));
    inventoryService.getSummary.and.returnValue(of(stockSummary));
    inventoryService.getMovements.and.returnValue(of(movementsResponse));
    warehouseService.getList.and.returnValue(of({ items: [] }) as never);
    productService.getCategories.and.returnValue(of([]));
    component = new InventarioComponent(inventoryService, warehouseService, productService, authWithRoles(['Admin']));
  });

  afterEach(() => component.ngOnDestroy());

  it('keeps total skeletons visible until the summary response arrives', () => {
    const stocksResponse = new Subject<InventoryStocksPage>();
    const summaryResponse = new Subject<InventoryStocksSummary>();
    inventoryService.getStocks.and.returnValue(stocksResponse);
    inventoryService.getSummary.and.returnValue(summaryResponse);

    component.ngOnInit();
    expect(component.hasLoadedStocks()).toBeFalse();
    expect(component.hasLoadedSummary()).toBeFalse();

    stocksResponse.next(stockPage);
    expect(component.hasLoadedStocks()).toBeTrue();
    expect(component.hasLoadedSummary()).toBeFalse();
    expect(component.totals().totalPhysicalQuantity).toBe(0);

    summaryResponse.next(stockSummary);
    expect(component.hasLoadedSummary()).toBeTrue();
    expect(component.totals()).toEqual(stockSummary.totals);
    expect(component.totalCount()).toBe(1);
    expect(component.totalPages()).toBe(1);
  });

  it('shows the API detail when historical stock cannot be reconstructed', () => {
    const detail = 'Impossibile ricostruire la giacenza alla data richiesta: rettifica storica senza direzione.';
    inventoryService.getStocks.and.returnValue(throwError(() => new HttpErrorResponse({
      status: 409,
      error: detail
    })));

    component.ngOnInit();

    expect(component.errorMessage()).toBe(detail);
    expect(component.hasLoadedStocks()).toBeFalse();
  });

  it('shows the API detail when the inventory summary request fails', () => {
    const detail = 'Riepilogo storico non disponibile.';
    inventoryService.getSummary.and.returnValue(throwError(() => new HttpErrorResponse({
      status: 409,
      error: detail
    })));

    component.ngOnInit();

    expect(component.errorMessage()).toBe(detail);
    expect(component.hasLoadedSummary()).toBeFalse();
  });

  it('debounces search changes before requesting filtered stocks', fakeAsync(() => {
    component.ngOnInit();
    expect(inventoryService.getStocks).toHaveBeenCalledTimes(1);
    expect(inventoryService.getSummary).toHaveBeenCalledTimes(1);

    component.search.set('maglia');
    component.onSearchChange();
    tick(299);
    expect(inventoryService.getStocks).toHaveBeenCalledTimes(1);
    expect(inventoryService.getSummary).toHaveBeenCalledTimes(1);

    tick(1);
    expect(inventoryService.getStocks).toHaveBeenCalledTimes(2);
    expect(inventoryService.getSummary).toHaveBeenCalledTimes(2);
    expect(inventoryService.getStocks.calls.mostRecent().args[0].search).toBe('maglia');
    expect(inventoryService.getSummary.calls.mostRecent().args[0].search).toBe('maglia');
  }));

  it('requests summaries for filter changes but not for pagination or sorting', () => {
    component.ngOnInit();
    component.onFilterChange();
    expect(inventoryService.getSummary).toHaveBeenCalledTimes(2);

    component.totalPages.set(2);
    component.goToPage(2);
    component.onSortChange({ sortBy: 'sku', sortDescending: true });

    expect(inventoryService.getSummary).toHaveBeenCalledTimes(2);
  });

  it('recalculates page count locally when page size changes without requesting another summary', () => {
    inventoryService.getSummary.and.returnValue(of({
      ...stockSummary,
      totalCount: 41,
      totalPages: 3
    }));
    component.ngOnInit();
    expect(component.totalPages()).toBe(3);
    const summaryCalls = inventoryService.getSummary.calls.count();

    component.onPageSizeChange(10);

    expect(component.totalPages()).toBe(5);
    expect(component.page()).toBe(1);
    expect(inventoryService.getSummary).toHaveBeenCalledTimes(summaryCalls);
  });

  it('exports using the currently selected filters', () => {
    const exportResult = new Subject<Blob>();
    inventoryService.exportCsv.and.returnValue(exportResult);
    component.filterCategoryId.set('category-1');

    component.exportCsv();

    expect(inventoryService.exportCsv).toHaveBeenCalledWith(
      jasmine.objectContaining({ categoryId: 'category-1' })
    );
    expect(component.exporting()).toBe('csv');
    exportResult.complete();
    expect(component.exporting()).toBeNull();
  });

  it('opens the stock ledger and loads its first movement page', () => {
    component.ngOnInit();
    component.openMovements(stock);

    expect(inventoryService.getMovements).toHaveBeenCalledWith(
      'warehouse-1',
      'variant-1',
      undefined,
      1,
      20
    );
    expect(component.selectedStock()).toBe(stock);
    expect(component.movements()).toEqual(movementsResponse.items);
    expect(component.movementTotalCount()).toBe(1);
  });

  describe('historical date filter', () => {
    const dateInput = (fixture: { nativeElement: HTMLElement }) =>
      fixture.nativeElement.querySelector('input[type="date"]');

    function render(roles: string[]) {
      TestBed.resetTestingModule();
      TestBed.configureTestingModule({
        imports: [InventarioComponent],
        providers: [
          { provide: InventoryStockService, useValue: inventoryService },
          { provide: WarehouseService, useValue: warehouseService },
          { provide: ProductService, useValue: productService },
          { provide: AuthService, useValue: authWithRoles(roles) }
        ]
      });
      const fixture = TestBed.createComponent(InventarioComponent);
      fixture.detectChanges();
      return fixture;
    }

    for (const role of ['Admin', 'Accounting']) {
      it(`shows the date control and sends atDate for ${role}`, () => {
        const fixture = render([role]);
        expect(dateInput(fixture)).not.toBeNull();

        fixture.componentInstance.filterAtDate.set('2026-10-20');
        fixture.componentInstance.onFilterChange();
        fixture.componentInstance.openMovements(stock);

        expect(inventoryService.getStocks.calls.mostRecent().args[0].atDate).toBeDefined();
        expect(inventoryService.getSummary.calls.mostRecent().args[0].atDate).toBeDefined();
        expect(inventoryService.getMovements.calls.mostRecent().args[2]).toBeDefined();
      });
    }

    for (const role of ['Sales', 'Warehouse']) {
      it(`hides the date control and never sends atDate for ${role}`, () => {
        const fixture = render([role]);
        expect(dateInput(fixture)).toBeNull();
        expect(fixture.nativeElement.textContent).not.toContain('Giacenza alla data');

        fixture.componentInstance.filterAtDate.set('2026-10-20');
        fixture.componentInstance.onFilterChange();
        fixture.componentInstance.openMovements(stock);

        expect(inventoryService.getStocks.calls.mostRecent().args[0].atDate).toBeUndefined();
        expect(inventoryService.getSummary.calls.mostRecent().args[0].atDate).toBeUndefined();
        expect(inventoryService.getMovements.calls.mostRecent().args[2]).toBeUndefined();
      });
    }
  });

  describe('cost visibility', () => {
    const redactedStock: InventoryStock = { ...stock, unitCost: null, totalValue: null, costSource: null };
    const redactedMovements: InventoryStockMovementsResponse = {
      ...movementsResponse, items: [{ ...movementsResponse.items[0], unitCost: null }]
    };
    const redactedSummary: InventoryStocksSummary = {
      ...stockSummary, totals: { ...stockSummary.totals, totalValue: null }
    };

    function render(roles: string[], redacted: boolean) {
      TestBed.resetTestingModule();
      if (redacted) {
        inventoryService.getStocks.and.returnValue(of({ ...stockPage, items: [redactedStock] }));
        inventoryService.getSummary.and.returnValue(of(redactedSummary));
        inventoryService.getMovements.and.returnValue(of(redactedMovements));
      }
      TestBed.configureTestingModule({
        imports: [InventarioComponent],
        providers: [
          { provide: InventoryStockService, useValue: inventoryService },
          { provide: WarehouseService, useValue: warehouseService },
          { provide: ProductService, useValue: productService },
          { provide: AuthService, useValue: authWithRoles(roles) }
        ]
      });
      const fixture = TestBed.createComponent(InventarioComponent);
      fixture.detectChanges();
      fixture.componentInstance.openMovements(redacted ? redactedStock : stock);
      fixture.detectChanges();
      return fixture;
    }

    for (const role of ['Admin', 'Accounting']) {
      it(`shows costs, valuation, sort headers and the method selector for ${role}`, () => {
        const html: HTMLElement = render([role], false).nativeElement;
        expect(html.textContent).toContain('Valore complessivo');
        expect(html.textContent).toContain('Valore totale');
        expect(html.textContent).toContain('Costo unitario');
        expect(html.textContent).toContain('10.00 €');
        expect(html.textContent).toContain('50.00 €');
        expect(html.querySelector('th[appSortHeader="unitCost"]')).not.toBeNull();
        expect(html.querySelector('th[appSortHeader="totalValue"]')).not.toBeNull();
        expect(html.querySelector('select[aria-label="Metodo di valorizzazione"]')).not.toBeNull();
        expect(html.textContent).toContain('Costo da movimenti di carico');
      });
    }

    for (const role of ['Sales', 'Warehouse']) {
      it(`hides every cost and valuation element and survives null values for ${role}`, () => {
        const html: HTMLElement = render([role], true).nativeElement;
        expect(html.textContent).toContain('SKU-1');
        expect(html.textContent).toContain('DDT-1');
        for (const label of ['Valore complessivo', 'Valore totale', 'Costo unitario', 'Metodo di valorizzazione',
          'Costo medio ponderato', 'Ultimo costo', 'Costo da movimenti', 'Costo da anagrafica']) {
          expect(html.textContent).withContext(`${role}: ${label}`).not.toContain(label);
        }
        expect(html.querySelector('th[appSortHeader="unitCost"]')).toBeNull();
        expect(html.querySelector('th[appSortHeader="totalValue"]')).toBeNull();
        expect(html.querySelector('select[aria-label="Metodo di valorizzazione"]')).toBeNull();
        expect(html.textContent).not.toMatch(/NaN|\bnull\b|€\s*€/);
        expect(html.textContent).not.toContain('€');
      });
    }
  });
});
