import { HttpErrorResponse } from '@angular/common/http';
import { Component, inject, OnInit, signal } from '@angular/core';
import { MessageService } from 'primeng/api';
import { ButtonModule } from 'primeng/button';
import { CardModule } from 'primeng/card';
import { MessageModule } from 'primeng/message';
import { ProgressSpinnerModule } from 'primeng/progressspinner';
import { TagModule } from 'primeng/tag';
import { ToastModule } from 'primeng/toast';
import { finalize } from 'rxjs';

import { getOverdueDaysLabel } from '../borrow-due-date-display';
import { BorrowStatusSeverity, getBorrowStatusDisplay } from '../borrow-status-display';
import { BorrowedBook } from '../models/borrowed-book.model';
import { BorrowingApiService } from '../services/borrowing-api.service';

type RenewalHintSeverity = 'success' | 'danger' | 'secondary';

@Component({
  selector: 'app-my-books-page',
  imports: [
    ButtonModule,
    CardModule,
    MessageModule,
    ProgressSpinnerModule,
    TagModule,
    ToastModule
  ],
  providers: [MessageService],
  templateUrl: './my-books-page.html',
  styleUrl: './my-books-page.scss'
})
export class MyBooksPageComponent implements OnInit {
  private readonly maxRenewalCount = 1;
  private readonly borrowingApi = inject(BorrowingApiService);
  private readonly messageService = inject(MessageService);
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

  protected readonly borrowedBooks = signal<BorrowedBook[]>([]);
  protected readonly isLoading = signal(false);
  protected readonly errorMessage = signal<string | null>(null);
  protected readonly returningBookId = signal<string | null>(null);
  protected readonly renewingBookId = signal<string | null>(null);

  ngOnInit(): void {
    this.loadMyBooks();
  }

  protected isReturning(bookId: string): boolean {
    return this.returningBookId() === bookId;
  }

  protected isRenewing(bookId: string): boolean {
    return this.renewingBookId() === bookId;
  }

  protected returnBook(item: BorrowedBook): void {
    if (item.returnedAt || this.returningBookId() || this.isRenewing(item.bookId)) {
      return;
    }

    this.returningBookId.set(item.bookId);

    this.borrowingApi
      .returnBook(item.bookId)
      .pipe(finalize(() => this.returningBookId.set(null)))
      .subscribe({
        next: () => {
          this.messageService.add({
            severity: 'success',
            summary: 'Başarılı',
            detail: 'Kitap iade edildi.'
          });
          this.loadMyBooks();
        },
        error: (error: unknown) => {
          this.messageService.add({
            severity: 'error',
            summary: 'İşlem başarısız',
            detail: this.getReturnErrorMessage(error)
          });
        }
      });
  }

  protected renewBook(item: BorrowedBook): void {
    if (!this.canRenew(item) || this.renewingBookId() || this.isReturning(item.bookId)) {
      return;
    }

    this.renewingBookId.set(item.bookId);

    this.borrowingApi
      .renew(item.bookId)
      .pipe(finalize(() => this.renewingBookId.set(null)))
      .subscribe({
        next: () => {
          this.messageService.add({
            severity: 'success',
            summary: 'Başarılı',
            detail: 'Ödünç süresi 7 gün uzatıldı.'
          });
          this.loadMyBooks();
        },
        error: (error: unknown) => {
          this.messageService.add({
            severity: 'error',
            summary: 'İşlem başarısız',
            detail: this.getRenewErrorMessage(error)
          });
        }
      });
  }

  protected getCoverClass(item: BorrowedBook): string {
    const source = `${item.bookId}${item.bookName ?? ''}`;
    const hash = Array.from(source).reduce((total, character) => total + character.charCodeAt(0), 0);

    return `book-cover--${this.coverTones[hash % this.coverTones.length]}`;
  }

  protected getBookName(item: BorrowedBook): string {
    return item.bookName ?? 'Kitap adı bulunamadı';
  }

  protected getAuthor(item: BorrowedBook): string {
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

  protected canRenew(item: BorrowedBook): boolean {
    return item.status === 'Borrowed' && item.renewalCount < this.maxRenewalCount;
  }

  protected getRenewalHint(item: BorrowedBook): string {
    if (item.status === 'Overdue') {
      return 'Gecikmiş kitap uzatılamaz';
    }

    if (item.renewalCount >= this.maxRenewalCount) {
      return 'Uzatma hakkı kullanıldı';
    }

    return '1 uzatma hakkı';
  }

  protected getRenewalHintSeverity(item: BorrowedBook): RenewalHintSeverity {
    if (item.status === 'Overdue') {
      return 'danger';
    }

    return item.renewalCount >= this.maxRenewalCount ? 'secondary' : 'success';
  }

  private loadMyBooks(): void {
    this.isLoading.set(true);
    this.errorMessage.set(null);

    this.borrowingApi
      .getMyBooks()
      .pipe(finalize(() => this.isLoading.set(false)))
      .subscribe({
        next: (borrowedBooks) => {
          this.borrowedBooks.set(borrowedBooks);
        },
        error: () => {
          this.errorMessage.set('Ödünç aldığınız kitaplar yüklenirken bir hata oluştu.');
        }
      });
  }

  private getReturnErrorMessage(error: unknown): string {
    if (!(error instanceof HttpErrorResponse)) {
      return 'Kitap iade edilirken bir hata oluştu.';
    }

    const problem = this.getProblemDetails(error);

    if (error.status === 404 && problem?.title === 'Resource not found.') {
      return 'Aktif ödünç kaydı bulunamadı.';
    }

    if (error.status === 409 && problem?.title === 'Concurrency conflict.') {
      return 'Kitap durumu değişti. Lütfen tekrar deneyin.';
    }

    return 'Kitap iade edilirken bir hata oluştu.';
  }

  private getRenewErrorMessage(error: unknown): string {
    if (!(error instanceof HttpErrorResponse)) {
      return 'Ödünç süresi uzatılırken bir hata oluştu.';
    }

    const problem = this.getProblemDetails(error);

    if (error.status === 400 && problem?.detail?.includes('Overdue borrow records cannot be renewed')) {
      return 'Gecikmiş kitapların süresi uzatılamaz.';
    }

    if (error.status === 400 && problem?.detail?.includes('already been renewed')) {
      return 'Bu kitap için uzatma hakkınızı zaten kullandınız.';
    }

    if (error.status === 404 && problem?.title === 'Resource not found.') {
      return 'Aktif ödünç kaydı bulunamadı.';
    }

    if (error.status === 409 && problem?.title === 'Concurrency conflict.') {
      return 'Ödünç kaydı değişti. Lütfen tekrar deneyin.';
    }

    return 'Ödünç süresi uzatılırken bir hata oluştu.';
  }

  private getProblemDetails(error: HttpErrorResponse): { title?: string; detail?: string } | null {
    const body = error.error as { title?: unknown; detail?: unknown } | null;

    return {
      title: typeof body?.title === 'string' ? body.title : undefined,
      detail: typeof body?.detail === 'string' ? body.detail : undefined
    };
  }
}
