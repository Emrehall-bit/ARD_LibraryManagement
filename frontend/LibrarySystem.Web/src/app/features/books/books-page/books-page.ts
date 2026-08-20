import { Component, computed, inject, OnInit, signal } from '@angular/core';
import { CardModule } from 'primeng/card';
import { InputTextModule } from 'primeng/inputtext';
import { MessageModule } from 'primeng/message';
import { ProgressSpinnerModule } from 'primeng/progressspinner';
import { TagModule } from 'primeng/tag';
import { finalize } from 'rxjs';

import { Book } from '../models/book.model';
import { BooksApiService } from '../services/books-api.service';

type StockSeverity = 'success' | 'danger';

@Component({
  selector: 'app-books-page',
  imports: [
    CardModule,
    InputTextModule,
    MessageModule,
    ProgressSpinnerModule,
    TagModule
  ],
  templateUrl: './books-page.html',
  styleUrl: './books-page.scss'
})
export class BooksPageComponent implements OnInit {
  private readonly booksApi = inject(BooksApiService);
  private readonly coverTones = ['navy', 'gold', 'teal', 'clay'];

  protected readonly books = signal<Book[]>([]);
  protected readonly isLoading = signal(false);
  protected readonly errorMessage = signal<string | null>(null);
  protected readonly searchTerm = signal('');

  protected readonly filteredBooks = computed(() => {
    const query = this.searchTerm().trim().toLocaleLowerCase('tr-TR');

    if (!query) {
      return this.books();
    }

    return this.books().filter((book) => {
      const name = book.name.toLocaleLowerCase('tr-TR');
      const author = book.author.toLocaleLowerCase('tr-TR');

      return name.includes(query) || author.includes(query);
    });
  });

  ngOnInit(): void {
    this.loadBooks();
  }

  protected updateSearchTerm(value: string): void {
    this.searchTerm.set(value);
  }

  protected getCoverClass(book: Book): string {
    const source = `${book.id}${book.name}`;
    const hash = Array.from(source).reduce((total, character) => total + character.charCodeAt(0), 0);

    return `book-cover--${this.coverTones[hash % this.coverTones.length]}`;
  }

  protected getStockLabel(stock: number): string {
    return stock > 0 ? `${stock} stokta` : 'Stokta yok';
  }

  protected getStockSeverity(stock: number): StockSeverity {
    return stock > 0 ? 'success' : 'danger';
  }

  private loadBooks(): void {
    this.isLoading.set(true);
    this.errorMessage.set(null);

    this.booksApi
      .getAll()
      .pipe(finalize(() => this.isLoading.set(false)))
      .subscribe({
        next: (books) => {
          this.books.set(books);
        },
        error: () => {
          this.errorMessage.set('Kitaplar yüklenirken bir hata oluştu.');
        }
      });
  }
}
