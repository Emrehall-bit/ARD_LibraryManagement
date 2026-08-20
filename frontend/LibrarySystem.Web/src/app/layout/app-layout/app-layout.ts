import { Component } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { DrawerModule } from 'primeng/drawer';

import { SidebarComponent } from '../sidebar/sidebar';
import { TopbarComponent } from '../topbar/topbar';

@Component({
  selector: 'app-layout',
  imports: [DrawerModule, RouterOutlet, SidebarComponent, TopbarComponent],
  templateUrl: './app-layout.html',
  styleUrl: './app-layout.scss'
})
export class AppLayoutComponent {
  protected sidebarVisible = false;

  protected openSidebar(): void {
    this.sidebarVisible = true;
  }
}
