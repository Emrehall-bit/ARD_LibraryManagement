import { HttpErrorResponse } from '@angular/common/http';
import { Component, computed, inject, OnInit, signal } from '@angular/core';
import { MessageService } from 'primeng/api';
import { ButtonModule } from 'primeng/button';
import { CardModule } from 'primeng/card';
import { InputTextModule } from 'primeng/inputtext';
import { MessageModule } from 'primeng/message';
import { ProgressSpinnerModule } from 'primeng/progressspinner';
import { TagModule } from 'primeng/tag';
import { ToastModule } from 'primeng/toast';
import { finalize } from 'rxjs';

import { BorrowingApiService } from '../../borrowing/services/borrowing-api.service';
import { Book } from '../models/book.model';
import { BooksApiService } from '../services/books-api.service';

type StockSeverity = 'success' | 'danger';

@Component({
  selector: 'app-books-page',
  imports: [
    ButtonModule,
    CardModule,
    InputTextModule,
    MessageModule,
    ProgressSpinnerModule,
    TagModule,
    ToastModule
  ],
  providers: [MessageService],
  templateUrl: './books-page.html',
  styleUrl: './books-page.scss'
})
export class BooksPageComponent implements OnInit {
  private readonly booksApi = inject(BooksApiService);
  private readonly borrowingApi = inject(BorrowingApiService);
  private readonly messageService = inject(MessageService);
  private readonly coverTones = ['navy', 'gold', 'teal', 'clay'];

  protected readonly books = signal<Book[]>([]);
  protected readonly isLoading = signal(false);
  protected readonly errorMessage = signal<string | null>(null);
  protected readonly searchTerm = signal('');
  protected readonly borrowingBookId = signal<string | null>(null);

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

  protected isBorrowing(bookId: string): boolean {
    return this.borrowingBookId() === bookId;
  }

  protected borrowBook(book: Book): void {
    if (book.stock <= 0 || this.borrowingBookId()) {
      return;
    }

    this.borrowingBookId.set(book.id);

    this.borrowingApi
      .borrow(book.id)
      .pipe(finalize(() => this.borrowingBookId.set(null)))
      .subscribe({
        next: () => {
          this.messageService.add({
            severity: 'success',
            summary: 'Başarılı',
            detail: 'Kitap ödünç alındı.'
          });
          this.loadBooks();
        },
        error: (error: unknown) => {
          this.messageService.add({
            severity: 'error',
            summary: 'İşlem başarısız',
            detail: this.getBorrowErrorMessage(error)
          });
        }
      });
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

  private getBorrowErrorMessage(error: unknown): string {
    if (!(error instanceof HttpErrorResponse)) {
      return 'Kitap ödünç alınırken bir hata oluştu.';
    }

    const problem = this.getProblemDetails(error);

    if (error.status === 400 && problem?.detail?.includes('is out of stock')) {
      return 'Bu kitap şu anda stokta bulunmuyor.';
    }

    if (error.status === 400 && problem?.detail?.includes('is already borrowed by the current user')) {
      return 'Bu kitabı zaten ödünç aldınız.';
    }

    if (error.status === 409 && problem?.title === 'Concurrency conflict.') {
      return 'Kitap durumu değişti. Lütfen tekrar deneyin.';
    }

    return 'Kitap ödünç alınırken bir hata oluştu.';
  }

  private getProblemDetails(error: HttpErrorResponse): { title?: string; detail?: string } | null {
    const body = error.error as { title?: unknown; detail?: unknown } | null;

    return {
      title: typeof body?.title === 'string' ? body.title : undefined,
      detail: typeof body?.detail === 'string' ? body.detail : undefined
    };
  }
}
