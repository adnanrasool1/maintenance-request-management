import { HttpInterceptorFn } from '@angular/common/http';

// Production build (and the web container): always the real API, through nginx.
// This file never imports the mock backend, so it isn't in the production bundle.
export const environment = {
  useMocks: false,
  mockInterceptors: [] as HttpInterceptorFn[],
};
