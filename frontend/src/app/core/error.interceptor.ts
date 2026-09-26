import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { catchError, throwError } from 'rxjs';
import { API_PREFIX, LOGIN_URL } from './api.service';
import { AuthService } from './auth.service';

/**
 * A 401 from any `/api` call except login means the token is missing, invalid or expired:
 * clear the session and go to the login page. A failed login's 401 is left to the login page.
 * The error is always rethrown so callers can still react to it.
 */
export const errorInterceptor: HttpInterceptorFn = (req, next) => {
  const auth = inject(AuthService);
  const router = inject(Router);
  return next(req).pipe(
    catchError((error: unknown) => {
      if (
        error instanceof HttpErrorResponse &&
        error.status === 401 &&
        req.url.startsWith(API_PREFIX) &&
        req.url !== LOGIN_URL
      ) {
        auth.logout();
        void router.navigate(['/login']);
      }
      return throwError(() => error);
    }),
  );
};
