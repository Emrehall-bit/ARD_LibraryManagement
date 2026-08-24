import { BookCategory } from './book.model';

export interface UpdateBookRequest {
  name: string;
  author: string;
  stock: number;
  category: BookCategory;
}
