export const PRICE_FORMULA_MAX_STEPS = 20;
export const PRICE_FORMULA_DEFAULT_ROUNDING_STEP = 0.10;

export interface PriceFormulaStep {
  label: string;
  percentage: number;
}

export interface PriceFormula {
  isConfigured: boolean;
  steps: PriceFormulaStep[];
  roundingStep: number;
  updatedAt: string | null;
  updatedByUserId: string | null;
}

export interface SavePriceFormulaRequest {
  steps: PriceFormulaStep[];
  roundingStep: number;
}

export interface PriceFormulaCalculationStep extends PriceFormulaStep {
  runningValue: number;
}

export interface PriceFormulaCalculation {
  purchaseCost: number;
  steps: PriceFormulaCalculationStep[];
  unroundedPrice: number;
  roundingStep: number;
  finalPrice: number;
}
