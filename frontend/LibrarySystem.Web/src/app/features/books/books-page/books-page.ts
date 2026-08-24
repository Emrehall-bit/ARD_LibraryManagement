import { HttpErrorResponse } from '@angular/common/http';
import { Component, computed, DestroyRef, inject, OnInit, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormBuilder, FormsModule, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router } from '@angular/router';
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
import { SelectModule } from 'primeng/select';
import { SelectButtonModule } from 'primeng/selectbutton';
import { TableModule } from 'primeng/table';
import { TagModule } from 'primeng/tag';
import { ToastModule } from 'primeng/toast';
import { finalize } from 'rxjs';

import { AuthStateService } from '../../../core/auth/auth-state.service';
import { LibraryRealtimeService } from '../../../core/realtime/library-realtime.service';
import { hasActiveOverdueBorrow } from '../../borrowing/borrow-due-date-display';
import { BORROWING_POLICY } from '../../borrowing/borrowing-policy';
import { BorrowingApiService } from '../../borrowing/services/borrowing-api.service';
import { BookCategoryOption, getBookCategoryLabel } from '../book-category-options';
import { Book, BookCategory } from '../models/book.model';
import { CreateBookRequest } from '../models/create-book-request.model';
import { UpdateBookRequest } from '../models/update-book-request.model';
import { BookSortBy, BookSortDirection, BooksApiService, BookStockStatus } from '../services/books-api.service';

type StockSeverity = 'success' | 'danger';
type CreateBookControlName = 'name' | 'author' | 'stock' | 'category';
type EditBookControlName = 'name' | 'author' | 'stock' | 'category';
type BooksPageChangeEvent = { first?: number; rows?: number; page?: number };
type BookSortOptionValue = `${BookSortBy}:${BookSortDirection}`;
type CategoryFilterValue = BookCategory | 'all';
type BooksViewMode = 'catalog' | 'management';

interface BookSortOption {
  label: string;
  value: BookSortOptionValue;
  sortBy: BookSortBy;
  sortDirection: BookSortDirection;
}

interface StockStatusOption {
  label: string;
  value: BookStockStatus;
}

interface CategoryFilterOption {
  label: string;
  value: CategoryFilterValue;
}

interface BooksViewOption {
  label: string;
  value: BooksViewMode;
}

