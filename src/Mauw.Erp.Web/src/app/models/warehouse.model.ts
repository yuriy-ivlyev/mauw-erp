export enum WarehouseType {
  Standard = 1,
  Defective = 2,
  Consignment = 3,
  Subcontractor = 4,
  Transit = 5
}

export const WAREHOUSE_TYPE_LABELS: Record<WarehouseType, string> = {
  [WarehouseType.Standard]:     'Standard',
  [WarehouseType.Defective]:    'Fallati',
  [WarehouseType.Consignment]:  'Conto Vendita',
  [WarehouseType.Subcontractor]:'Terzisti',
  [WarehouseType.Transit]:      'Transito',
};

export interface WarehouseSummary {
  id: string;
  code: string;
  name: string;
  typeValue: number;
  typeLabel: string;
  isActive: boolean;
  isDefault: boolean;
  isSellable: boolean;
  siteName: string | null;
  city: string;
}

export interface WarehouseDetail {
  id: string;
  code: string;
  name: string;
  typeValue: number;
  typeLabel: string;
  isActive: boolean;
  isDefault: boolean;
  isSellable: boolean;
  contactAddressId: string;
  siteName: string | null;
  street: string;
  zipCode: string;
  city: string;
  province: string | null;
  countryCode: string;
  description: string | null;
  createdAt: string;
  updatedAt: string;
}

export interface WarehouseFilterParams {
  search?: string;
  type?: number;
  isActive?: boolean;
  page: number;
  pageSize: number;
}

export interface CreateWarehouseRequest {
  code: string;
  name: string;
  type: number;
  contactAddressId: string;
  isSellable: boolean;
  isDefault: boolean;
  description?: string | null;
}

export interface UpdateWarehouseRequest {
  name: string;
  type: number;
  contactAddressId: string;
  isSellable: boolean;
  isActive: boolean;
  description?: string | null;
}

export interface PagedResult<T> {
  items: T[];
  pageNumber: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
}
