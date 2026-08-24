import { BookCategory } from './models/book.model';

export interface BookCategoryOption {
  label: string;
  value: BookCategory;
}

export const BOOK_CATEGORY_OPTIONS: BookCategoryOption[] = [
  { label: 'Roman', value: 'Novel' },
  { label: 'Bilim Kurgu', value: 'ScienceFiction' },
  { label: 'Fantastik', value: 'Fantasy' },
  { label: 'Polisiye / Gizem', value: 'Mystery' },
  { label: 'Macera', value: 'Adventure' },
  { label: 'Aksiyon', value: 'Action' },
  { label: 'Korku / Gerilim', value: 'HorrorThriller' },
  { label: 'Tarih', value: 'History' },
  { label: 'Biyografi', value: 'Biography' },
  { label: 'Kişisel Gelişim', value: 'PersonalDevelopment' },
  { label: 'Psikoloji', value: 'Psychology' },
  { label: 'Felsefe', value: 'Philosophy' },
  { label: 'Bilim', value: 'Science' },
  { label: 'Çocuk', value: 'Children' },
  { label: 'Gençlik', value: 'YoungAdult' },
  { label: 'Şiir', value: 'Poetry' },
  { label: 'Diğer', value: 'Other' }
];

export function getBookCategoryLabel(category: BookCategory | null | undefined): string {
  return BOOK_CATEGORY_OPTIONS.find((option) => option.value === category)?.label ?? 'Diğer';
}
