import { BorrowStatus } from './models/borrowed-book.model';

export type BorrowStatusSeverity = 'success' | 'info' | 'danger' | 'secondary';

interface BorrowStatusDisplay {
  label: string;
  severity: BorrowStatusSeverity;
}

const borrowStatusDisplay: Record<BorrowStatus, BorrowStatusDisplay> = {
  Borrowed: {
    label: 'Ödünçte',
    severity: 'info'
  },
  Returned: {
    label: 'İade Edildi',
    severity: 'secondary'
  },
  Overdue: {
    label: 'Gecikmiş',
    severity: 'danger'
  }
};

export function getBorrowStatusDisplay(status: BorrowStatus): BorrowStatusDisplay {
  return borrowStatusDisplay[status];
}
