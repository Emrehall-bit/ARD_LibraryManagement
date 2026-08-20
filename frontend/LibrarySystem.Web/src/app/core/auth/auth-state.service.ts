import { computed, inject, Injectable, signal } from '@angular/core';

import { AuthStorageService } from './auth-storage.service';

@Injectable({
  providedIn: 'root'
})
export class AuthStateService {
  private readonly authStorage = inject(AuthStorageService);
  private readonly authenticated = signal(!!this.authStorage.getAccessToken());

  readonly isAuthenticated = computed(() => this.authenticated());

  setAuthenticated(accessToken: string): void {
    this.authStorage.setAccessToken(accessToken);
    this.authenticated.set(true);
  }

  logout(): void {
    this.authStorage.removeAccessToken();
    this.authenticated.set(false);
  }
}
