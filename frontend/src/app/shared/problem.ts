import { HttpErrorResponse } from '@angular/common/http';
import { ProblemDetails } from './api-models';

/** HTTP status of an error, or 0 when it isn't an HTTP error. */
export function statusOf(error: unknown): number {
  return error instanceof HttpErrorResponse ? error.status : 0;
}

/** The ProblemDetails body of an HTTP error, or null when there is none (contract §4). */
export function problemOf(error: unknown): ProblemDetails | null {
  if (error instanceof HttpErrorResponse && error.error && typeof error.error === 'object') {
    return error.error as ProblemDetails;
  }
  return null;
}
