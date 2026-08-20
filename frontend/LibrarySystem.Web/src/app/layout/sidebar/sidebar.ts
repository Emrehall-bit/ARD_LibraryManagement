import { Component } from '@angular/core';
import { RouterLink, RouterLinkActive } from '@angular/router';

interface SidebarItem {
  label: string;
  icon: string;
  route: string;
}

@Component({
  selector: 'app-sidebar',
  imports: [RouterLink, RouterLinkActive],
  templateUrl: './sidebar.html',
  styleUrl: './sidebar.scss'
})
export class SidebarComponent {
  protected readonly items: SidebarItem[] = [
    { label: 'Anasayfa', icon: 'pi pi-home', route: '/dashboard' },
    { label: 'Katalog', icon: 'pi pi-book', route: '/books' },
    { label: 'Ödünç Aldıklarım', icon: 'pi pi-bookmark', route: '/my-books' }
  ];
}
