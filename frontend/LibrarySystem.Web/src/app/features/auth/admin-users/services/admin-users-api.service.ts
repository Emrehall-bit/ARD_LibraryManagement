import { HttpClient, HttpParams } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';

import { environment } from '../../../../../environments/environment';
import { PagedAdminUsers } from '../models/paged-admin-users.model';

@Injectable({
  providedIn: 'root'
})
export class AdminUsersApiService {
  private readonly http = inject(HttpClient);
  private readonly apiUrl = `${environment.apiBaseUrl}/api/admin/users`;

  getAll(page = 1, pageSize = 20, search?: string): Observable<PagedAdminUsers> {
    let params = new HttpParams()
      .set('page', page)
      .set('pageSize', pageSize);
    const trimmedSearch = search?.trim();

    if (trimmedSearch) {
      params = params.set('search', trimmedSearch);
    }

    return this.http.get<PagedAdminUsers>(this.apiUrl, { params });
  }
}
