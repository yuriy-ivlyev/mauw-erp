export type ValuationMethod = 'LastPurchaseCost' | 'WeightedAverageCost';
export type InventoryStockCostSource = 'StockMovements' | 'PurchaseCost';

export interface InventoryStock {
  warehouseId: string;
  warehouseCode: string;
  warehouseName: string;
  productVariantId: string;
  sku: string;
  barcode: string;
  description: string;
  color: string | null;
  size: string | null;
  category: string | null;
  quantityOnHand: number;
  quantityReserved: number;
  quantityIncoming: number;
  quantityAvailable: number;
  unitOfMeasure: string;
  unitCost: number | null;
  totalValue: number | null;
  costSource: InventoryStockCostSource;
}

export interface InventoryStockTotals {
  totalPhysicalQuantity: number;
  totalReservedQuantity: number;
  totalIncomingQuantity: number;
  totalAvailableQuantity: number;
  totalValue: number;
}

export interface InventoryStocksPage {
  items: InventoryStock[];
  page: number;
  pageSize: number;
  generatedAtUtc: string;
  atDate: string | null;
  allocationNote: string | null;
}

export interface InventoryStocksSummary {
  totalCount: number;
  pageSize: number;
  totalPages: number;
  totals: InventoryStockTotals;
}

export interface InventoryStockMovement {
  id: string;
  movementDateUtc: string;
  movementType: string;
  reason: string | null;
  quantity: number;
  quantityDelta: number | null;
  unitCost: number;
  documentReference: string | null;
  notes: string | null;
  runningBalance: number | null;
}

export interface InventoryStockMovementsResponse {
  items: InventoryStockMovement[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
}

export interface InventoryStockFilters {
  search?: string;
  warehouseId?: string;
  categoryId?: string;
  atDate?: string;
  onlyInStock: boolean;
  valuationMethod: ValuationMethod;
  page: number;
  pageSize: number;
  sortBy: string;
  sortDescending: boolean;
}
