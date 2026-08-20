import { HttpErrorResponse } from '@angular/common/http';
import { Component, inject, OnInit, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ConfirmationService, MessageService } from 'primeng/api';
import { ButtonModule } from 'primeng/button';
import { CardModule } from 'primeng/card';
import { ConfirmDialogModule } from 'primeng/confirmdialog';
import { DialogModule } from 'primeng/dialog';
import { InputNumberModule } from 'primeng/inputnumber';
import { InputTextModule } from 'primeng/inputtext';
import { MessageModule } from 'primeng/message';
import { PaginatorModule } from 'primeng/paginator';
import { ProgressSpinnerModule } from 'primeng/progressspinner';
import { TagModule } from 'primeng/tag';
import { ToastModule } from 'primeng/toast';
import { finalize } from 'rxjs';

import { AuthStateService } from '../../../core/auth/auth-state.service';
import { BorrowingApiService } from '../../borrowing/services/borrowing-api.service';
import { Book } from '../models/book.model';
import { CreateBookRequest } from '../models/create-book-request.model';
import { UpdateBookRequest } from '../models/update-book-request.model';
import { BooksApiService } from '../services/books-api.service';

type StockSeverity = 'success' | 'danger';
type CreateBookControlName = 'name' | 'author' | 'stock';
type EditBookControlName = 'name' | 'author' | 'stock';
type BooksPageChangeEvent = { first?: number; rows?: number; page?: number };

@Component({
  selector: 'app-books-page',
  imports: [
    ButtonModule,
    CardModule,
    ConfirmDialogModule,
    DialogModule,
    InputNumberModule,
    InputTextModule,
    MessageModule,
    PaginatorModule,
    ProgressSpinnerModule,
    ReactiveFormsModule,
    TagModule,
    ToastModule
  ],
  providers: [ConfirmationService, MessageService],
  templateUrl: './books-page.html',
  styleUrl: './books-page.scss'
})
export class BooksPageComponent implements OnInit {
  private readonly authState = inject(AuthStateService);
  private readonly booksApi = inject(BooksApiService);
  private readonly borrowingApi = inject(BorrowingApiService);
  private readonly confirmationService = inject(ConfirmationService);
  private readonly formBuilder = inject(FormBuilder);
  private readonly messageService = inject(MessageService);
  private readonly coverTones = ['navy', 'gold', 'teal', 'clay'];

  protected readonly books = signal<Book[]>([]);
  protected readonly isLoading = signal(false);
  protected readonly errorMessage = signal<string | null>(null);
  protected readonly searchTerm = signal('');
  protected readonly activeSearchTerm = signal('');
  protected readonly page = signal(1);
  protected readonly pageSize = signal(20);
  protected readonly totalCount = signal(0);
  protected readonly totalPages = signal(0);
  protected readonly borrowingBookId = signal<string | null>(null);
  protected readonly isCreateDialogVisible = signal(false);
  protected readonly isCreating = signal(false);
  protected readonly createErrorMessage = signal<string | null>(null);
  protected readonly selectedBook = signal<Book | null>(null);
  protected readonly isEditDialogVisible = signal(false);
  protected readonly isUpdating = signal(false);
  protected readonly editErrorMessage = signal<string | null>(null);
  protected readonly deletingBookId = signal<string | null>(null);
  protected readonly isAdmin = this.authState.isAdmin;
  protected readonly pageSizeOptions = [20, 40, 60, 100];

  protected readonly createBookForm = this.formBuilder.nonNullable.group({
    name: ['', [Validators.required, Validators.maxLength(200)]],
    author: ['', [Validators.required, Validators.maxLength(200)]],
    stock: [0, [Validators.required, Validators.min(0)]]
  });

  protected readonly editBookForm = this.formBuilder.nonNullable.group({
    name: ['', [Validators.required, Validators.maxLength(200)]],
    author: ['', [Validators.required, Validators.maxLength(200)]],
    stock: [0, [Validators.required, Validators.min(0)]]
  });

  ngOnInit(): void {
    this.loadBooks();
  }

  protected updateSearchTerm(value: string): void {
    this.searchTerm.set(value);
  }

  protected searchBooks(): void {
    this.activeSearchTerm.set(this.searchTerm().trim());
    this.page.set(1);
    this.loadBooks();
  }

  protected handlePageChange(event: BooksPageChangeEvent): void {
    const nextPageSize = event.rows ?? this.pageSize();
    const nextPage = nextPageSize !== this.pageSize()
      ? 1
      : (event.page ?? Math.floor((event.first ?? 0) / nextPageSize)) + 1;

    this.pageSize.set(nextPageSize);
    this.page.set(nextPage);
    this.loadBooks();
  }

