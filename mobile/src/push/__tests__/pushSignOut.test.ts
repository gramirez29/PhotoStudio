import { signOut } from '../../session/sessionManager';
import { unregisterPushDevice } from '../pushManager';
import { signOutOfDevice } from '../pushSignOut';

jest.mock('../../session/sessionManager', () => ({ signOut: jest.fn() }));
jest.mock('../pushManager', () => ({ unregisterPushDevice: jest.fn() }));

describe('signOutOfDevice', () => {
  beforeEach(() => jest.clearAllMocks());

  it('forgets the phone while the session still exists, and only then ends the session', async () => {
    const order: string[] = [];
    jest.mocked(unregisterPushDevice).mockImplementation(() => {
      order.push('unregister');
      return Promise.resolve();
    });
    jest.mocked(signOut).mockImplementation(() => {
      order.push('signOut');
      return Promise.resolve();
    });

    await signOutOfDevice();

    expect(order).toEqual(['unregister', 'signOut']);
  });
});
