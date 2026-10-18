export type AddressPurpose = 'LegalHeadquarters' | 'OperationalSite' | 'WarehouseSite' | 'Administrative';

export interface CompanyAddressSite {
  id: string;
  purpose: AddressPurpose;
  siteName: string | null;
  street: string;
  zipCode: string;
  city: string;
  province: string | null;
  countryCode: string;
  isMainOperationalSite: boolean;
  phone: string | null;
  email: string | null;
  accessNotes: string | null;
  linkedWarehouseId: string | null;
}

export interface CompanySettings {
  id: string;
  businessName: string;
  vatNumber: string | null;
  taxCode: string | null;
  sdiCode: string | null;
  pecEmail: string | null;
  countryCode: string;
  fiscalRegime: string | null;
  reaNumber: string | null;
  reaOffice: string | null;
  shareCapital: number | null;
  isShareCapitalFullyPaid: boolean;
  bankName: string | null;
  iban: string | null;
  bicSwift: string | null;
  invoiceNotes: string | null;
  isSoleShareholder: boolean | null;
  isInLiquidation: boolean;
  logoUrl: string | null;
  legalHeadquarters: CompanyAddressSite | null;
  operationalSites: CompanyAddressSite[];
}

export interface UpdateCompanySettingsRequest {
  businessName: string;
  vatNumber?: string | null;
  taxCode?: string | null;
  sdiCode?: string | null;
  pecEmail?: string | null;
  countryCode?: string;
  fiscalRegime?: string | null;
  reaNumber?: string | null;
  reaOffice?: string | null;
  shareCapital?: number | null;
  isShareCapitalFullyPaid?: boolean;
  bankName?: string | null;
  iban?: string | null;
  bicSwift?: string | null;
  invoiceNotes?: string | null;
  isSoleShareholder?: boolean | null;
  isInLiquidation?: boolean;
  logoUrl?: string | null;
  legalHeadquarters: CompanyAddressSiteRequest;
}

export interface CompanyAddressSiteRequest {
  purpose?: string;
  siteName?: string | null;
  street: string;
  zipCode: string;
  city: string;
  province?: string | null;
  countryCode?: string;
  isMainOperationalSite?: boolean;
  phone?: string | null;
  email?: string | null;
  accessNotes?: string | null;
}
