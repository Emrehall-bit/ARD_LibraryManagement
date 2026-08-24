import { Component, computed, inject, output } from '@angular/core';
import { Router } from '@angular/router';
import { ButtonModule } from 'primeng/button';
import { TagModule } from 'primeng/tag';

import { AuthStateService } from '../../core/auth/auth-state.service';
import { AUTH_ROLES } from '../../core/auth/auth-roles';

@Component({
  selector: 'app-topbar',
  imports: [ButtonModule, TagModule],
  templateUrl: './topbar.html',
  styleUrl: './topbar.scss'
})
export class TopbarComponent {
  readonly menuClick = output<void>();

  private readonly authState = inject(AuthStateService);
  private readonly router = inject(Router);

  protected readonly isAuthenticated = this.authState.isAuthenticated;
  protected readonly username = computed(() => this.authState.username() ?? 'Kullanıcı');
  protected readonly avatarInitial = computed(() => this.username().trim().charAt(0).toLocaleUpperCase('tr-TR') || 'K');
  protected readonly roleLabel = computed(() => {
    if (this.authState.isAdmin()) {
      return 'Admin';
    }

    if (this.authState.roles().includes(AUTH_ROLES.Member)) {
      return 'Üye';
    }

    return null;
  });

  login(): void {
    this.router.navigate(['/login']);
  }

  logout(): void {
    this.authState.logout();
    this.router.navigate(['/login']);
  }
}
