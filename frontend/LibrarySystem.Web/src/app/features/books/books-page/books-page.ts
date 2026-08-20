import { HttpErrorResponse } from '@angular/common/http';
import { Component, computed, inject, OnInit, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MessageService } from 'primeng/api';
import { ButtonModule } from 'primeng/button';
import { CardModule } from 'primeng/card';
import { DialogModule } from 'primeng/dialog';
import { InputNumberModule } from 'primeng/inputnumber';
import { InputTextModule } from 'primeng/inputtext';
import { MessageModule } from 'primeng/message';
import { ProgressSpinnerModule } from 'primeng/progressspinner';
import { TagModule } from 'primeng/tag';
import { ToastModule } from 'primeng/toast';
import { finalize } from 'rxjs';

import { BorrowingApiService } from '../../borrowing/services/borrowing-api.service';
import { Book } from '../models/book.model';
import { CreateBookRequest } from '../models/create-book-request.model';
import { BooksApiService } from '../services/books-api.service';

type StockSeverity = 'success' | 'danger';
type CreateBookControlName = 'name' | 'author' | 'stock';

@Component({
  selector: 'app-books-page',
  imports: [
    ButtonModule,
    CardModule,
    DialogModule,
    InputNumberModule,
    InputTextModule,
    MessageModule,
    ProgressSpinnerModule,
    ReactiveFormsModule,
    TagModule,
    ToastModule
  ],
  providers: [MessageService],
  templateUrl: './books-page.html',
  styleUrl: './books-page.scss'
})
export class BooksPageComponent implements OnInit {
  private readonly booksApi = inject(BooksApiService);
  private readonly borrowingApi = inject(BorrowingApiService);
  private readonly formBuilder = inject(FormBuilder);
  private readonly messageService = inject(MessageService);
  private readonly coverTones = ['navy', 'gold', 'teal', 'clay'];

  protected readonly books = signal<Book[]>([]);
  protected readonly isLoading = signal(false);
  protected readonly errorMessage = signal<string | null>(null);
  protected readonly searchTerm = signal('');
  protected readonly borrowingBookId = signal<string | null>(null);
  protected readonly isCreateDialogVisible = signal(false);
  protected readonly isCreating = signal(false);
  protected readonly createErrorMessage = signal<string | null>(null);

  protected readonly createBookForm = this.formBuilder.nonNullable.group({
    name: ['', [Validators.required, Validators.maxLength(200)]],
    author: ['', [Validators.required, Validators.maxLength(200)]],
    stock: [0, [Validators.required, Validators.min(0)]]
  });

  protected readonly filteredBooks = computed(() => {
    const query = this.searchTerm().trim().toLocaleLowerCase('tr-TR');

    if (!query) {
      return this.books();
    }

    return this.books().filter((book) => {
      const name = book.name.toLocaleLowerCase('tr-TR');
      const author = book.author.toLocaleLowerCase('tr-TR');

      return name.includes(query) || author.includes(query);
    });
  });

  ngOnInit(): void {
    this.loadBooks();
  }

  protected updateSearchTerm(value: string): void {
    this.searchTerm.set(value);
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
          this.loadBooks();
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
          this.loadBooks();
        },
        error: (error: unknown) => {
          this.createErrorMessage.set(this.getCreateErrorMessage(error));
        }
      });
  }

  protected showCreateValidationError(controlName: CreateBookControlName): boolean {
    const control = this.createBookForm.controls[controlName];

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

  private loadBooks(): void {
    this.isLoading.set(true);
    this.errorMessage.set(null);

    this.booksApi
      .getAll()
      .pipe(finalize(() => this.isLoading.set(false)))
      .subscribe({
        next: (books) => {
          this.books.set(books);
        },
        error: () => {
          this.errorMessage.set('Kitaplar yüklenirken bir hata oluştu.');
        }
      });
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

  private getCreateRequiredMessage(controlName: CreateBookControlName): string {
    const messages: Record<CreateBookControlName, string> = {
      name: 'Kitap adı zorunludur.',
      author: 'Yazar zorunludur.',
      stock: 'Stok zorunludur.'
    };

    return messages[controlName];
  }
}
