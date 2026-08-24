import { Injectable } from '@angular/core';
import { HubConnection, HubConnectionBuilder, HubConnectionState } from '@microsoft/signalr';
import { Observable, Subject } from 'rxjs';

import { environment } from '../../../environments/environment';
import { BookStockChanged } from './models/book-stock-changed.model';

@Injectable({
  providedIn: 'root'
})
export class LibraryRealtimeService {
  private readonly stockChangedSubject = new Subject<BookStockChanged>();
  private readonly connection: HubConnection;
  private startPromise: Promise<void> | null = null;

  readonly bookStockChanged$: Observable<BookStockChanged> = this.stockChangedSubject.asObservable();

  constructor() {
    this.connection = new HubConnectionBuilder()
      .withUrl(environment.signalRHubUrl)
      .withAutomaticReconnect()
      .build();

    this.connection.on('BookStockChanged', (message: BookStockChanged) => {
      if (!this.isBookStockChanged(message)) {
        return;
      }

      this.stockChangedSubject.next(message);
    });
  }

  start(): Promise<void> {
    if (
      this.connection.state === HubConnectionState.Connected ||
      this.connection.state === HubConnectionState.Connecting ||
      this.connection.state === HubConnectionState.Reconnecting
    ) {
      return this.startPromise ?? Promise.resolve();
    }

    this.startPromise = this.connection
      .start()
      .catch((error: unknown) => {
        console.warn('Realtime stock connection could not be started.', error);
      })
      .finally(() => {
        this.startPromise = null;
      });

    return this.startPromise;
  }

  private isBookStockChanged(message: unknown): message is BookStockChanged {
    if (!message || typeof message !== 'object') {
      return false;
    }

    const candidate = message as Partial<BookStockChanged>;

    return typeof candidate.bookId === 'string' &&
      candidate.bookId.length > 0 &&
      typeof candidate.stock === 'number' &&
      Number.isInteger(candidate.stock) &&
      candidate.stock >= 0;
  }
}
