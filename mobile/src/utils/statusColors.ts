import type { BookingStatus } from '../types/api/booking';
import type { StatusColorRole } from '../types/theme/palette.types';

/** Role of the palette that marks each booking status. It is a role, not a color: the theme decides the color. */
export const STATUS_COLOR_ROLES: Readonly<Record<BookingStatus, StatusColorRole>> = {
  Tentative: 'warning',
  Confirmed: 'success',
  Completed: 'info',
  Cancelled: 'muted',
  Expired: 'muted',
  ClientAbsent: 'error',
};
