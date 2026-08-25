import { BorrowedBook } from './borrowed-book.model';

export interface PagedBorrowHistory {
  items: BorrowedBook[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
}
