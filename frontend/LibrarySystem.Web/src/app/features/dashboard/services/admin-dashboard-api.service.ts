import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';

import { environment } from '../../../../environments/environment';
import { AdminDashboardSummary } from '../models/admin-dashboard-summary.model';

@Injectable({
  providedIn: 'root'
})
export class AdminDashboardApiService {
  private readonly http = inject(HttpClient);
  private readonly apiUrl = `${environment.apiBaseUrl}/api/admin/dashboard`;

  getSummary(): Observable<AdminDashboardSummary> {
    return this.http.get<AdminDashboardSummary>(this.apiUrl);
  }
}
