import { BookCategory } from './book.model';

export interface BookImage {
  id: string;
  url: string;
  isCover: boolean;
  sortOrder: number;
}

export interface BookDetail {
  id: string;
  name: string;
  author: string;
  stock: number;
  category: BookCategory;
  description: string | null;
  isbn: string | null;
  publisher: string | null;
  publishedYear: number | null;
  images: BookImage[];
}
