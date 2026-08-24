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

type LoginControlName = 'username' | 'password';

@Component({
  selector: 'app-login',
  imports: [
    ButtonModule,
    InputTextModule,
    MessageModule,
    PasswordModule,
    ReactiveFormsModule,
    RouterLink
  ],
  templateUrl: './login.html',
  styleUrl: './login.scss'
})
export class LoginComponent {
  private readonly authApi = inject(AuthApiService);
  private readonly authState = inject(AuthStateService);
  private readonly formBuilder = inject(FormBuilder);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);

  protected readonly isSubmitting = signal(false);
  protected readonly loginError = signal<string | null>(null);
  protected readonly registerQueryParams = {
    returnUrl: this.route.snapshot.queryParamMap.get('returnUrl')
  };

  protected readonly loginForm = this.formBuilder.nonNullable.group({
    username: ['', Validators.required],
    password: ['', Validators.required]
  });

  protected submit(): void {
    this.loginError.set(null);

    if (this.loginForm.invalid) {
      this.loginForm.markAllAsTouched();
      return;
    }

    if (this.isSubmitting()) {
      return;
    }

    this.isSubmitting.set(true);

    this.authApi
      .login(this.loginForm.getRawValue())
      .pipe(finalize(() => this.isSubmitting.set(false)))
      .subscribe({
        next: (response) => {
          this.authState.setAuthenticated(response.accessToken);
          void this.router.navigateByUrl(this.getReturnUrl());
        },
        error: (error: unknown) => {
          this.loginError.set(this.getLoginErrorMessage(error));
        }
      });
  }

  protected showValidationError(controlName: LoginControlName): boolean {
    const control = this.loginForm.controls[controlName];

    return control.invalid && (control.dirty || control.touched);
  }

  private getLoginErrorMessage(error: unknown): string {
    if (error instanceof HttpErrorResponse && error.status === 401) {
      return 'Kullanıcı adı veya şifre hatalı.';
    }

    if (error instanceof HttpErrorResponse && error.status === 400) {
      return 'Lütfen kullanıcı adı ve şifre alanlarını kontrol edin.';
    }

    return 'Giriş yapılırken bir sorun oluştu. Lütfen tekrar deneyin.';
  }

  private getReturnUrl(): string {
    return getSafeReturnUrl(this.route.snapshot.queryParamMap.get('returnUrl'));
  }
}
