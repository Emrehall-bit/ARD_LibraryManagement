import { Component, inject, OnInit, signal } from '@angular/core';
import { MessageModule } from 'primeng/message';
import { ProgressSpinnerModule } from 'primeng/progressspinner';
import { TableModule } from 'primeng/table';
import { TagModule } from 'primeng/tag';
import { finalize } from 'rxjs';

import { BorrowStatusSeverity, getBorrowStatusDisplay } from '../borrow-status-display';
import { BorrowedBook } from '../models/borrowed-book.model';
import { BorrowingApiService } from '../services/borrowing-api.service';

@Component({
  selector: 'app-borrow-history-page',
  imports: [MessageModule, ProgressSpinnerModule, TableModule, TagModule],
  templateUrl: './borrow-history-page.html',
  styleUrl: './borrow-history-page.scss'
})
export class BorrowHistoryPageComponent implements OnInit {
  private readonly borrowingApi = inject(BorrowingApiService);
  private readonly dateTimeFormatter = new Intl.DateTimeFormat('tr-TR', {
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

  protected readonly history = signal<BorrowedBook[]>([]);
  protected readonly isLoading = signal(false);
  protected readonly errorMessage = signal<string | null>(null);

  ngOnInit(): void {
    this.loadHistory();
  }

  protected getBookName(item: BorrowedBook): string {
    return item.bookName ?? 'Kitap adı bulunamadı';
  }

  protected getAuthor(item: BorrowedBook): string {
    return item.author ?? 'Yazar bilgisi bulunamadı';
  }

  protected getBorrowedAtLabel(item: BorrowedBook): string {
    return this.dateTimeFormatter.format(new Date(item.borrowedAt));
  }

  protected getDueDateLabel(item: BorrowedBook): string {
    return this.dueDateFormatter.format(new Date(item.dueDate));
  }

  protected getReturnedAtLabel(item: BorrowedBook): string {
    return item.returnedAt ? this.dateTimeFormatter.format(new Date(item.returnedAt)) : '-';
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

  private loadHistory(): void {
    this.isLoading.set(true);
    this.errorMessage.set(null);

    this.borrowingApi
      .getHistory()
      .pipe(finalize(() => this.isLoading.set(false)))
      .subscribe({
        next: (history) => {
          this.history.set(history);
        },
        error: () => {
          this.errorMessage.set('Ödünç geçmişiniz yüklenirken bir hata oluştu.');
        }
      });
  }
}
