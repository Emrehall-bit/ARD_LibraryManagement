import { HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';

import { environment } from '../../../environments/environment';
import { AuthStorageService } from '../auth/auth-storage.service';

export const authInterceptor: HttpInterceptorFn = (request, next) => {
  const authStorage = inject(AuthStorageService);
  const accessToken = authStorage.getAccessToken();
  const isApiRequest = request.url.startsWith(environment.apiBaseUrl);

  if (!isApiRequest || !accessToken || request.headers.has('Authorization')) {
    return next(request);
  }

  return next(
    request.clone({
      setHeaders: {
        Authorization: `Bearer ${accessToken}`
      }
    })
  );
};
