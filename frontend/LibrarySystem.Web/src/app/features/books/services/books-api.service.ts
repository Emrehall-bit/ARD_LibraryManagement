import { HttpClient, HttpParams } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';

import { environment } from '../../../../environments/environment';
import { Book } from '../models/book.model';
import { CreateBookRequest } from '../models/create-book-request.model';
import { PagedBooksResponse } from '../models/paged-books-response.model';
import { UpdateBookRequest } from '../models/update-book-request.model';

export type BookSortBy = 'name' | 'author' | 'stock';
export type BookSortDirection = 'asc' | 'desc';

export interface GetBooksQuery {
  page?: number;
  pageSize?: number;
  search?: string;
  sortBy?: BookSortBy;
  sortDirection?: BookSortDirection;
}

@Injectable({
  providedIn: 'root'
})
export class BooksApiService {
  private readonly http = inject(HttpClient);
  private readonly apiUrl = `${environment.apiBaseUrl}/api/books`;

  getAll(query: GetBooksQuery = {}): Observable<PagedBooksResponse> {
    const page = query.page ?? 1;
    const pageSize = query.pageSize ?? 20;
    const sortBy = query.sortBy ?? 'name';
    const sortDirection = query.sortDirection ?? 'asc';
    let params = new HttpParams()
      .set('page', page)
      .set('pageSize', pageSize)
      .set('sortBy', sortBy)
      .set('sortDirection', sortDirection);
    const trimmedSearch = query.search?.trim();

    if (trimmedSearch) {
      params = params.set('search', trimmedSearch);
    }

    return this.http.get<PagedBooksResponse>(this.apiUrl, { params });
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
