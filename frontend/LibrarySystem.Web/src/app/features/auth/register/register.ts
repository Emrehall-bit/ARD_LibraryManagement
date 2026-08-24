import { HttpErrorResponse } from '@angular/common/http';
import { Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { finalize } from 'rxjs';
import { ButtonModule } from 'primeng/button';
import { InputTextModule } from 'primeng/inputtext';
import { MessageModule } from 'primeng/message';
import { PasswordModule } from 'primeng/password';

import { AuthStateService } from '../../../core/auth/auth-state.service';
import { getSafeReturnUrl } from '../../../core/auth/auth-return-url';
import { AuthApiService } from '../services/auth-api.service';

type RegisterControlName = 'username' | 'email' | 'password';

@Component({
  selector: 'app-register',
  imports: [
    ButtonModule,
    InputTextModule,
    MessageModule,
    PasswordModule,
    ReactiveFormsModule,
    RouterLink
  ],
  templateUrl: './register.html',
  styleUrl: './register.scss'
})
export class RegisterComponent {
  private readonly authApi = inject(AuthApiService);
  private readonly authState = inject(AuthStateService);
  private readonly formBuilder = inject(FormBuilder);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);

  protected readonly isSubmitting = signal(false);
  protected readonly registerError = signal<string | null>(null);
  protected readonly loginQueryParams = {
    returnUrl: this.route.snapshot.queryParamMap.get('returnUrl')
  };

  protected readonly registerForm = this.formBuilder.nonNullable.group({
    username: ['', Validators.required],
    email: ['', [Validators.required, Validators.email]],
    password: ['', [Validators.required, Validators.minLength(8)]]
  });

  protected submit(): void {
    this.registerError.set(null);

    if (this.registerForm.invalid) {
      this.registerForm.markAllAsTouched();
      return;
    }

    if (this.isSubmitting()) {
      return;
    }

    this.isSubmitting.set(true);

    this.authApi
      .register(this.registerForm.getRawValue())
      .pipe(finalize(() => this.isSubmitting.set(false)))
      .subscribe({
        next: (response) => {
          this.authState.setAuthenticated(response.accessToken);
          void this.router.navigateByUrl(this.getReturnUrl());
        },
        error: (error: unknown) => {
          this.registerError.set(this.getRegisterErrorMessage(error));
        }
      });
  }

  protected showValidationError(controlName: RegisterControlName): boolean {
    const control = this.registerForm.controls[controlName];

    return control.invalid && (control.dirty || control.touched);
  }

  protected getValidationMessage(controlName: RegisterControlName): string {
    const control = this.registerForm.controls[controlName];

    if (control.hasError('required')) {
      return this.getRequiredMessage(controlName);
    }

    if (controlName === 'email' && control.hasError('email')) {
      return 'Geçerli bir e-posta adresi girin.';
    }

    if (controlName === 'password' && control.hasError('minlength')) {
      return 'Şifre en az 8 karakter olmalıdır.';
    }

    return '';
  }

  private getRegisterErrorMessage(error: unknown): string {
    if (error instanceof HttpErrorResponse && error.status === 409) {
      const detail = this.getProblemDetail(error);

      if (detail === 'Username is already taken.') {
        return 'Bu kullanıcı adı zaten kullanılıyor.';
      }

      if (detail === 'Email is already taken.') {
        return 'Bu e-posta adresi zaten kullanılıyor.';
      }

      return 'Bu bilgilerle kayıt oluşturulamıyor.';
    }

    if (error instanceof HttpErrorResponse && error.status === 400) {
      return 'Lütfen kayıt bilgilerini kontrol edin.';
    }

    return 'Kayıt oluşturulurken bir sorun oluştu. Lütfen tekrar deneyin.';
  }

  private getProblemDetail(error: HttpErrorResponse): string | null {
    const body = error.error as { detail?: unknown } | null;

    return typeof body?.detail === 'string' ? body.detail : null;
  }

  private getReturnUrl(): string {
    return getSafeReturnUrl(this.route.snapshot.queryParamMap.get('returnUrl'));
  }

  private getRequiredMessage(controlName: RegisterControlName): string {
    const messages: Record<RegisterControlName, string> = {
      username: 'Kullanıcı adı zorunludur.',
      email: 'E-posta zorunludur.',
      password: 'Şifre zorunludur.'
    };

    return messages[controlName];
  }
}
