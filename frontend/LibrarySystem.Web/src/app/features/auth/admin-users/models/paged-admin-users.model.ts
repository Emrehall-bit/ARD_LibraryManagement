import { AdminUser } from './admin-user.model';

export interface PagedAdminUsers {
  items: AdminUser[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
}
