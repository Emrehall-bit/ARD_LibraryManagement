import { Component, computed, inject } from '@angular/core';
import { RouterLink, RouterLinkActive } from '@angular/router';

import { AuthStateService } from '../../core/auth/auth-state.service';

interface SidebarItem {
  label: string;
  icon: string;
  route: string;
  requiresAuthentication?: boolean;
  requiresAdmin?: boolean;
}

@Component({
  selector: 'app-sidebar',
  imports: [RouterLink, RouterLinkActive],
  templateUrl: './sidebar.html',
  styleUrl: './sidebar.scss'
})
export class SidebarComponent {
  private readonly authState = inject(AuthStateService);
  private readonly allItems: SidebarItem[] = [
    { label: 'Anasayfa', icon: 'pi pi-home', route: '/dashboard', requiresAuthentication: true },
    { label: 'Katalog', icon: 'pi pi-book', route: '/books' },
    { label: 'Ödünç Aldıklarım', icon: 'pi pi-bookmark', route: '/my-books', requiresAuthentication: true },
    { label: 'Ödünç Geçmişim', icon: 'pi pi-history', route: '/borrow-history', requiresAuthentication: true },
    {
      label: 'Kullanıcılar',
      icon: 'pi pi-users',
      route: '/admin/users',
      requiresAuthentication: true,
      requiresAdmin: true
    },
    {
      label: 'Gecikmiş Ödünçler',
      icon: 'pi pi-exclamation-triangle',
      route: '/admin/overdue-borrows',
      requiresAuthentication: true,
      requiresAdmin: true
    }
  ];

  protected readonly items = computed(() =>
    this.allItems.filter(
      (item) =>
        (!item.requiresAuthentication || this.authState.isAuthenticated()) &&
        (!item.requiresAdmin || this.authState.isAdmin())
    )
  );
}
