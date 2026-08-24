import { Component, computed, DestroyRef, inject, OnInit, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { RouterLink } from '@angular/router';
import { forkJoin, finalize } from 'rxjs';
import { ButtonModule } from 'primeng/button';
import { CardModule } from 'primeng/card';
import { InputTextModule } from 'primeng/inputtext';
import { MessageModule } from 'primeng/message';
import { ProgressSpinnerModule } from 'primeng/progressspinner';
import { TagModule } from 'primeng/tag';

import {
  BorrowDueDateDisplay,
  getOverdueDaysLabel,
  getUpcomingBorrowDueDisplay
} from '../borrowing/borrow-due-date-display';
import { BorrowStatusSeverity, getBorrowStatusDisplay } from '../borrowing/borrow-status-display';
import { BorrowedBook } from '../borrowing/models/borrowed-book.model';
import { BorrowingApiService } from '../borrowing/services/borrowing-api.service';
import { Book } from '../books/models/book.model';
import { BooksApiService } from '../books/services/books-api.service';
import { AdminDashboardSummary, AdminRecentOverdueBorrow } from './models/admin-dashboard-summary.model';
import { AdminDashboardApiService } from './services/admin-dashboard-api.service';
import { AuthStateService } from '../../core/auth/auth-state.service';
import { LibraryRealtimeService } from '../../core/realtime/library-realtime.service';

interface SummaryItem {
  label: string;
  value: string;
  icon: string;
  tone: 'gold' | 'teal' | 'warning' | 'danger';
}

interface UpcomingBorrowedBook {
  item: BorrowedBook;
  dueDisplay: BorrowDueDateDisplay;
}

@Component({
  selector: 'app-dashboard',
  imports: [ButtonModule, CardModule, InputTextModule, MessageModule, ProgressSpinnerModule, RouterLink, TagModule],
  templateUrl: './dashboard.html',
  styleUrl: './dashboard.scss'
})
export class DashboardComponent implements OnInit {
  private readonly authState = inject(AuthStateService);
  private readonly adminDashboardApi = inject(AdminDashboardApiService);
  private readonly booksApi = inject(BooksApiService);
  private readonly borrowingApi = inject(BorrowingApiService);
  private readonly destroyRef = inject(DestroyRef);
  private readonly libraryRealtime = inject(LibraryRealtimeService);
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
  protected readonly adminSummary = signal<AdminDashboardSummary | null>(null);
  protected readonly isAdminLoading = signal(false);
  protected readonly adminErrorMessage = signal<string | null>(null);
  protected readonly isLoading = signal(false);
  protected readonly errorMessage = signal<string | null>(null);
  protected readonly isAuthenticated = this.authState.isAuthenticated;
  protected readonly isAdminDashboard = this.authState.isAdmin;

  protected readonly catalogBooks = computed(() => this.books().slice(0, 4));
  protected readonly recentBorrowedBooks = computed(() => this.borrowedBooks().slice(0, 3));
  protected readonly overdueBorrowCount = computed(() =>
    this.borrowedBooks().filter((borrowedBook) => borrowedBook.status === 'Overdue').length
  );
  private readonly upcomingBorrowedBookEntries = computed<UpcomingBorrowedBook[]>(() => {
    const now = new Date();

    return this.borrowedBooks()
      .map((borrowedBook) => ({
        item: borrowedBook,
        dueDisplay: getUpcomingBorrowDueDisplay(borrowedBook, now)
      }))
      .filter((borrowedBook): borrowedBook is UpcomingBorrowedBook => borrowedBook.dueDisplay !== null)
      .sort((first, second) =>
        first.dueDisplay.remainingDays - second.dueDisplay.remainingDays ||
        new Date(first.item.dueDate).getTime() - new Date(second.item.dueDate).getTime());
  });
  protected readonly upcomingBorrowedBooks = computed(() => this.upcomingBorrowedBookEntries().slice(0, 3));
  protected readonly upcomingBorrowCount = computed(() => this.upcomingBorrowedBookEntries().length);

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
    },
    {
      label: 'Yaklaşan Teslimler',
      value: this.upcomingBorrowCount().toString(),
      icon: 'pi pi-calendar-clock',
      tone: 'warning'
    }
  ]);
  protected readonly adminSummaryItems = computed<SummaryItem[]>(() => {
    const summary = this.adminSummary();

    if (!summary) {
      return [];
    }

    return [
      { label: 'Toplam Kullanıcı', value: summary.totalUsers.toString(), icon: 'pi pi-users', tone: 'teal' },
      { label: 'Toplam Kitap', value: summary.totalBooks.toString(), icon: 'pi pi-book', tone: 'gold' },
      { label: 'Toplam Stok', value: summary.totalStock.toString(), icon: 'pi pi-box', tone: 'teal' },
      {
        label: 'Stokta Olmayan',
        value: summary.outOfStockBooks.toString(),
        icon: 'pi pi-exclamation-circle',
        tone: 'warning'
      },
      { label: 'Aktif Ödünç', value: summary.activeBorrows.toString(), icon: 'pi pi-bookmark', tone: 'teal' },
      { label: 'Gecikmiş Ödünç', value: summary.overdueBorrows.toString(), icon: 'pi pi-clock', tone: 'danger' },
      { label: 'İade Edilmiş', value: summary.returnedBorrows.toString(), icon: 'pi pi-check-circle', tone: 'gold' }
    ];
  });

  ngOnInit(): void {
    if (this.isAdminDashboard()) {
      this.loadAdminDashboardData();
      return;
    }

    void this.libraryRealtime.start();
    this.libraryRealtime.bookStockChanged$
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe((event) => this.updateBookStock(event.bookId, event.stock));

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

  protected getOverdueDaysLabel(item: BorrowedBook): string | null {
    return getOverdueDaysLabel(item);
  }

  protected getAdminDueDateLabel(item: AdminRecentOverdueBorrow): string {
    return this.dueDateFormatter.format(new Date(item.dueDate));
  }

  protected getAdminOverdueDaysLabel(item: AdminRecentOverdueBorrow): string {
    return item.overdueDays === 1
      ? '1 gün gecikmiş'
      : `${item.overdueDays} gün gecikmiş`;
  }

  protected getAdminBookName(item: AdminRecentOverdueBorrow): string {
    return item.bookName || 'Kitap adı bulunamadı';
  }

  protected getAdminAuthor(item: AdminRecentOverdueBorrow): string {
    return item.author || 'Yazar bilgisi bulunamadı';
  }

  private loadAdminDashboardData(): void {
    this.isAdminLoading.set(true);
    this.adminErrorMessage.set(null);

    this.adminDashboardApi
      .getSummary()
      .pipe(finalize(() => this.isAdminLoading.set(false)))
      .subscribe({
        next: (summary) => {
          this.adminSummary.set(summary);
        },
        error: () => {
          this.adminErrorMessage.set('Yönetim paneli verileri yüklenirken bir hata oluştu.');
        }
      });
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

  private updateBookStock(bookId: string, stock: number): void {
    if (!this.books().some((book) => book.id === bookId)) {
      return;
    }

    this.books.update((books) =>
      books.map((book) => book.id === bookId ? { ...book, stock } : book)
    );
  }
}
