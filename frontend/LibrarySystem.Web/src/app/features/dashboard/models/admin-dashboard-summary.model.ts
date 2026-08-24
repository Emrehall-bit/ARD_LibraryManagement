export interface AdminDashboardSummary {
  totalUsers: number;
  totalBooks: number;
  totalStock: number;
  outOfStockBooks: number;
  activeBorrows: number;
  overdueBorrows: number;
  returnedBorrows: number;
  recentOverdueBorrows: AdminRecentOverdueBorrow[];
}

export interface AdminRecentOverdueBorrow {
  id: string;
  userId: string;
  username: string;
  bookId: string;
  bookName: string;
  author: string;
  dueDate: string;
  overdueDays: number;
}
