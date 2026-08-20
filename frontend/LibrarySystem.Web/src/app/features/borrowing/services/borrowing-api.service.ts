import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';

import { environment } from '../../../../environments/environment';
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

  getMyBooks(): Observable<BorrowedBook[]> {
    return this.http.get<BorrowedBook[]>(`${this.apiBaseUrl}/api/borrow/my-books`);
  }
}
