// Limits mirrored from docs/api-contract.md. The server remains the authority.
export const MAX_COST = 1_000_000;
export const MAX_TEXT_LENGTH = 2000;

/** True when `value` has at most two decimal places (money rule, contract §1). */
export function hasAtMostTwoDecimals(value: number): boolean {
  return Math.round(value * 100) / 100 === value;
}
