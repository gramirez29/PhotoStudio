import { render } from '@testing-library/react-native';
import { act } from 'react';
import { openBooking } from '../../navigation/appNavigation';
import { configureForegroundNotifications, registerPushDevice, subscribeToPushEvents } from '../../push/pushManager';
import { useSessionStore } from '../../session/sessionStore';
import type { PushEventHandlers } from '../../types/push/pushManager.types';
import { createQueryWrapper } from '../../hooks/__fixtures__/queryWrapper';
import { PushRegistration } from '../PushRegistration';

jest.mock('../../navigation/appNavigation', () => ({ openBooking: jest.fn() }));
jest.mock('../../push/pushManager', () => ({
  configureForegroundNotifications: jest.fn(),
  registerPushDevice: jest.fn(() => Promise.resolve('registered')),
  subscribeToPushEvents: jest.fn(),
}));

/** A signed-in session for the store. */
const session = {
  accessToken: 'a',
  accessTokenExpiresAt: Date.now() + 600_000,
  photographerId: 'p1',
  username: 'ana',
  email: 'ana@example.com',
  name: 'Ana',
  isSystemOperator: false,
} as const;

describe('PushRegistration', () => {
  const { client, Wrapper } = createQueryWrapper();
  const unsubscribe = jest.fn();

  beforeEach(() => {
    jest.clearAllMocks();
    useSessionStore.getState().setSignedOut();
    jest.mocked(subscribeToPushEvents).mockReturnValue(unsubscribe);
  });

  afterAll(() => client.clear());

  it('shows the notifications that arrive while the app is open, from the start', () => {
    render(<PushRegistration />, { wrapper: Wrapper });

    expect(configureForegroundNotifications).toHaveBeenCalledTimes(1);
  });

  it('does nothing while the photographer is signed out', () => {
    render(<PushRegistration />, { wrapper: Wrapper });

    expect(registerPushDevice).not.toHaveBeenCalled();
    expect(subscribeToPushEvents).not.toHaveBeenCalled();
  });

  it('registers the phone and listens as soon as the photographer signs in', () => {
    render(<PushRegistration />, { wrapper: Wrapper });

    act(() => useSessionStore.getState().setSignedIn(session));

    expect(registerPushDevice).toHaveBeenCalledTimes(1);
    expect(subscribeToPushEvents).toHaveBeenCalledTimes(1);
  });

  it('stops listening when the photographer signs out', () => {
    render(<PushRegistration />, { wrapper: Wrapper });
    act(() => useSessionStore.getState().setSignedIn(session));

    act(() => useSessionStore.getState().setSignedOut());

    expect(unsubscribe).toHaveBeenCalledTimes(1);
  });

  it('opens the booking of a tapped notification and reloads the inbox when one arrives', () => {
    const invalidate = jest.spyOn(client, 'invalidateQueries');
    render(<PushRegistration />, { wrapper: Wrapper });
    act(() => useSessionStore.getState().setSignedIn(session));
    const handlers = jest.mocked(subscribeToPushEvents).mock.calls[0]?.[0] as PushEventHandlers;

    handlers.onOpenBooking('b1');
    handlers.onReceived();

    expect(openBooking).toHaveBeenCalledWith('b1');
    expect(invalidate).toHaveBeenCalledWith({ queryKey: ['notifications'] });
  });
});
