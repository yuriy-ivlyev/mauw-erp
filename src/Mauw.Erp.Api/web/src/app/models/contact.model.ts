export interface ContactSummary {
  id: string;
  businessName: string;
  vatNumber: string | null;
  taxCode: string | null;
  city: string | null;
  province: string | null;
  status: string;
  isCustomer: boolean;
  isSupplier: boolean;
  isAgent: boolean;
  createdAt: string;
}

export interface ContactDetail {
  id: string;
  businessName: string;
  vatNumber: string | null;
  taxCode: string | null;
  sdiCode: string | null;
  pecEmail: string | null;
  countryCode: string;
  fiscalRegime: string | null;
  status: string;
  isDeleted: boolean;
  createdAt: string;
  updatedAt: string;
  customerProfile: CustomerProfile | null;
  supplierProfile: SupplierProfile | null;
  agentProfile: AgentProfile | null;
  addresses: ContactAddress[];
  persons: ContactPerson[];
}

export interface CustomerProfile {
  priceListId: string | null;
  paymentTermId: string | null;
  financialDiscountRate: number;
  creditLimit: number;
  isBlocked: boolean;
  blockReason: string | null;
  defaultAgentId: string | null;
  deliveryNotes: string | null;
}

export interface SupplierProfile {
  paymentTermId: string | null;
  iban: string | null;
  bicSwift: string | null;
}

export interface AgentProfile {
  defaultCommissionRate: number;
  operatingArea: string | null;
}

export interface ContactAddress {
  id: string;
  type: string;
  street: string;
  zipCode: string;
  city: string;
  province: string | null;
  countryCode: string;
  isDefaultShipping: boolean;
}

export interface ContactPerson {
  id: string;
  firstName: string;
  lastName: string;
  role: string | null;
  email: string | null;
  phone: string | null;
  isPrimary: boolean;
}

export interface ContactFilterParams {
  search?: string;
  isCustomer?: boolean;
  isSupplier?: boolean;
  isAgent?: boolean;
  status?: string;
  sortBy?: string;
  sortDescending?: boolean;
  page?: number;
  pageSize?: number;
}

export interface PagedResult<T> {
  items: T[];
  pageNumber: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
}

export interface CreateContactRequest {
  businessName: string;
  vatNumber?: string | null;
  taxCode?: string | null;
  sdiCode?: string | null;
  pecEmail?: string | null;
  countryCode?: string;
  fiscalRegime?: string | null;
  status?: string;
  customerProfile?: Partial<CustomerProfile> | null;
  supplierProfile?: Partial<SupplierProfile> | null;
  agentProfile?: Partial<AgentProfile> | null;
  addresses?: Partial<ContactAddress>[];
  persons?: Partial<ContactPerson>[];
}

export interface UpdateContactRequest extends Partial<CreateContactRequest> {
  disableCustomer?: boolean;
  disableSupplier?: boolean;
  disableAgent?: boolean;
}

export interface ToggleBlockRequest {
  isBlocked: boolean;
  reason?: string | null;
}
