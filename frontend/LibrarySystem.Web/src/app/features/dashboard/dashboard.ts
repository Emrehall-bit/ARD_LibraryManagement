import { Component, computed, inject, OnInit, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { forkJoin, finalize } from 'rxjs';
import { ButtonModule } from 'primeng/button';
import { CardModule } from 'primeng/card';
import { InputTextModule } from 'primeng/inputtext';
import { MessageModule } from 'primeng/message';
import { ProgressSpinnerModule } from 'primeng/progressspinner';
import { TagModule } from 'primeng/tag';

import { BorrowStatusSeverity, getBorrowStatusDisplay } from '../borrowing/borrow-status-display';
import { BorrowedBook } from '../borrowing/models/borrowed-book.model';
import { BorrowingApiService } from '../borrowing/services/borrowing-api.service';
import { Book } from '../books/models/book.model';
import { BooksApiService } from '../books/services/books-api.service';
import { AuthStateService } from '../../core/auth/auth-state.service';

interface SummaryItem {
  label: string;
  value: string;
  icon: string;
  tone: 'gold' | 'teal' | 'danger';
}

@Component({
  selector: 'app-dashboard',
  imports: [ButtonModule, CardModule, InputTextModule, MessageModule, ProgressSpinnerModule, RouterLink, TagModule],
  templateUrl: './dashboard.html',
  styleUrl: './dashboard.scss'
})
export class DashboardComponent implements OnInit {
  private readonly authState = inject(AuthStateService);
  private readonly booksApi = inject(BooksApiService);
  private readonly borrowingApi = inject(BorrowingApiService);
  private readonly coverTones = ['navy', 'gold', 'teal', 'clay'];
  private readonly dateFormatter = new Intl.DateTimeFormat('tr-TR', {
    day: '2-digit',
    month: 'short',
    year: 'numeric',
    hour: '2-digit',
    minute: '2-digit'
  });
  private readonly dueDateFormatter = new Intl.DateTimeFormat('tr-TR', {
    day: 'numeric',
    month: 'long',
    year: 'numeric'
  });

  protected readonly books = signal<Book[]>([]);
  protected readonly totalBookCount = signal(0);
  protected readonly borrowedBooks = signal<BorrowedBook[]>([]);
  protected readonly isLoading = signal(false);
  protected readonly errorMessage = signal<string | null>(null);
  protected readonly isAuthenticated = this.authState.isAuthenticated;

  protected readonly catalogBooks = computed(() => this.books().slice(0, 4));
  protected readonly recentBorrowedBooks = computed(() => this.borrowedBooks().slice(0, 3));
  protected readonly overdueBorrowCount = computed(() =>
    this.borrowedBooks().filter((borrowedBook) => borrowedBook.status === 'Overdue').length
  );

  protected readonly summaryItems = computed<SummaryItem[]>(() => [
    { label: 'Toplam Kitap', value: this.totalBookCount().toString(), icon: 'pi pi-book', tone: 'gold' },
    {
      label: 'Aktif Ödünçlerim',
      value: this.borrowedBooks().length.toString(),
      icon: 'pi pi-bookmark',
      tone: 'teal'
    },
    {
      label: 'Gecikmiş Kitaplarım',
      value: this.overdueBorrowCount().toString(),
      icon: 'pi pi-exclamation-triangle',
      tone: 'danger'
    }
  ]);

  ngOnInit(): void {
    this.loadDashboardData();
  }

  protected getBookCoverClass(book: Book): string {
    return this.getCoverClass(`${book.id}${book.name}`);
  }

  protected getBorrowedCoverClass(item: BorrowedBook): string {
    return this.getCoverClass(`${item.bookId}${item.bookName ?? ''}`);
  }

  protected getStockLabel(stock: number): string {
    return stock > 0 ? `${stock} stokta` : 'Stokta yok';
  }

  protected getBorrowedBookName(item: BorrowedBook): string {
    return item.bookName ?? 'Kitap adı bulunamadı';
  }

  protected getBorrowedAuthor(item: BorrowedBook): string {
    return item.author ?? 'Yazar bilgisi bulunamadı';
  }

  protected getBorrowedAtLabel(item: BorrowedBook): string {
    return this.dateFormatter.format(new Date(item.borrowedAt));
  }

  protected getDueDateLabel(item: BorrowedBook): string {
    return this.dueDateFormatter.format(new Date(item.dueDate));
  }

  protected getStatusLabel(item: BorrowedBook): string {
    return getBorrowStatusDisplay(item.status).label;
  }

  protected getStatusSeverity(item: BorrowedBook): BorrowStatusSeverity {
    return getBorrowStatusDisplay(item.status).severity;
  }

  protected isOverdue(item: BorrowedBook): boolean {
    return item.status === 'Overdue';
  }

  private loadDashboardData(): void {
    this.isLoading.set(true);
    this.errorMessage.set(null);

    if (!this.isAuthenticated()) {
      this.booksApi
        .getAll({ page: 1, pageSize: 4 })
        .pipe(finalize(() => this.isLoading.set(false)))
        .subscribe({
          next: (books) => {
            this.books.set(books.items);
            this.totalBookCount.set(books.totalCount);
            this.borrowedBooks.set([]);
          },
          error: () => {
            this.errorMessage.set('Dashboard verileri yÃ¼klenirken bir hata oluÅŸtu.');
          }
        });
      return;
    }

    forkJoin({
      books: this.booksApi.getAll({ page: 1, pageSize: 4 }),
      borrowedBooks: this.borrowingApi.getMyBooks()
    })
      .pipe(finalize(() => this.isLoading.set(false)))
      .subscribe({
        next: ({ books, borrowedBooks }) => {
          this.books.set(books.items);
          this.totalBookCount.set(books.totalCount);
          this.borrowedBooks.set(borrowedBooks);
        },
        error: () => {
          this.errorMessage.set('Dashboard verileri yüklenirken bir hata oluştu.');
        }
      });
  }

  private getCoverClass(source: string): string {
    const hash = Array.from(source).reduce((total, character) => total + character.charCodeAt(0), 0);

    return `cover--${this.coverTones[hash % this.coverTones.length]}`;
  }
}
