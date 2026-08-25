import { Component, inject, OnInit, signal } from '@angular/core';
import { MessageModule } from 'primeng/message';
import { PaginatorModule } from 'primeng/paginator';
import { ProgressSpinnerModule } from 'primeng/progressspinner';
import { TableModule } from 'primeng/table';
import { TagModule } from 'primeng/tag';
import { finalize } from 'rxjs';

import { BorrowStatusSeverity, getBorrowStatusDisplay } from '../borrow-status-display';
import { BorrowedBook } from '../models/borrowed-book.model';
import { BorrowingApiService } from '../services/borrowing-api.service';

type BorrowHistoryPageChangeEvent = { first?: number; rows?: number; page?: number };

@Component({
  selector: 'app-borrow-history-page',
  imports: [MessageModule, PaginatorModule, ProgressSpinnerModule, TableModule, TagModule],
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
  protected readonly page = signal(1);
  protected readonly pageSize = signal(20);
  protected readonly totalCount = signal(0);
  protected readonly totalPages = signal(0);
  protected readonly isLoading = signal(false);
  protected readonly errorMessage = signal<string | null>(null);
  protected readonly pageSizeOptions = [20, 40, 60, 100];

  ngOnInit(): void {
    this.loadHistory();
  }

  protected handlePageChange(event: BorrowHistoryPageChangeEvent): void {
    const nextPageSize = event.rows ?? this.pageSize();
    const nextPage = nextPageSize !== this.pageSize()
      ? 1
      : (event.page ?? Math.floor((event.first ?? 0) / nextPageSize)) + 1;

    this.pageSize.set(nextPageSize);
    this.page.set(nextPage);
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
      .getHistory(this.page(), this.pageSize())
      .pipe(finalize(() => this.isLoading.set(false)))
      .subscribe({
        next: (response) => {
          this.history.set(response.items);
          this.page.set(response.page);
          this.pageSize.set(response.pageSize);
          this.totalCount.set(response.totalCount);
          this.totalPages.set(response.totalPages);
        },
        error: () => {
          this.errorMessage.set('Ödünç geçmişiniz yüklenirken bir hata oluştu.');
        }
      });
  }
}
