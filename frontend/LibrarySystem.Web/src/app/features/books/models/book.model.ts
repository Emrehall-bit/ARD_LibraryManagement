export interface Book {
  id: string;
  name: string;
  author: string;
  stock: number;
  category: BookCategory;
  coverImageUrl: string | null;
}

export type BookCategory =
  | 'Novel'
  | 'ScienceFiction'
  | 'Fantasy'
  | 'Mystery'
  | 'Adventure'
  | 'Action'
  | 'HorrorThriller'
  | 'History'
  | 'Biography'
  | 'PersonalDevelopment'
  | 'Psychology'
  | 'Philosophy'
  | 'Science'
  | 'Children'
  | 'YoungAdult'
  | 'Poetry'
  | 'Other';
