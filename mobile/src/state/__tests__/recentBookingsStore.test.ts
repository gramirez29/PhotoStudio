import { MAX_RECENT_BOOKINGS, useRecentBookingsStore } from '../recentBookingsStore';

describe('useRecentBookingsStore', () => {
  beforeEach(() => {
    useRecentBookingsStore.getState().clear();
  });

  it('puts the latest booking first without duplicates', () => {
    const { addBooking } = useRecentBookingsStore.getState();

    addBooking('a');
    addBooking('b');
    addBooking('a');

    expect(useRecentBookingsStore.getState().bookingIds).toEqual(['a', 'b']);
  });

  it(`keeps at most ${MAX_RECENT_BOOKINGS} bookings`, () => {
    const { addBooking } = useRecentBookingsStore.getState();

    for (let index = 0; index < MAX_RECENT_BOOKINGS + 2; index += 1) {
      addBooking(`booking-${index}`);
    }

    const { bookingIds } = useRecentBookingsStore.getState();
    expect(bookingIds).toHaveLength(MAX_RECENT_BOOKINGS);
    expect(bookingIds[0]).toBe(`booking-${MAX_RECENT_BOOKINGS + 1}`);
  });
});
