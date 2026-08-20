import { Component, inject, output } from '@angular/core';
import { Router } from '@angular/router';
import { ButtonModule } from 'primeng/button';

import { AuthStateService } from '../../core/auth/auth-state.service';

@Component({
  selector: 'app-topbar',
  imports: [ButtonModule],
  templateUrl: './topbar.html',
  styleUrl: './topbar.scss'
})
export class TopbarComponent {
  readonly menuClick = output<void>();

  private readonly authState = inject(AuthStateService);
  private readonly router = inject(Router);

  protected readonly isAuthenticated = this.authState.isAuthenticated;

  login(): void {
    this.router.navigate(['/login']);
  }

  logout(): void {
    this.authState.logout();
    this.router.navigate(['/login']);
  }
}
