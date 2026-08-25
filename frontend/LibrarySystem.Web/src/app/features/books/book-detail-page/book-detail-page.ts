import { HttpErrorResponse } from '@angular/common/http';
import { Component, computed, DestroyRef, inject, OnInit, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { MessageService } from 'primeng/api';
import { ButtonModule } from 'primeng/button';
import { CardModule } from 'primeng/card';
import { MessageModule } from 'primeng/message';
import { ProgressSpinnerModule } from 'primeng/progressspinner';
import { TagModule } from 'primeng/tag';
import { ToastModule } from 'primeng/toast';
import { finalize } from 'rxjs';

import { AuthStateService } from '../../../core/auth/auth-state.service';
import { LibraryRealtimeService } from '../../../core/realtime/library-realtime.service';
import { hasActiveOverdueBorrow } from '../../borrowing/borrow-due-date-display';
import { BORROWING_POLICY } from '../../borrowing/borrowing-policy';
import { BorrowingApiService } from '../../borrowing/services/borrowing-api.service';
import { getBookCategoryLabel } from '../book-category-options';
import { BookDetail } from '../models/book-detail.model';
import { Book, BookCategory } from '../models/book.model';
import { BooksApiService } from '../services/books-api.service';

type StockSeverity = 'success' | 'danger';

@Component({
  selector: 'app-book-detail-page',
  imports: [
    ButtonModule,
    CardModule,
    MessageModule,
    ProgressSpinnerModule,
    RouterLink,
    TagModule,
    ToastModule
  ],
  providers: [MessageService],
  templateUrl: './book-detail-page.html',
  styleUrl: './book-detail-page.scss'
})
export class BookDetailPageComponent implements OnInit {
  private readonly authState = inject(AuthStateService);
  private readonly booksApi = inject(BooksApiService);
  private readonly borrowingApi = inject(BorrowingApiService);
  private readonly destroyRef = inject(DestroyRef);
  private readonly libraryRealtime = inject(LibraryRealtimeService);
  private readonly messageService = inject(MessageService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly coverTones = ['navy', 'gold', 'teal', 'clay'];

  protected readonly book = signal<BookDetail | null>(null);
  protected readonly isLoading = signal(false);
  protected readonly errorMessage = signal<string | null>(null);
  protected readonly isBorrowing = signal(false);
  protected readonly activeBorrowCount = signal(0);
  protected readonly hasOverdueBorrow = signal(false);
  protected readonly isAuthenticated = this.authState.isAuthenticated;
  protected readonly maxActiveBorrowCount = BORROWING_POLICY.maxActiveBorrowCount;
  protected readonly hasReachedBorrowLimit = computed(() =>
    this.activeBorrowCount() >= BORROWING_POLICY.maxActiveBorrowCount
  );
  protected readonly borrowRestrictionMessage = computed(() => {
    if (!this.isAuthenticated()) {
      return null;
    }

    if (this.hasOverdueBorrow()) {
      return 'Gecikmiş kitabınızı iade etmeden yeni kitap ödünç alamazsınız.';
    }

    if (this.hasReachedBorrowLimit()) {
      return `Ödünç alma limitinize ulaştınız (${this.activeBorrowCount()} / ${BORROWING_POLICY.maxActiveBorrowCount}).`;
    }

    return null;
  });

  ngOnInit(): void {
    void this.libraryRealtime.start();
    this.libraryRealtime.bookStockChanged$
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe((event) => this.updateBookStock(event.bookId, event.stock));

    this.route.paramMap
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe((params) => {
        const id = params.get('id');

        if (!id) {
          this.book.set(null);
          this.errorMessage.set('Kitap bulunamadı.');
          return;
        }

        this.loadBook(id);
      });

    this.loadBorrowEligibility();
  }

  protected getCoverClass(book: Book): string {
    const source = `${book.id}${book.name}`;
    const hash = Array.from(source).reduce((total, character) => total + character.charCodeAt(0), 0);

    return `book-detail-cover--${this.coverTones[hash % this.coverTones.length]}`;
  }

  protected getCategoryLabel(category: BookCategory): string {
    return getBookCategoryLabel(category);
  }

  protected getStockLabel(stock: number): string {
    return stock > 0 ? `${stock} adet stokta` : 'Stokta yok';
  }

  protected getStockSeverity(stock: number): StockSeverity {
    return stock > 0 ? 'success' : 'danger';
  }

  protected getBorrowLabel(stock: number): string {
    if (stock === 0) {
      return 'Stokta Yok';
    }

    return this.isAuthenticated() ? 'Ödünç Al' : 'Giriş Yaparak Ödünç Al';
  }

  protected getBorrowIcon(stock: number): string {
    if (stock === 0) {
      return 'pi pi-bookmark';
    }

    return this.isAuthenticated() ? 'pi pi-bookmark' : 'pi pi-lock';
  }

  protected isBorrowDisabled(stock: number): boolean {
    return stock === 0 ||
      this.isBorrowing() ||
      (this.isAuthenticated() && (this.hasOverdueBorrow() || this.hasReachedBorrowLimit()));
  }

  protected shouldShowBorrowRestriction(stock: number): boolean {
    return stock > 0 && this.borrowRestrictionMessage() !== null;
  }

  protected borrowBook(): void {
    const currentBook = this.book();

    if (!currentBook ||
      currentBook.stock <= 0 ||
      this.isBorrowing() ||
      (this.isAuthenticated() && (this.hasOverdueBorrow() || this.hasReachedBorrowLimit()))) {
      return;
    }

    if (!this.isAuthenticated()) {
      const returnUrl = this.router.createUrlTree(['/books', currentBook.id]).toString();
      this.router.navigate(['/login'], { queryParams: { returnUrl } });
      return;
    }

    this.isBorrowing.set(true);

    this.borrowingApi
      .borrow(currentBook.id)
      .pipe(finalize(() => this.isBorrowing.set(false)))
      .subscribe({
        next: () => {
          this.messageService.add({
            severity: 'success',
            summary: 'Başarılı',
            detail: 'Kitap ödünç alındı.'
          });
          this.loadBorrowEligibility();
          this.loadBook(currentBook.id);
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

  private loadBook(id: string): void {
    this.isLoading.set(true);
    this.errorMessage.set(null);

    this.booksApi
      .getById(id)
      .pipe(finalize(() => this.isLoading.set(false)))
      .subscribe({
        next: (book) => {
          this.book.set(book);
        },
        error: (error: unknown) => {
          this.book.set(null);
          this.errorMessage.set(this.getLoadErrorMessage(error));
        }
      });
  }

  private updateBookStock(bookId: string, stock: number): void {
    this.book.update((book) => book?.id === bookId ? { ...book, stock } : book);
  }

  private loadBorrowEligibility(): void {
    if (!this.isAuthenticated()) {
      this.activeBorrowCount.set(0);
      this.hasOverdueBorrow.set(false);
      return;
    }

    this.borrowingApi
      .getMyBooks()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (borrowedBooks) => {
          this.activeBorrowCount.set(borrowedBooks.length);
          this.hasOverdueBorrow.set(hasActiveOverdueBorrow(borrowedBooks));
        },
        error: () => {
          this.activeBorrowCount.set(0);
          this.hasOverdueBorrow.set(false);
        }
      });
  }

  private getLoadErrorMessage(error: unknown): string {
    if (error instanceof HttpErrorResponse && error.status === 404) {
      return 'Kitap bulunamadı.';
    }

    return 'Kitap bilgileri yüklenirken bir hata oluştu.';
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

    if (error.status === 400 && problem?.detail?.includes('User has overdue borrowed books')) {
      return 'Gecikmiş kitabınızı iade etmeden yeni kitap ödünç alamazsınız.';
    }

    if (error.status === 400 && problem?.detail?.includes('User has reached the maximum active borrow limit')) {
      return 'Aynı anda en fazla 3 kitap ödünç alabilirsiniz.';
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
