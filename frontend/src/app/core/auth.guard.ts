import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { RequestDetail, User } from '../shared/api-models';
import { AuthService } from './auth.service';

/** Lets the route through only with a session that hasn't expired; otherwise goes to login. */
export const authGuard: CanActivateFn = () => {
  const auth = inject(AuthService);
  return auth.hasValidSession() ? true : inject(Router).createUrlTree(['/login']);
};

/**
 * Whether to show Approve / Reject: the user is an Approver, the request is pending, and the
 * user didn't raise it. UX only; the server enforces all three rules (contract §3.9).
 */
export function canDecide(user: User | null, request: RequestDetail | null): boolean {
  return (
    user !== null &&
    request !== null &&
    user.role === 'Approver' &&
    request.status === 'PendingApproval' &&
    request.raisedByUserId.toLowerCase() !== user.id.toLowerCase()
  );
}
