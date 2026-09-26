import { HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { API_PREFIX, LOGIN_URL } from './api.service';
import { AuthService } from './auth.service';

/** Adds `Authorization: Bearer <token>` to `/api` calls, except login (contract §1). */
export const authInterceptor: HttpInterceptorFn = (req, next) => {
  if (!req.url.startsWith(API_PREFIX) || req.url === LOGIN_URL) {
    return next(req);
  }
  const token = inject(AuthService).token();
  return next(token ? req.clone({ setHeaders: { Authorization: `Bearer ${token}` } }) : req);
};
