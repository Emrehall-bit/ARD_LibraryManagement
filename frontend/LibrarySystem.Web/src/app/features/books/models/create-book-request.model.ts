import { BookCategory } from './book.model';

export interface CreateBookRequest {
  name: string;
  author: string;
  stock: number;
  category: BookCategory;
}
