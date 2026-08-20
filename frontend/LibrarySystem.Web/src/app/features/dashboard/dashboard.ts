import { Component } from '@angular/core';
import { RouterLink } from '@angular/router';
import { ButtonModule } from 'primeng/button';
import { CardModule } from 'primeng/card';
import { InputTextModule } from 'primeng/inputtext';
import { TagModule } from 'primeng/tag';

interface BookPreview {
  title: string;
  author: string;
  meta: string;
  coverClass: string;
}

interface BorrowedPreview {
  title: string;
  author: string;
  dateLabel: string;
  date: string;
  status: string;
  severity: 'success' | 'warn' | 'info';
  coverClass: string;
}

interface SummaryItem {
  label: string;
  value: string;
  icon: string;
  tone: 'gold' | 'teal';
}

@Component({
  selector: 'app-dashboard',
  imports: [ButtonModule, CardModule, InputTextModule, RouterLink, TagModule],
  templateUrl: './dashboard.html',
  styleUrl: './dashboard.scss'
})
export class DashboardComponent {
  protected readonly popularBooks: BookPreview[] = [
    { title: 'Nutuk', author: 'M. K. Atatürk', meta: '12 stokta', coverClass: 'cover--navy' },
    { title: 'Şeker Portakalı', author: 'J. M. de Vasconcelos', meta: '8 stokta', coverClass: 'cover--gold' },
    {
      title: 'Saatleri Ayarlama Enstitüsü',
      author: 'A. H. Tanpınar',
      meta: '5 stokta',
      coverClass: 'cover--teal'
    },
    { title: 'Ben, Robot', author: 'Isaac Asimov', meta: '9 stokta', coverClass: 'cover--clay' }
  ];

  protected readonly currentLoans: BorrowedPreview[] = [
    {
      title: 'Kürk Mantolu Madonna',
      author: 'Sabahattin Ali',
      dateLabel: 'Alış',
      date: '14.08.2026',
      status: 'Devam ediyor',
      severity: 'info',
      coverClass: 'cover--gold'
    },
    {
      title: '1984',
      author: 'George Orwell',
      dateLabel: 'Alış',
      date: '10.08.2026',
      status: 'Yakında',
      severity: 'warn',
      coverClass: 'cover--navy'
    },
    {
      title: 'Dune',
      author: 'Frank Herbert',
      dateLabel: 'Alış',
      date: '06.08.2026',
      status: 'Zamanında',
      severity: 'success',
      coverClass: 'cover--clay'
    }
  ];

  protected readonly borrowedBooks: BorrowedPreview[] = [
    {
      title: 'Domain Driven Design',
      author: 'Eric Evans',
      dateLabel: 'Teslim',
      date: '26.08.2026',
      status: 'Aktif',
      severity: 'info',
      coverClass: 'cover--teal'
    },
    {
      title: 'Refactoring',
      author: 'Martin Fowler',
      dateLabel: 'Teslim',
      date: '23.08.2026',
      status: 'Yakında',
      severity: 'warn',
      coverClass: 'cover--navy'
    },
    {
      title: 'Clean Architecture',
      author: 'Robert C. Martin',
      dateLabel: 'Teslim',
      date: '29.08.2026',
      status: 'Aktif',
      severity: 'success',
      coverClass: 'cover--gold'
    }
  ];

  // Mock values for the visual dashboard shell; real metrics will come from the API later.
  protected readonly summaryItems: SummaryItem[] = [
    { label: 'Toplam Kitap', value: '1.248', icon: 'pi pi-book', tone: 'gold' },
    { label: 'Stokta Bulunan', value: '932', icon: 'pi pi-check-circle', tone: 'teal' },
    { label: 'Aktif Ödünç', value: '27', icon: 'pi pi-bookmark', tone: 'gold' }
  ];
}
