import { fireEvent, render, screen } from '@testing-library/react-native';
import NotificationsScreen from '../../app/notifications';
import { useNotificationActions } from '../../hooks/useNotificationActions';
import { useNotifications } from '../../hooks/useNotifications';

jest.mock('../../hooks/useNotifications');
jest.mock('../../hooks/useNotificationActions');

/**
 * Builds the data of an inbox query with the given pages.
 * @param pages The unread count of each page; each page has one notice.
 * @returns The query data.
 */
function pagesWithUnread(...pages: number[]): unknown {
  return {
    pages: pages.map((unreadCount, index) => ({
      items: [
        {
          id: `n${index}`,
          type: 'SessionReminder',
          bookingId: `b${index}`,
          clientName: `Cliente ${index}`,
          clientPhone: '+50670189220',
          packageName: 'Retrato Familiar',
          sessionStart: '2026-10-17T21:00:00+00:00',
          balance: null,
          proofAmount: null,
          deliveredAt: '2026-10-16T21:00:00+00:00',
          readAt: null,
        },
      ],
      unreadCount,
      nextCursor: null,
    })),
    pageParams: [null],
  };
}

/**
 * Makes the inbox answer with what the test needs.
 * @param state Parts of the query to replace.
 */
function inboxIs(state: Partial<ReturnType<typeof useNotifications>>): void {
  jest.mocked(useNotifications).mockReturnValue({
    data: pagesWithUnread(2) as never,
    hasNextPage: false,
    isFetchingNextPage: false,
    isFetchNextPageError: false,
    fetchNextPage: jest.fn(),
    isPending: false,
    isError: false,
    isRefetching: false,
    refetch: jest.fn(),
    ...state,
  } as unknown as ReturnType<typeof useNotifications>);
}

describe('NotificationsScreen', () => {
  beforeEach(() => {
    jest.clearAllMocks();
    jest.mocked(useNotificationActions).mockReturnValue({
      sendWhatsApp: jest.fn(),
      openBooking: jest.fn(),
      markRead: jest.fn(),
      markAllRead: jest.fn(),
      isMarkingAll: false,
      error: null,
    } as unknown as ReturnType<typeof useNotificationActions>);
  });

  it('shows the notices of every page loaded and the unread count of the first page', () => {
    inboxIs({ data: pagesWithUnread(3, 1) as never });

    render(<NotificationsScreen />);

    expect(screen.getByText(/Cliente 0/)).toBeTruthy();
    expect(screen.getByText(/Cliente 1/)).toBeTruthy();
    expect(screen.getByText('Marcar todo como leído')).toBeTruthy();
  });

  it('asks for the next page from the "Ver más" button, only while there is one', () => {
    const fetchNextPage = jest.fn();
    inboxIs({ hasNextPage: true, fetchNextPage });
    const { rerender } = render(<NotificationsScreen />);

    fireEvent.press(screen.getByText('Ver más'));
    expect(fetchNextPage).toHaveBeenCalledTimes(1);

    inboxIs({ hasNextPage: false });
    rerender(<NotificationsScreen />);
    expect(screen.queryByText('Ver más')).toBeNull();
  });

  it('says so when there are no notices', () => {
    inboxIs({ data: { pages: [{ items: [], unreadCount: 0, nextCursor: null }], pageParams: [null] } as never });

    render(<NotificationsScreen />);

    expect(screen.getByText(/No tienes avisos/)).toBeTruthy();
  });
});
