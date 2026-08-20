import { Book } from './book.model';

export interface PagedBooksResponse {
  items: Book[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
}
