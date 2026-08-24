import { BorrowedBook } from './models/borrowed-book.model';

export type BorrowDueDateSeverity = 'warn' | 'danger';

export interface BorrowDueDateDisplay {
  remainingDays: number;
  label: string;
  severity: BorrowDueDateSeverity;
}

const millisecondsPerDay = 24 * 60 * 60 * 1000;

export function getUpcomingBorrowDueDisplay(
  borrowedBook: BorrowedBook,
  now = new Date()
): BorrowDueDateDisplay | null {
  if (borrowedBook.status !== 'Borrowed') {
    return null;
  }

  const remainingDays = getRemainingCalendarDays(borrowedBook.dueDate, now);

  if (!Number.isFinite(remainingDays) || remainingDays < 0 || remainingDays > 2) {
    return null;
  }

  return {
    remainingDays,
    label: remainingDays === 0 ? 'Bugün teslim' : `${remainingDays} gün kaldı`,
    severity: remainingDays === 0 ? 'danger' : 'warn'
  };
}

function getRemainingCalendarDays(dueDateValue: string, now: Date): number {
  const dueDate = new Date(dueDateValue);

  if (Number.isNaN(dueDate.getTime())) {
    return Number.NaN;
  }

  return Math.round((startOfLocalDay(dueDate) - startOfLocalDay(now)) / millisecondsPerDay);
}

function startOfLocalDay(date: Date): number {
  return new Date(date.getFullYear(), date.getMonth(), date.getDate()).getTime();
}
