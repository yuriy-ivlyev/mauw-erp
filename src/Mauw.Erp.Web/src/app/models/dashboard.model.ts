export interface DashboardStatistics {
  sales: SalesStatistics;
  contacts: ContactStatistics;
  customerLocations: CustomerLocationStatistics[];
  inventory: InventoryStatistics;
  movements: MovementStatistics;
}

export interface SalesStatistics {
  orderCount: number;
  grossRevenue: number;
  daily: DailySalesStatistics[];
}

export interface DailySalesStatistics {
  date: string;
  orderCount: number;
  grossRevenue: number;
}

export interface ContactStatistics {
  totalContacts: number;
  totalCustomers: number;
  totalSuppliers: number;
  totalAgents: number;
  newThisWeek: number;
  newThisMonth: number;
}

export interface CustomerLocationStatistics {
  city: string;
  province: string | null;
  countryCode: string;
  customerCount: number;
}

export interface InventoryStatistics {
  activeWarehouses: number;
  quantityOnHand: number;
  quantityReserved: number;
  quantityAvailable: number;
}

export interface MovementStatistics {
  totalCount: number;
  inboundCount: number;
  outboundCount: number;
  adjustmentCount: number;
  daily: DailyMovementStatistics[];
}

export interface DailyMovementStatistics {
  date: string;
  inboundCount: number;
  outboundCount: number;
  adjustmentCount: number;
  totalCount: number;
}
