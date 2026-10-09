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

  it('rejects an unknown type or a mistyped field', () => {
    expect(() => parseNotification({ ...reminder, type: 'Other' })).toThrow('notification.type');
    expect(() => parseNotification({ ...reminder, clientName: 3 })).toThrow(ResponseShapeError);
    expect(() => parseNotification(null)).toThrow(ResponseShapeError);
  });
});

describe('parseNotificationList and parseMarkAllRead', () => {
  it('parses the inbox with its unread counter', () => {
    expect(parseNotificationList({ items: [reminder], unreadCount: 1 })).toEqual({ items: [reminder], unreadCount: 1 });
  });

  it('points at the element that is invalid', () => {
    expect(() => parseNotificationList({ items: [{ ...reminder, id: 1 }], unreadCount: 1 })).toThrow('notifications.items[0].id');
    expect(() => parseNotificationList({ items: [], unreadCount: '1' })).toThrow('notifications.unreadCount');
  });

  it('parses how many were marked', () => {
    expect(parseMarkAllRead({ marked: 2 })).toEqual({ marked: 2 });
    expect(() => parseMarkAllRead({})).toThrow('markAllRead.marked');
  });
});

describe('createNotificationsApi', () => {
  it('lists the inbox with GET', async () => {
    const { fetchFn, urls } = stubFetch(jsonResponse(200, { items: [reminder], unreadCount: 1 }));
    const api = createNotificationsApi(createHttpClient('https://api.example.test', { fetchFn }));

    await expect(api.listNotifications()).resolves.toEqual({ items: [reminder], unreadCount: 1 });
    expect(urls).toEqual(['https://api.example.test/api/notifications']);
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
