import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';

import { environment } from '../../../../environments/environment';
import { Book } from '../models/book.model';
import { CreateBookRequest } from '../models/create-book-request.model';
import { UpdateBookRequest } from '../models/update-book-request.model';

@Injectable({
  providedIn: 'root'
})
export class BooksApiService {
  private readonly http = inject(HttpClient);
  private readonly apiUrl = `${environment.apiBaseUrl}/api/books`;

  getAll(): Observable<Book[]> {
    return this.http.get<Book[]>(this.apiUrl);
  }

  getById(id: string): Observable<Book> {
    return this.http.get<Book>(`${this.apiUrl}/${id}`);
  }

  create(request: CreateBookRequest): Observable<Book> {
    return this.http.post<Book>(this.apiUrl, request);
  }

  update(id: string, request: UpdateBookRequest): Observable<Book> {
    return this.http.put<Book>(`${this.apiUrl}/${id}`, request);
  }

  delete(id: string): Observable<void> {
    return this.http.delete<void>(`${this.apiUrl}/${id}`);
  }
}
