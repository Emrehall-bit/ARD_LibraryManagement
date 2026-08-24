export interface AdminOverdueBorrow {
  id: string;
  userId: string;
  username: string;
  bookId: string;
  bookName: string | null;
  author: string | null;
  borrowedAt: string;
  dueDate: string;
  overdueDays: number;
  renewalCount: number;
  status: 'Overdue';
}
