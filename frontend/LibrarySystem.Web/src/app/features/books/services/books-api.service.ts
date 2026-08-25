import { HttpClient, HttpParams } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';

import { environment } from '../../../../environments/environment';
import { BookDetail, BookImage } from '../models/book-detail.model';
import { Book, BookCategory } from '../models/book.model';
import { CreateBookRequest } from '../models/create-book-request.model';
import { PagedBooksResponse } from '../models/paged-books-response.model';
import { UpdateBookRequest } from '../models/update-book-request.model';

export type BookSortBy = 'name' | 'author' | 'stock';
export type BookSortDirection = 'asc' | 'desc';
export type BookStockStatus = 'all' | 'inStock' | 'outOfStock';

export interface GetBooksQuery {
  page?: number;
  pageSize?: number;
  search?: string;
  sortBy?: BookSortBy;
  sortDirection?: BookSortDirection;
  stockStatus?: BookStockStatus;
  category?: BookCategory | null;
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
    const stockStatus = query.stockStatus ?? 'all';
    let params = new HttpParams()
      .set('page', page)
      .set('pageSize', pageSize)
      .set('sortBy', sortBy)
      .set('sortDirection', sortDirection)
      .set('stockStatus', stockStatus);
    const trimmedSearch = query.search?.trim();

    if (trimmedSearch) {
      params = params.set('search', trimmedSearch);
    }

    if (query.category) {
      params = params.set('category', query.category);
    }

    return this.http.get<PagedBooksResponse>(this.apiUrl, { params });
  }

  getById(id: string): Observable<BookDetail> {
    return this.http.get<BookDetail>(`${this.apiUrl}/${id}`);
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

  uploadImage(bookId: string, file: File, isCover: boolean, sortOrder: number): Observable<BookImage> {
    const formData = new FormData();
    formData.append('file', file);
    formData.append('isCover', String(isCover));
    formData.append('sortOrder', String(sortOrder));

    return this.http.post<BookImage>(`${this.apiUrl}/${bookId}/images`, formData);
  }

  setCover(bookId: string, imageId: string): Observable<BookImage> {
    return this.http.put<BookImage>(`${this.apiUrl}/${bookId}/images/${imageId}/cover`, {});
  }

  deleteImage(bookId: string, imageId: string): Observable<void> {
    return this.http.delete<void>(`${this.apiUrl}/${bookId}/images/${imageId}`);
  }
}
