import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { catchError, throwError } from 'rxjs';

import { environment } from '../../../environments/environment';
import { AuthStateService } from '../auth/auth-state.service';

const anonymousAuthUrls = [
  `${environment.apiBaseUrl}/api/auth/login`,
  `${environment.apiBaseUrl}/api/auth/register`
];

export const authErrorInterceptor: HttpInterceptorFn = (request, next) => {
  const authState = inject(AuthStateService);
  const router = inject(Router);
  const isApiRequest = request.url.startsWith(environment.apiBaseUrl);
  const isAnonymousAuthRequest = anonymousAuthUrls.some((url) => request.url.startsWith(url));

  return next(request).pipe(
    catchError((error: unknown) => {
      if (
        isApiRequest &&
        !isAnonymousAuthRequest &&
        error instanceof HttpErrorResponse &&
        error.status === 401
      ) {
        authState.logout();
        void router.navigate(['/login']);
      }

      return throwError(() => error);
    })
  );
};
