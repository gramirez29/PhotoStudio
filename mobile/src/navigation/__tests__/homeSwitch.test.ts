import type { ReplaceableNavigation } from '../../types/navigation/homeSwitch.types';
import { putHomeUnderneath } from '../homeSwitch';

/**
 * Builds a stand-in for the navigation of a screen.
 * @param names Names of the routes of the stack, from the first.
 * @returns The stand-in, with its reset as a mock.
 */
function navigationOf(names: string[]): ReplaceableNavigation & { reset: jest.Mock } {
  return { reset: jest.fn(), getState: () => ({ routes: names.map((name) => ({ name, key: `${name}-key` })) }) };
}

describe('putHomeUnderneath', () => {
  it('puts the list of bookings under the settings, whatever the stack had', () => {
    const navigation = navigationOf(['index', 'bookings/index', 'settings']);

    putHomeUnderneath(navigation, 'bookings');

    expect(navigation.reset).toHaveBeenCalledWith({
      index: 1,
      routes: [{ name: 'bookings/index' }, { name: 'settings', key: 'settings-key' }],
    });
  });

  it('puts the agenda under the settings', () => {
    const navigation = navigationOf(['bookings/index', 'settings']);

    putHomeUnderneath(navigation, 'agenda');

    expect(navigation.reset).toHaveBeenCalledWith({
      index: 1,
      routes: [{ name: 'index' }, { name: 'settings', key: 'settings-key' }],
    });
  });

  it('does nothing when there is no stack', () => {
    const navigation = { reset: jest.fn(), getState: () => undefined };

    putHomeUnderneath(navigation, 'agenda');

    expect(navigation.reset).not.toHaveBeenCalled();
  });
});
