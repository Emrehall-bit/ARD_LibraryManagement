export type BorrowStatus = 'Borrowed' | 'Returned' | 'Overdue';

export interface BorrowedBook {
  id: string;
  bookId: string;
  bookName: string | null;
  author: string | null;
  borrowedAt: string;
  dueDate: string;
  returnedAt: string | null;
  status: BorrowStatus;
  renewalCount: number;
}
