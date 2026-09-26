import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import {
  CreateRequest,
  DecisionRequest,
  LoginRequest,
  LoginResponse,
  RequestDetail,
  RequestStatus,
  RequestSummary,
  Site,
} from '../shared/api-models';

/** Relative paths only: nginx (or the dev proxy) serves the app and the API from one origin. */
export const API_PREFIX = '/api/';
export const LOGIN_URL = '/api/auth/login';

/** Thin HttpClient wrapper for the routes the UI uses (docs/api-contract.md §5). */
@Injectable({ providedIn: 'root' })
export class ApiService {
  private readonly http = inject(HttpClient);

  login(body: LoginRequest): Observable<LoginResponse> {
    return this.http.post<LoginResponse>(LOGIN_URL, body);
  }

  getSites(): Observable<Site[]> {
    return this.http.get<Site[]>('/api/sites');
  }

  getRequests(status?: RequestStatus): Observable<RequestSummary[]> {
    const params = status ? new HttpParams().set('status', status) : undefined;
    return this.http.get<RequestSummary[]>('/api/requests', { params });
  }

  getRequest(id: string): Observable<RequestDetail> {
    return this.http.get<RequestDetail>(`/api/requests/${encodeURIComponent(id)}`);
  }

  createRequest(body: CreateRequest): Observable<RequestDetail> {
    return this.http.post<RequestDetail>('/api/requests', body);
  }

  approve(id: string, body: DecisionRequest): Observable<RequestDetail> {
    return this.http.post<RequestDetail>(`/api/requests/${encodeURIComponent(id)}/approve`, body);
  }

  reject(id: string, body: DecisionRequest): Observable<RequestDetail> {
    return this.http.post<RequestDetail>(`/api/requests/${encodeURIComponent(id)}/reject`, body);
  }
}