  protected openCreateDialog(): void {
    this.createErrorMessage.set(null);
    this.isCreateDialogVisible.set(true);
  }

  protected closeCreateDialog(): void {
    if (this.isCreating()) {
      return;
    }

    this.isCreateDialogVisible.set(false);
    this.resetCreateForm();
  }

  protected openEditDialog(book: Book): void {
    if (this.isDeleting(book.id)) {
      return;
    }

    this.selectedBook.set(book);
    this.editErrorMessage.set(null);
    this.editBookForm.reset({
      name: book.name,
      author: book.author,
      stock: book.stock
    });
    this.isEditDialogVisible.set(true);
  }

  protected closeEditDialog(): void {
    if (this.isUpdating()) {
      return;
    }

    this.isEditDialogVisible.set(false);
    this.resetEditForm();
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

  protected isDeleting(bookId: string): boolean {
    return this.deletingBookId() === bookId;
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
          this.loadCurrentPage();
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

  protected createBook(): void {
    this.createErrorMessage.set(null);

    if (this.createBookForm.invalid) {
      this.createBookForm.markAllAsTouched();
      return;
    }

    if (this.isCreating()) {
      return;
    }

    const request: CreateBookRequest = this.createBookForm.getRawValue();

    this.isCreating.set(true);

    this.booksApi
      .create(request)
      .pipe(finalize(() => this.isCreating.set(false)))
      .subscribe({
        next: () => {
          this.messageService.add({
            severity: 'success',
            summary: 'Başarılı',
            detail: 'Kitap eklendi.'
          });
          this.isCreateDialogVisible.set(false);
          this.resetCreateForm();
          this.loadCurrentPage();
        },
        error: (error: unknown) => {
          this.createErrorMessage.set(this.getCreateErrorMessage(error));
        }
      });
  }

  protected updateBook(): void {
    this.editErrorMessage.set(null);

    if (this.editBookForm.invalid) {
      this.editBookForm.markAllAsTouched();
      return;
    }

    const book = this.selectedBook();

    if (!book || this.isUpdating()) {
      return;
    }

    const request: UpdateBookRequest = this.editBookForm.getRawValue();

    this.isUpdating.set(true);

    this.booksApi
      .update(book.id, request)
      .pipe(finalize(() => this.isUpdating.set(false)))
      .subscribe({
        next: () => {
          this.messageService.add({
            severity: 'success',
            summary: 'Başarılı',
            detail: 'Kitap güncellendi.'
          });
          this.isEditDialogVisible.set(false);
          this.resetEditForm();
          this.loadCurrentPage();
        },
        error: (error: unknown) => {
          this.editErrorMessage.set(this.getUpdateErrorMessage(error));
        }
      });
  }

  protected confirmDeleteBook(book: Book): void {
    if (this.deletingBookId()) {
      return;
    }

    this.confirmationService.confirm({
      header: 'Kitabı Sil',
      message: `"${book.name}" kitabını silmek istediğinize emin misiniz?`,
      icon: 'pi pi-exclamation-triangle',
      acceptLabel: 'Evet, Sil',
      rejectLabel: 'Vazgeç',
      acceptButtonStyleClass: 'p-button-danger',
      rejectButtonStyleClass: 'p-button-secondary p-button-text',
      accept: () => this.deleteBook(book)
    });
  }

  protected showCreateValidationError(controlName: CreateBookControlName): boolean {
    const control = this.createBookForm.controls[controlName];

    return control.invalid && (control.dirty || control.touched);
  }

  protected showEditValidationError(controlName: EditBookControlName): boolean {
    const control = this.editBookForm.controls[controlName];

    return control.invalid && (control.dirty || control.touched);
  }

  protected getCreateValidationMessage(controlName: CreateBookControlName): string {
    const control = this.createBookForm.controls[controlName];

    if (control.hasError('required')) {
      return this.getCreateRequiredMessage(controlName);
    }

    if ((controlName === 'name' || controlName === 'author') && control.hasError('maxlength')) {
      return 'En fazla 200 karakter girebilirsiniz.';
    }

    if (controlName === 'stock' && control.hasError('min')) {
      return 'Stok 0 veya daha büyük olmalıdır.';
    }

    return '';
  }

  protected getEditValidationMessage(controlName: EditBookControlName): string {
    const control = this.editBookForm.controls[controlName];

    if (control.hasError('required')) {
      return this.getEditRequiredMessage(controlName);
    }

    if ((controlName === 'name' || controlName === 'author') && control.hasError('maxlength')) {
      return 'En fazla 200 karakter girebilirsiniz.';
    }

    if (controlName === 'stock' && control.hasError('min')) {
      return 'Stok 0 veya daha büyük olmalıdır.';
    }

    return '';
  }

  private deleteBook(book: Book): void {
    if (this.deletingBookId()) {
      return;
    }

    this.deletingBookId.set(book.id);

    this.booksApi
      .delete(book.id)
      .pipe(finalize(() => this.deletingBookId.set(null)))
      .subscribe({
        next: () => {
          this.messageService.add({
            severity: 'success',
            summary: 'Başarılı',
            detail: 'Kitap silindi.'
          });
          if (this.books().length === 1 && this.page() > 1) {
            this.page.update((page) => page - 1);
          }

          this.loadCurrentPage();
        },
        error: (error: unknown) => {
          this.messageService.add({
            severity: 'error',
            summary: 'İşlem başarısız',
            detail: this.getDeleteErrorMessage(error)
          });
        }
      });
  }

  private loadBooks(): void {
    this.isLoading.set(true);
    this.errorMessage.set(null);

    this.booksApi
      .getAll(this.page(), this.pageSize(), this.activeSearchTerm())
      .pipe(finalize(() => this.isLoading.set(false)))
      .subscribe({
        next: (response) => {
          this.books.set(response.items);
          this.page.set(response.page);
          this.pageSize.set(response.pageSize);
          this.totalCount.set(response.totalCount);
          this.totalPages.set(response.totalPages);
        },
        error: () => {
          this.errorMessage.set('Kitaplar yüklenirken bir hata oluştu.');
        }
      });
  }

  private loadCurrentPage(): void {
    this.loadBooks();
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

  private getCreateErrorMessage(error: unknown): string {
    if (!(error instanceof HttpErrorResponse)) {
      return 'Kitap eklenirken bir hata oluştu.';
    }

    const problem = this.getProblemDetails(error);

    if (error.status === 400 && problem?.title === 'Validation failed.') {
      return 'Lütfen kitap bilgilerini kontrol edin.';
    }

    if (error.status === 400) {
      return 'Kitap bilgileri geçerli değil.';
    }

    return 'Kitap eklenirken bir hata oluştu.';
  }

  private getUpdateErrorMessage(error: unknown): string {
    if (!(error instanceof HttpErrorResponse)) {
      return 'Kitap güncellenirken bir hata oluştu.';
    }

    const problem = this.getProblemDetails(error);

    if (error.status === 400 && problem?.title === 'Validation failed.') {
      return 'Lütfen kitap bilgilerini kontrol edin.';
    }

    if (error.status === 404 && problem?.title === 'Resource not found.') {
      return 'Kitap bulunamadı.';
    }

    if (error.status === 409 && problem?.title === 'Concurrency conflict.') {
      return 'Kitap durumu değişti. Lütfen tekrar deneyin.';
    }

    return 'Kitap güncellenirken bir hata oluştu.';
  }

  private getDeleteErrorMessage(error: unknown): string {
    if (!(error instanceof HttpErrorResponse)) {
      return 'Kitap silinirken bir hata oluştu.';
    }

    const problem = this.getProblemDetails(error);

    if (error.status === 404 && problem?.title === 'Resource not found.') {
      return 'Kitap bulunamadı.';
    }

    if (error.status === 409 && problem?.title === 'Concurrency conflict.') {
      return 'Kitap durumu değişti. Lütfen tekrar deneyin.';
    }

    return 'Kitap silinirken bir hata oluştu.';
  }

  private getProblemDetails(error: HttpErrorResponse): { title?: string; detail?: string } | null {
    const body = error.error as { title?: unknown; detail?: unknown } | null;

    return {
      title: typeof body?.title === 'string' ? body.title : undefined,
      detail: typeof body?.detail === 'string' ? body.detail : undefined
    };
  }

  private resetCreateForm(): void {
    this.createBookForm.reset({
      name: '',
      author: '',
      stock: 0
    });
    this.createErrorMessage.set(null);
  }

  private resetEditForm(): void {
    this.editBookForm.reset({
      name: '',
      author: '',
      stock: 0
    });
    this.selectedBook.set(null);
    this.editErrorMessage.set(null);
  }

  private getCreateRequiredMessage(controlName: CreateBookControlName): string {
    const messages: Record<CreateBookControlName, string> = {
      name: 'Kitap adı zorunludur.',
      author: 'Yazar zorunludur.',
      stock: 'Stok zorunludur.'
    };

    return messages[controlName];
  }

  private getEditRequiredMessage(controlName: EditBookControlName): string {
    const messages: Record<EditBookControlName, string> = {
      name: 'Kitap adı zorunludur.',
      author: 'Yazar zorunludur.',
      stock: 'Stok zorunludur.'
    };

    return messages[controlName];
  }
}
