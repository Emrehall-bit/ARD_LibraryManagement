import { Component, computed, inject } from '@angular/core';
import { RouterLink, RouterLinkActive } from '@angular/router';

import { AuthStateService } from '../../core/auth/auth-state.service';

interface SidebarItem {
  label: string;
  icon: string;
  route: string;
  requiresAuthentication?: boolean;
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
    { label: 'Ödünç Aldıklarım', icon: 'pi pi-bookmark', route: '/my-books', requiresAuthentication: true }
  ];

  protected readonly items = computed(() =>
    this.allItems.filter((item) => !item.requiresAuthentication || this.authState.isAuthenticated())
  );
}
