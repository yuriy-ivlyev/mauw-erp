import { AbstractControl, ValidationErrors, ValidatorFn } from '@angular/forms';

export const guidPattern = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;

export function hasDecimal8Precision(value: unknown): boolean {
  if (value === null || value === '' || !Number.isFinite(Number(value))) return false;
  const match = /^(\d+)(?:\.(\d+))?(?:e([+-]?\d+))?$/i.exec(String(value));
  return !!match && (match[2]?.length ?? 0) - Number(match[3] ?? 0) <= 8;
}

export const decimal8: ValidatorFn = (control: AbstractControl): ValidationErrors | null => {
  return hasDecimal8Precision(control.value) ? null : { decimal8: true };
};

export const priceRuleValidator: ValidatorFn = (control: AbstractControl): ValidationErrors | null => {
  const target = control.get('target')?.value as string;
  const targetId = control.get('targetId')?.value as string;
  const type = control.get('calculationType')?.value as string;
  const value = Number(control.get('value')?.value);
  const from = control.get('validFrom')?.value as string;
  const to = control.get('validTo')?.value as string;
  const errors: ValidationErrors = {};
  if (!['global', 'variantId', 'productId', 'categoryId'].includes(target) ||
    (target !== 'global' && (!guidPattern.test(targetId) || /^0{8}-0{4}-0{4}-0{4}-0{12}$/.test(targetId))) ||
    (target === 'global' && !!targetId)) errors['target'] = true;
  if (!['FixedPrice', 'PercentageDiscount', 'CostMarkup'].includes(type) ||
    (type === 'PercentageDiscount' && value > 100)) errors['calculationType'] = true;
  if ((from && !Number.isFinite(Date.parse(from))) || (to && !Number.isFinite(Date.parse(to))) ||
    (from && to && Date.parse(from) > Date.parse(to))) errors['dateWindow'] = true;
  return Object.keys(errors).length ? errors : null;
};
