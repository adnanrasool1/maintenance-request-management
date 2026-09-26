import { AbstractControl, ValidationErrors, ValidatorFn } from '@angular/forms';

// Limits mirrored from docs/api-contract.md. The server remains the authority.
export const MAX_COST = 1_000_000;
export const MAX_TEXT_LENGTH = 2000;

/** True when `value` has at most two decimal places (money rule, contract §1). */
export function hasAtMostTwoDecimals(value: number): boolean {
  return Math.round(value * 100) / 100 === value;
}

/** Fails on a non-empty string that is only whitespace (`Validators.required` covers ''). */
export const notBlank: ValidatorFn = (control: AbstractControl): ValidationErrors | null => {
  const value = control.value;
  return typeof value === 'string' && value.length > 0 && value.trim().length === 0
    ? { blank: true }
    : null;
};

/** Money: > 0, at most 1,000,000.00, at most 2 decimals. Empty is left to `required`. */
export const cost: ValidatorFn = (control: AbstractControl): ValidationErrors | null => {
  const value = control.value;
  if (value === null || value === '') return null;
  if (typeof value !== 'number' || !Number.isFinite(value)) return { number: true };
  if (value <= 0) return { positive: true };
  if (value > MAX_COST) return { maxCost: true };
  if (!hasAtMostTwoDecimals(value)) return { decimals: true };
  return null;
};
