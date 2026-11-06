import type { ReplaceableNavigation } from '../../types/navigation/homeSwitch.types';
import { replaceScreenWithHome } from '../homeSwitch';

/**
 * Builds a stand-in for the navigation of a screen.
 * @param names Names of the routes of the stack, from the first.
 * @returns The stand-in, with its dispatch as a mock.
 */
function navigationOf(names: string[]): ReplaceableNavigation & { dispatch: jest.Mock } {
  return { dispatch: jest.fn(), getState: () => ({ routes: names.map((name) => ({ name })) }) };
}

describe('replaceScreenWithHome', () => {
  it('replaces the agenda under the settings with the list of bookings', () => {
    const navigation = navigationOf(['index', 'settings']);

    replaceScreenWithHome(navigation, 'agenda', 'bookings');

    expect(navigation.dispatch).toHaveBeenCalledWith({ type: 'REPLACE', payload: { name: 'bookings/index' } });
  });

  it('replaces the list of bookings with the agenda when it is the first screen', () => {
    const navigation = navigationOf(['bookings/index', 'settings']);

    replaceScreenWithHome(navigation, 'bookings', 'agenda');

    expect(navigation.dispatch).toHaveBeenCalledWith({ type: 'REPLACE', payload: { name: 'index' } });
  });

  it('leaves alone a list of bookings that was opened on top of the agenda', () => {
    const navigation = navigationOf(['index', 'bookings/index', 'settings']);

    replaceScreenWithHome(navigation, 'bookings', 'agenda');

    expect(navigation.dispatch).not.toHaveBeenCalled();
  });

  it('does not make a second copy of the target', () => {
    const navigation = navigationOf(['index', 'bookings/index', 'settings']);

    replaceScreenWithHome(navigation, 'agenda', 'bookings');

    expect(navigation.dispatch).not.toHaveBeenCalled();
  });
});
