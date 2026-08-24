import { Component } from '@angular/core';
import { RouterOutlet } from '@angular/router';

import { SidebarComponent } from '../sidebar/sidebar';
import { TopbarComponent } from '../topbar/topbar';

@Component({
  selector: 'app-layout',
  imports: [RouterOutlet, SidebarComponent, TopbarComponent],
  templateUrl: './app-layout.html',
  styleUrl: './app-layout.scss'
})
export class AppLayoutComponent {
  protected sidebarVisible = false;

  protected openSidebar(): void {
    this.sidebarVisible = true;
  }

  protected closeSidebar(): void {
    this.sidebarVisible = false;
  }
}
