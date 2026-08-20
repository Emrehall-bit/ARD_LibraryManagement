import { Injectable } from '@angular/core';

const ACCESS_TOKEN_STORAGE_KEY = 'librarysystem_access_token';

@Injectable({
  providedIn: 'root'
})
export class AuthStorageService {
  getAccessToken(): string | null {
    return localStorage.getItem(ACCESS_TOKEN_STORAGE_KEY);
  }

  setAccessToken(token: string): void {
    localStorage.setItem(ACCESS_TOKEN_STORAGE_KEY, token);
  }

  removeAccessToken(): void {
    localStorage.removeItem(ACCESS_TOKEN_STORAGE_KEY);
  }
}
