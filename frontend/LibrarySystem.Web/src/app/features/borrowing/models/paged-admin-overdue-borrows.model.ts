import { AdminOverdueBorrow } from './admin-overdue-borrow.model';

export interface PagedAdminOverdueBorrows {
  items: AdminOverdueBorrow[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
}
