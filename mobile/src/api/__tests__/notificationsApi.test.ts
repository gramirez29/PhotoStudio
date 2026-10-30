import { ResponseShapeError } from '../guards';
import { createHttpClient } from '../httpClient';
import { createNotificationsApi, parseMarkAllRead, parseNotification, parseNotificationList } from '../notificationsApi';
import { jsonResponse, stubFetch } from '../__fixtures__/testResponses';

const reminder = {
  id: 'n1',
  type: 'SessionReminder',
  bookingId: 'b1',
  clientName: 'María Pérez',
  clientPhone: '+50670189220',
  packageName: 'Retrato Familiar',
  sessionStart: '2026-10-17T21:00:00+00:00',
  balance: null,
  proofAmount: null,
  deliveredAt: '2026-10-16T21:00:00+00:00',
  readAt: null,
} as const;

describe('parseNotification', () => {
  it('parses a reminder and a balance notice', () => {
    expect(parseNotification(reminder)).toEqual(reminder);
    expect(
      parseNotification({ ...reminder, type: 'BalanceDue', balance: { amount: 50000, currency: 'CRC' }, readAt: '2026-10-17T00:00:00+00:00' }),
    ).toMatchObject({ type: 'BalanceDue', balance: { amount: 50000, currency: 'CRC' }, readAt: '2026-10-17T00:00:00+00:00' });
  });

  it('parses a proof notice with its declared amount', () => {
    expect(
      parseNotification({ ...reminder, type: 'PaymentProofSubmitted', proofAmount: { amount: 25000, currency: 'CRC' } }),
    ).toMatchObject({ type: 'PaymentProofSubmitted', proofAmount: { amount: 25000, currency: 'CRC' }, balance: null });
    expect(() => parseNotification({ ...reminder, proofAmount: { amount: 'x', currency: 'CRC' } })).toThrow(ResponseShapeError);
  });

  it('rejects an unknown type or a mistyped field', () => {
    expect(() => parseNotification({ ...reminder, type: 'Other' })).toThrow('notification.type');
    expect(() => parseNotification({ ...reminder, clientName: 3 })).toThrow(ResponseShapeError);
    expect(() => parseNotification(null)).toThrow(ResponseShapeError);
  });
});

describe('parseNotificationList and parseMarkAllRead', () => {
  it('parses the inbox with its unread counter', () => {
    expect(parseNotificationList({ items: [reminder], unreadCount: 1, nextCursor: '9.abc' })).toEqual({
      items: [reminder],
      unreadCount: 1,
      nextCursor: '9.abc',
    });
    expect(parseNotificationList({ items: [], unreadCount: 0, nextCursor: null }).nextCursor).toBeNull();
  });

  it('points at the element that is invalid', () => {
    expect(() => parseNotificationList({ items: [{ ...reminder, id: 1 }], unreadCount: 1, nextCursor: null })).toThrow(
      'notifications.items[0].id',
    );
    expect(() => parseNotificationList({ items: [], unreadCount: '1', nextCursor: null })).toThrow('notifications.unreadCount');
    expect(() => parseNotificationList({ items: [], unreadCount: 1, nextCursor: 5 })).toThrow('notifications.nextCursor');
    expect(parseNotificationList({ items: [], unreadCount: 1 }).nextCursor).toBeNull();
  });

  it('parses how many were marked', () => {
    expect(parseMarkAllRead({ marked: 2 })).toEqual({ marked: 2 });
    expect(() => parseMarkAllRead({})).toThrow('markAllRead.marked');
  });
});

describe('createNotificationsApi', () => {
  it('lists the inbox with GET', async () => {
    const { fetchFn, urls } = stubFetch(jsonResponse(200, { items: [reminder], unreadCount: 1, nextCursor: null }));
    const api = createNotificationsApi(createHttpClient('https://api.example.test', { fetchFn }));

    await expect(api.listNotifications(null)).resolves.toEqual({ items: [reminder], unreadCount: 1, nextCursor: null });
    expect(urls).toEqual(['https://api.example.test/api/notifications?limit=5']);
  });

  it('asks for the next page with the cursor of the previous one', async () => {
    const { fetchFn, urls } = stubFetch(jsonResponse(200, { items: [], unreadCount: 0, nextCursor: null }));
    const api = createNotificationsApi(createHttpClient('https://api.example.test', { fetchFn }));

    await api.listNotifications('9.abc');

    expect(urls).toEqual(['https://api.example.test/api/notifications?limit=5&cursor=9.abc']);
  });

  it('marks one notification as read with POST', async () => {
    const { fetchFn, urls } = stubFetch(jsonResponse(200, { ...reminder, readAt: '2026-10-17T00:00:00+00:00' }));
    const api = createNotificationsApi(createHttpClient('https://api.example.test', { fetchFn }));

    await expect(api.markRead('n1')).resolves.toMatchObject({ id: 'n1', readAt: '2026-10-17T00:00:00+00:00' });
    expect(urls).toEqual(['https://api.example.test/api/notifications/n1/read']);
  });

  it('marks the whole inbox as read with POST', async () => {
    const { fetchFn, urls } = stubFetch(jsonResponse(200, { marked: 3 }));
    const api = createNotificationsApi(createHttpClient('https://api.example.test', { fetchFn }));

    await expect(api.markAllRead()).resolves.toEqual({ marked: 3 });
    expect(urls).toEqual(['https://api.example.test/api/notifications/read-all']);
  });
});
