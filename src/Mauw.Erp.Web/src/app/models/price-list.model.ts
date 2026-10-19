export type PriceCalculationType = 'FixedPrice' | 'PercentageDiscount' | 'CostMarkup';

export interface PriceRule {
  id?: string;
  calculationType: PriceCalculationType;
  value: number;
  variantId?: string | null;
  productId?: string | null;
  categoryId?: string | null;
  minimumQuantity: number;
  validFrom?: string | null;
  validTo?: string | null;
  priority: number;
}

export interface PriceListRequest {
  name: string;
  currency: 'EUR';
  isActive: boolean;
  rules: PriceRule[];
}

export interface PriceList extends PriceListRequest {
  id: string;
}

export interface PriceResolutionRequest {
  contactId?: string | null;
  priceListId?: string | null;
  at: string;
  lines: { variantId: string; quantity: number }[];
}

export interface ResolvedPrice {
  variantId: string;
  priceListId: string | null;
  ruleId: string | null;
  listUnitPrice: number;
  discountPercent: number;
  agreedUnitNetPrice: number;
  vatRate: number;
}

export interface PriceResolution {
  priceListId: string | null;
  lines: ResolvedPrice[];
}

export interface ItemPricingRequest {
  sourceOrderItemId?: string | null;
  priceListId?: string | null;
  overrideReason?: string | null;
  isCustomerReturn?: boolean;
  sourceLineId?: string | null;
  unitNetPrice?: number;
  unitGrossPrice?: number;
  discountPercent?: number;
}
