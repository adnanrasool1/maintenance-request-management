import { mockApiInterceptor } from '../app/core/mock-api.interceptor';

// Development build (`npm start`, which swaps this file in via angular.json).
// The one switch: true answers /api calls from the in-browser mock backend;
// false sends them to the real API through proxy.conf.json.
const useMocks = false;

export const environment = {
  useMocks,
  mockInterceptors: useMocks ? [mockApiInterceptor] : [],
};
