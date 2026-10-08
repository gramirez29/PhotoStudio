import { create } from 'zustand';

/** Maximum number of recently opened bookings kept in memory. */
export const MAX_RECENT_BOOKINGS = 5;

/** Client-side state: bookings the photographer opened recently. Server data lives in TanStack Query, not here. */
export interface RecentBookingsState {
  /** Identifiers of the recently opened bookings, most recent first. */
  readonly bookingIds: readonly string[];
  /**
   * Moves a booking to the top of the list, keeping at most {@link MAX_RECENT_BOOKINGS}.
   * @param bookingId Booking identifier.
   */
  addBooking: (bookingId: string) => void;
  /** Removes every entry. */
  clear: () => void;
}

/** Hook to read and update the recently opened bookings. */
export const useRecentBookingsStore = create<RecentBookingsState>()((set) => ({
  bookingIds: [],
  addBooking: (bookingId) =>
    set((state) => ({
      bookingIds: [bookingId, ...state.bookingIds.filter((existing) => existing !== bookingId)].slice(
        0,
        MAX_RECENT_BOOKINGS,
      ),
    })),
  clear: () => set({ bookingIds: [] }),
}));
