import { Component, inject, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ButtonModule } from 'primeng/button';
import { InputTextModule } from 'primeng/inputtext';
import { MessageModule } from 'primeng/message';
import { PaginatorModule } from 'primeng/paginator';
import { ProgressSpinnerModule } from 'primeng/progressspinner';
import { TableModule } from 'primeng/table';
import { TagModule } from 'primeng/tag';
import { finalize } from 'rxjs';

import { AdminUser } from '../models/admin-user.model';
import { AdminUsersApiService } from '../services/admin-users-api.service';

type AdminUsersPageChangeEvent = { first?: number; rows?: number; page?: number };
type RoleSeverity = 'danger' | 'info' | 'secondary';
type UserStatusSeverity = 'success' | 'danger';

@Component({
  selector: 'app-admin-users-page',
  imports: [
    ButtonModule,
    FormsModule,
    InputTextModule,
    MessageModule,
    PaginatorModule,
    ProgressSpinnerModule,
    TableModule,
    TagModule
  ],
  templateUrl: './admin-users-page.html',
  styleUrl: './admin-users-page.scss'
})
export class AdminUsersPageComponent implements OnInit {
  private readonly adminUsersApi = inject(AdminUsersApiService);

  protected readonly users = signal<AdminUser[]>([]);
  protected readonly page = signal(1);
  protected readonly pageSize = signal(20);
  protected readonly totalCount = signal(0);
  protected readonly totalPages = signal(0);
  protected readonly searchTerm = signal('');
  protected readonly activeSearchTerm = signal('');
  protected readonly isLoading = signal(false);
  protected readonly errorMessage = signal<string | null>(null);
  protected readonly pageSizeOptions = [20, 40, 60, 100];

  ngOnInit(): void {
    this.loadUsers();
  }

  protected updateSearchTerm(value: string): void {
    this.searchTerm.set(value);
  }

  protected searchUsers(): void {
    this.activeSearchTerm.set(this.searchTerm().trim());
    this.page.set(1);
    this.loadUsers();
  }

  protected handlePageChange(event: AdminUsersPageChangeEvent): void {
    const nextPageSize = event.rows ?? this.pageSize();
    const nextPage = nextPageSize !== this.pageSize()
      ? 1
      : (event.page ?? Math.floor((event.first ?? 0) / nextPageSize)) + 1;

    this.pageSize.set(nextPageSize);
    this.page.set(nextPage);
    this.loadUsers();
  }

  protected hasActiveSearch(): boolean {
    return this.activeSearchTerm().length > 0;
  }

  protected getRoleLabel(role: string): string {
    if (role === 'Admin') {
      return 'Admin';
    }

    if (role === 'Member') {
      return 'Üye';
    }

    return role;
  }

  protected getRoleSeverity(role: string): RoleSeverity {
    if (role === 'Admin') {
      return 'danger';
    }

    if (role === 'Member') {
      return 'info';
    }

    return 'secondary';
  }

  protected getUserStatusLabel(user: AdminUser): string {
    return user.overdueBorrowCount > 0
      ? 'Gecikmiş Kitabı Var'
      : 'Normal';
  }

  protected getUserStatusSeverity(user: AdminUser): UserStatusSeverity {
    return user.overdueBorrowCount > 0 ? 'danger' : 'success';
  }

  private loadUsers(): void {
    this.isLoading.set(true);
    this.errorMessage.set(null);

    this.adminUsersApi
      .getAll(this.page(), this.pageSize(), this.activeSearchTerm())
      .pipe(finalize(() => this.isLoading.set(false)))
      .subscribe({
        next: (response) => {
          this.users.set(response.items);
          this.page.set(response.page);
          this.pageSize.set(response.pageSize);
          this.totalCount.set(response.totalCount);
          this.totalPages.set(response.totalPages);
        },
        error: () => {
          this.errorMessage.set('Kullanıcılar yüklenirken bir hata oluştu.');
        }
      });
  }
}
