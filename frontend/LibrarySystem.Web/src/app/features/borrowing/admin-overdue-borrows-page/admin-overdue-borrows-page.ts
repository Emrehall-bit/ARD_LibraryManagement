import { Component, inject, OnInit, signal } from '@angular/core';
import { MessageModule } from 'primeng/message';
import { PaginatorModule } from 'primeng/paginator';
import { ProgressSpinnerModule } from 'primeng/progressspinner';
import { TableModule } from 'primeng/table';
import { TagModule } from 'primeng/tag';
import { finalize } from 'rxjs';

import { BorrowStatusSeverity, getBorrowStatusDisplay } from '../borrow-status-display';
import { AdminOverdueBorrow } from '../models/admin-overdue-borrow.model';
import { BorrowingApiService } from '../services/borrowing-api.service';

type AdminOverduePageChangeEvent = { first?: number; rows?: number; page?: number };
type DelaySeverity = 'warn' | 'danger';

@Component({
  selector: 'app-admin-overdue-borrows-page',
  imports: [MessageModule, PaginatorModule, ProgressSpinnerModule, TableModule, TagModule],
  templateUrl: './admin-overdue-borrows-page.html',
  styleUrl: './admin-overdue-borrows-page.scss'
})
export class AdminOverdueBorrowsPageComponent implements OnInit {
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

  protected readonly items = signal<AdminOverdueBorrow[]>([]);
  protected readonly page = signal(1);
  protected readonly pageSize = signal(20);
  protected readonly totalCount = signal(0);
  protected readonly totalPages = signal(0);
  protected readonly isLoading = signal(false);
  protected readonly errorMessage = signal<string | null>(null);
  protected readonly pageSizeOptions = [20, 40, 60, 100];

  ngOnInit(): void {
    this.loadOverdueBorrows();
  }

  protected handlePageChange(event: AdminOverduePageChangeEvent): void {
    const nextPageSize = event.rows ?? this.pageSize();
    const nextPage = nextPageSize !== this.pageSize()
      ? 1
      : (event.page ?? Math.floor((event.first ?? 0) / nextPageSize)) + 1;

    this.pageSize.set(nextPageSize);
    this.page.set(nextPage);
    this.loadOverdueBorrows();
  }

  protected getBookName(item: AdminOverdueBorrow): string {
    return item.bookName ?? 'Kitap adı bulunamadı';
  }

  protected getAuthor(item: AdminOverdueBorrow): string {
    return item.author ?? 'Yazar bilgisi bulunamadı';
  }

  protected getBorrowedAtLabel(item: AdminOverdueBorrow): string {
    return this.dateTimeFormatter.format(new Date(item.borrowedAt));
  }

  protected getDueDateLabel(item: AdminOverdueBorrow): string {
    return this.dueDateFormatter.format(new Date(item.dueDate));
  }

  protected getOverdueDaysLabel(item: AdminOverdueBorrow): string {
    return item.overdueDays === 1
      ? '1 gün'
      : `${item.overdueDays} gün`;
  }

  protected getOverdueSeverity(item: AdminOverdueBorrow): DelaySeverity {
    return item.overdueDays <= 2 ? 'warn' : 'danger';
  }

  protected getRenewalCountLabel(item: AdminOverdueBorrow): string {
    return item.renewalCount === 0
      ? 'Uzatılmadı'
      : `${item.renewalCount} kez uzatıldı`;
  }

  protected getStatusLabel(item: AdminOverdueBorrow): string {
    return getBorrowStatusDisplay(item.status).label;
  }

  protected getStatusSeverity(item: AdminOverdueBorrow): BorrowStatusSeverity {
    return getBorrowStatusDisplay(item.status).severity;
  }

  private loadOverdueBorrows(): void {
    this.isLoading.set(true);
    this.errorMessage.set(null);

    this.borrowingApi
      .getOverdueBorrows(this.page(), this.pageSize())
      .pipe(finalize(() => this.isLoading.set(false)))
      .subscribe({
        next: (response) => {
          this.items.set(response.items);
          this.page.set(response.page);
          this.pageSize.set(response.pageSize);
          this.totalCount.set(response.totalCount);
          this.totalPages.set(response.totalPages);
        },
        error: () => {
          this.errorMessage.set('Gecikmiş ödünç kayıtları yüklenirken bir hata oluştu.');
        }
      });
  }
}
