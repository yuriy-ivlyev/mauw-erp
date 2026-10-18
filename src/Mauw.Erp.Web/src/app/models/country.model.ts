export interface CountryDto {
  isoCode: string;
  name: string;
  isEuMember: boolean;
  displayOrder: number;
}

export interface VatValidationResult {
  isFormatValid: boolean;
  isViesValid: boolean | null;
  isViesAvailable: boolean;
  companyName: string | null;
  companyAddress: string | null;
  errorMessage: string | null;
}