@Component({
  selector: 'app-books-page',
  imports: [
    ButtonModule,
    CardModule,
    ConfirmDialogModule,
    DialogModule,
    FormsModule,
    InputNumberModule,
    InputTextModule,
    MessageModule,
    PaginatorModule,
    ProgressSpinnerModule,
    ReactiveFormsModule,
    SelectModule,
    SelectButtonModule,
    TableModule,
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
  private readonly destroyRef = inject(DestroyRef);
  private readonly formBuilder = inject(FormBuilder);
  private readonly libraryRealtime = inject(LibraryRealtimeService);
  private readonly messageService = inject(MessageService);
  private readonly router = inject(Router);
  private readonly coverTones = ['navy', 'gold', 'teal', 'clay'];

  protected readonly books = signal<Book[]>([]);
  protected readonly isLoading = signal(false);
  protected readonly errorMessage = signal<string | null>(null);
  protected readonly searchTerm = signal('');
  protected readonly activeSearchTerm = signal('');
  protected readonly page = signal(1);
  protected readonly pageSize = signal(20);
  protected readonly selectedSort = signal<BookSortOptionValue>('name:asc');
  protected readonly stockStatus = signal<BookStockStatus>('all');
  protected readonly selectedCategory = signal<CategoryFilterValue>('all');
  protected readonly selectedView = signal<BooksViewMode>('catalog');
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
  protected readonly activeBorrowCount = signal(0);
  protected readonly hasOverdueBorrow = signal(false);
  protected readonly isAdmin = this.authState.isAdmin;
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
      return 'Aynı anda en fazla 3 kitap ödünç alabilirsiniz.';
    }

    return null;
  });
  protected readonly pageSizeOptions = [20, 40, 60, 100];
  protected readonly viewOptions: BooksViewOption[] = [
    { label: 'Kart Görünümü', value: 'catalog' },
    { label: 'Yönetim Görünümü', value: 'management' }
  ];
  protected readonly sortOptions: BookSortOption[] = [
    { label: 'Kitap Adı (A-Z)', value: 'name:asc', sortBy: 'name', sortDirection: 'asc' },
    { label: 'Kitap Adı (Z-A)', value: 'name:desc', sortBy: 'name', sortDirection: 'desc' },
    { label: 'Yazar (A-Z)', value: 'author:asc', sortBy: 'author', sortDirection: 'asc' },
    { label: 'Yazar (Z-A)', value: 'author:desc', sortBy: 'author', sortDirection: 'desc' },
    { label: 'Stok (Azdan Çoğa)', value: 'stock:asc', sortBy: 'stock', sortDirection: 'asc' },
    { label: 'Stok (Çoktan Aza)', value: 'stock:desc', sortBy: 'stock', sortDirection: 'desc' }
  ];
  protected readonly stockStatusOptions: StockStatusOption[] = [
    { label: 'Tüm Stoklar', value: 'all' },
    { label: 'Stokta Olanlar', value: 'inStock' },
    { label: 'Stokta Olmayanlar', value: 'outOfStock' }
  ];
  protected readonly categoryOptions: BookCategoryOption[] = [
    { label: 'Roman', value: 'Novel' },
    { label: 'Bilim Kurgu', value: 'ScienceFiction' },
    { label: 'Fantastik', value: 'Fantasy' },
    { label: 'Polisiye / Gizem', value: 'Mystery' },
    { label: 'Macera', value: 'Adventure' },
    { label: 'Aksiyon', value: 'Action' },
    { label: 'Korku / Gerilim', value: 'HorrorThriller' },
    { label: 'Tarih', value: 'History' },
    { label: 'Biyografi', value: 'Biography' },
    { label: 'Kişisel Gelişim', value: 'PersonalDevelopment' },
    { label: 'Psikoloji', value: 'Psychology' },
    { label: 'Felsefe', value: 'Philosophy' },
    { label: 'Bilim', value: 'Science' },
    { label: 'Çocuk', value: 'Children' },
    { label: 'Gençlik', value: 'YoungAdult' },
    { label: 'Şiir', value: 'Poetry' },
    { label: 'Diğer', value: 'Other' }
  ];
  protected readonly categoryFilterOptions: CategoryFilterOption[] = [
    { label: 'Tüm Türler', value: 'all' },
    ...this.categoryOptions
  ];

  protected readonly createBookForm = this.formBuilder.nonNullable.group({
    name: ['', [Validators.required, Validators.maxLength(200)]],
    author: ['', [Validators.required, Validators.maxLength(200)]],
    stock: [0, [Validators.required, Validators.min(0)]],
    category: ['Other' as BookCategory, Validators.required]
  });

  protected readonly editBookForm = this.formBuilder.nonNullable.group({
    name: ['', [Validators.required, Validators.maxLength(200)]],
    author: ['', [Validators.required, Validators.maxLength(200)]],
    stock: [0, [Validators.required, Validators.min(0)]],
    category: ['Other' as BookCategory, Validators.required]
  });

  ngOnInit(): void {
    void this.libraryRealtime.start();
    this.libraryRealtime.bookStockChanged$
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe((event) => this.updateBookStock(event.bookId, event.stock));

    this.loadBooks();
    this.loadBorrowEligibility();
  }

  protected updateSearchTerm(value: string): void {
    this.searchTerm.set(value);
  }

  protected searchBooks(): void {
    this.activeSearchTerm.set(this.searchTerm().trim());
    this.page.set(1);
    this.loadBooks();
  }

  protected updateSort(value: BookSortOptionValue): void {
    this.selectedSort.set(value);
    this.page.set(1);
    this.loadBooks();
  }

  protected updateStockStatus(value: BookStockStatus): void {
    this.stockStatus.set(value);
    this.page.set(1);
    this.loadBooks();
  }

  protected updateCategory(value: CategoryFilterValue): void {
    this.selectedCategory.set(value);
    this.page.set(1);
    this.loadBooks();
  }

  protected updateView(value: BooksViewMode): void {
    if (!this.isAdmin() && value === 'management') {
      this.selectedView.set('catalog');
      return;
    }

    this.selectedView.set(value);
  }

  protected isManagementView(): boolean {
    return this.isAdmin() && this.selectedView() === 'management';
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
      stock: book.stock,
      category: book.category
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

  protected getCategoryLabel(category: BookCategory): string {
    return getBookCategoryLabel(category);
  }

  protected viewDetails(book: Book): void {
    this.router.navigate(['/books', book.id]);
  }

  protected getEmptyStateMessage(): string {
    if (this.activeSearchTerm()) {
      return 'Aramanız ve filtrelerinizle eşleşen kitap bulunamadı.';
    }

    if (this.stockStatus() === 'inStock') {
      return 'Stokta bulunan kitap yok.';
    }

    if (this.stockStatus() === 'outOfStock') {
      return 'Stokta olmayan kitap yok.';
    }

    return 'Henüz kayıtlı kitap bulunmuyor.';
  }

  protected isBorrowing(bookId: string): boolean {
    return this.borrowingBookId() === bookId;
  }

  protected isDeleting(bookId: string): boolean {
    return this.deletingBookId() === bookId;
  }

  protected isBorrowDisabled(book: Book): boolean {
    return book.stock === 0 ||
      this.isBorrowing(book.id) ||
      this.isDeleting(book.id) ||
      (this.isAuthenticated() && (this.hasOverdueBorrow() || this.hasReachedBorrowLimit()));
  }

  protected shouldShowOverdueBorrowRestriction(book: Book): boolean {
    return book.stock > 0 && this.isAuthenticated() && this.hasOverdueBorrow();
  }

  protected borrowBook(book: Book): void {
    if (book.stock <= 0 ||
      this.borrowingBookId() ||
      (this.isAuthenticated() && (this.hasOverdueBorrow() || this.hasReachedBorrowLimit()))) {
      return;
    }

    if (!this.isAuthenticated()) {
      const returnUrl = this.router.createUrlTree(['/books']).toString();
      this.router.navigate(['/login'], { queryParams: { returnUrl } });
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
          this.loadBorrowEligibility();
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
      .getAll({
        page: this.page(),
        pageSize: this.pageSize(),
        search: this.activeSearchTerm(),
        stockStatus: this.stockStatus(),
        category: this.getSelectedCategory(),
        ...this.getSelectedSort()
      })
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

  private updateBookStock(bookId: string, stock: number): void {
    if (!this.books().some((book) => book.id === bookId)) {
      return;
    }

    this.books.update((books) =>
      books.map((book) => book.id === bookId ? { ...book, stock } : book)
    );
  }

  private getSelectedSort(): Pick<BookSortOption, 'sortBy' | 'sortDirection'> {
    return this.sortOptions.find((option) => option.value === this.selectedSort()) ?? this.sortOptions[0];
  }

  private getSelectedCategory(): BookCategory | null {
    return this.selectedCategory() === 'all'
      ? null
      : this.selectedCategory() as BookCategory;
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
      stock: 0,
      category: 'Other'
    });
    this.createErrorMessage.set(null);
  }

  private resetEditForm(): void {
    this.editBookForm.reset({
      name: '',
      author: '',
      stock: 0,
      category: 'Other'
    });
    this.selectedBook.set(null);
    this.editErrorMessage.set(null);
  }

  private getCreateRequiredMessage(controlName: CreateBookControlName): string {
    const messages: Record<CreateBookControlName, string> = {
      name: 'Kitap adı zorunludur.',
      author: 'Yazar zorunludur.',
      stock: 'Stok zorunludur.',
      category: 'Tür zorunludur.'
    };

    return messages[controlName];
  }

  private getEditRequiredMessage(controlName: EditBookControlName): string {
    const messages: Record<EditBookControlName, string> = {
      name: 'Kitap adı zorunludur.',
      author: 'Yazar zorunludur.',
      stock: 'Stok zorunludur.',
      category: 'Tür zorunludur.'
    };

    return messages[controlName];
  }
}
