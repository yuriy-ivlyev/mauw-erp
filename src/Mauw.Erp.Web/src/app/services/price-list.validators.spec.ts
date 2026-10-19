import { FormControl, FormGroup } from '@angular/forms';
import { decimal8, priceRuleValidator } from './price-list.validators';

describe('Price list rule validation', () => {
  function rule() {
    return new FormGroup({
      target: new FormControl('global'), targetId: new FormControl(''),
      calculationType: new FormControl('FixedPrice'), value: new FormControl(0),
      validFrom: new FormControl(''), validTo: new FormControl('')
    }, { validators: priceRuleValidator });
  }
  it('accepts zero and eight decimals, including numeric scientific notation', () => {
    for (const value of [0, 1, 12.12345678, 0.00000001, '23.00000000']) {
      expect(decimal8(new FormControl(value))).withContext(String(value)).toBeNull();
    }
  });
  it('rejects negatives, missing values, non-finite and ninth decimals', () => {
    for (const value of [-1, '', null, Infinity, NaN, 0.000000001, '2.123456789']) {
      expect(decimal8(new FormControl(value))).withContext(String(value)).not.toBeNull();
    }
  });
  it('requires a non-empty valid GUID for a specific target and none for global', () => {
    const form = rule();
    expect(form.valid).toBeTrue();
    form.patchValue({ target: 'variantId' });
    expect(form.hasError('target')).toBeTrue();
    form.patchValue({ targetId: '12345678-1234-1234-1234-123456789012' });
    expect(form.valid).toBeTrue();
    form.patchValue({ target: 'global' });
    expect(form.hasError('target')).toBeTrue();
  });
  it('limits percentage discount but permits cost markup above 100 percent', () => {
    const form = rule();
    form.patchValue({ calculationType: 'PercentageDiscount', value: 101 });
    expect(form.hasError('calculationType')).toBeTrue();
    form.patchValue({ value: 100 });
    expect(form.valid).toBeTrue();
    form.patchValue({ calculationType: 'CostMarkup', value: 150 });
    expect(form.valid).toBeTrue();
  });
  it('validates inclusive windows and rejects invalid or reversed dates', () => {
    const form = rule();
    form.patchValue({ validFrom: '2026-10-18T10:00', validTo: '2026-10-18T10:00' });
    expect(form.valid).toBeTrue();
    form.patchValue({ validTo: '2026-10-18T09:59' });
    expect(form.hasError('dateWindow')).toBeTrue();
    form.patchValue({ validTo: 'invalid' });
    expect(form.hasError('dateWindow')).toBeTrue();
  });
});
