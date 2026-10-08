import type { BookingStatus } from '../types/api/booking';
import type { ColorToken } from '../types/theme/tokens.types';

/** Color token used for each booking status. */
export const STATUS_COLOR_TOKENS: Readonly<Record<BookingStatus, ColorToken>> = {
  Tentative: 'warning',
  Confirmed: 'success',
  Completed: 'info',
  Cancelled: 'muted',
  Expired: 'muted',
  ClientAbsent: 'danger',
};
