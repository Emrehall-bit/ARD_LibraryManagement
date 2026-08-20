export interface BorrowedBook {
  id: string;
  bookId: string;
  bookName: string | null;
  author: string | null;
  borrowedAt: string;
  returnedAt: string | null;
}
