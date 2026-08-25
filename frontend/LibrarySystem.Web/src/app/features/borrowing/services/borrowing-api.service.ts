import { HttpClient, HttpParams } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';

import { environment } from '../../../../environments/environment';
import { PagedAdminOverdueBorrows } from '../models/paged-admin-overdue-borrows.model';
import { PagedBorrowHistory } from '../models/paged-borrow-history.model';
import { BorrowedBook } from '../models/borrowed-book.model';

@Injectable({
  providedIn: 'root'
})
export class BorrowingApiService {
  private readonly http = inject(HttpClient);
  private readonly apiBaseUrl = environment.apiBaseUrl;

  borrow(bookId: string): Observable<BorrowedBook> {
    return this.http.post<BorrowedBook>(`${this.apiBaseUrl}/api/borrow/${bookId}`, null);
  }

  returnBook(bookId: string): Observable<BorrowedBook> {
    return this.http.post<BorrowedBook>(`${this.apiBaseUrl}/api/return/${bookId}`, null);
  }

  renew(bookId: string): Observable<BorrowedBook> {
    return this.http.post<BorrowedBook>(`${this.apiBaseUrl}/api/borrow/renew/${bookId}`, null);
  }

  getMyBooks(): Observable<BorrowedBook[]> {
    return this.http.get<BorrowedBook[]>(`${this.apiBaseUrl}/api/borrow/my-books`);
  }

  getHistory(page = 1, pageSize = 20): Observable<PagedBorrowHistory> {
    const params = new HttpParams()
      .set('page', page)
      .set('pageSize', pageSize);

    return this.http.get<PagedBorrowHistory>(`${this.apiBaseUrl}/api/borrow/history`, { params });
  }

  getOverdueBorrows(page = 1, pageSize = 20): Observable<PagedAdminOverdueBorrows> {
    const params = new HttpParams()
      .set('page', page)
      .set('pageSize', pageSize);

    return this.http.get<PagedAdminOverdueBorrows>(`${this.apiBaseUrl}/api/borrow/overdue`, { params });
  }
}
