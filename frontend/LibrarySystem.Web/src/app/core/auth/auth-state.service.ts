import { computed, inject, Injectable, signal } from '@angular/core';

import { AUTH_ROLES } from './auth-roles';
import { AuthStorageService } from './auth-storage.service';
import { decodeJwtRoles } from './jwt-role-decoder';

@Injectable({
  providedIn: 'root'
})
export class AuthStateService {
  private readonly authStorage = inject(AuthStorageService);
  private readonly initialAccessToken = this.authStorage.getAccessToken();
  private readonly authenticated = signal(!!this.initialAccessToken);
  private readonly userRoles = signal<string[]>(decodeJwtRoles(this.initialAccessToken));

  readonly isAuthenticated = computed(() => this.authenticated());
  readonly roles = computed(() => this.userRoles());
  readonly isAdmin = computed(() => this.userRoles().includes(AUTH_ROLES.Admin));

  setAuthenticated(accessToken: string): void {
    this.authStorage.setAccessToken(accessToken);
    this.userRoles.set(decodeJwtRoles(accessToken));
    this.authenticated.set(true);
  }

  logout(): void {
    this.authStorage.removeAccessToken();
    this.userRoles.set([]);
    this.authenticated.set(false);
  }
}
