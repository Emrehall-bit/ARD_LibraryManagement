export interface AdminUser {
  id: string;
  username: string;
  email: string;
  roles: string[];
  activeBorrowCount: number;
  overdueBorrowCount: number;
}
