import { MaterialIcons } from '@expo/vector-icons';
import { act, fireEvent, render, screen } from '@testing-library/react-native';
import { BackHandler, StyleSheet } from 'react-native';
import { SafeAreaProvider } from 'react-native-safe-area-context';
import { useAppearanceStore } from '../../appearance/appearanceStore';
import { openNewBooking } from '../../navigation/appNavigation';
import { DARK_PALETTE, LIGHT_PALETTE } from '../../theme/palettes';
import { NewBookingFab } from '../NewBookingFab';

jest.mock('../../navigation/appNavigation', () => ({ openNewBooking: jest.fn() }));

/** Metrics of a phone whose navigation bar covers the last 20 points of the screen. */
const METRICS = { frame: { x: 0, y: 0, width: 360, height: 780 }, insets: { top: 24, left: 0, right: 0, bottom: 20 } };

/**
 * Renders the button on a phone with a navigation bar.
 * @returns The rendered tree.
 */
function renderFab(): ReturnType<typeof render> {
  return render(
    <SafeAreaProvider initialMetrics={METRICS}>
      <NewBookingFab />
    </SafeAreaProvider>,
  );
}

describe('NewBookingFab', () => {
  beforeEach(() => {
    jest.clearAllMocks();
    useAppearanceStore.setState({ preference: 'light' });
  });

  it('starts as a round button with a plus and nothing else', () => {
    renderFab();

    expect(screen.getByLabelText('Nueva reserva')).toBeTruthy();
    expect(screen.UNSAFE_getByType(MaterialIcons).props.name).toBe('add');
    expect(screen.queryByText('Agregar reserva')).toBeNull();
    expect(screen.queryByLabelText('Cerrar el menú')).toBeNull();
    expect(openNewBooking).not.toHaveBeenCalled();
  });

  it('shows the option "Agregar reserva" when it is tapped, without opening the form yet', () => {
    renderFab();

    fireEvent.press(screen.getByLabelText('Nueva reserva'));

    expect(screen.getByText('Agregar reserva')).toBeTruthy();
    expect(openNewBooking).not.toHaveBeenCalled();
  });

  it('turns the plus into a cross while the menu is open, and says so to the screen reader', () => {
    renderFab();

    fireEvent.press(screen.getByLabelText('Nueva reserva'));

    const icons = screen.UNSAFE_getAllByType(MaterialIcons).map((icon) => icon.props.name);
    expect(icons).toContain('close');
    expect(screen.getByLabelText('Cerrar el menú de reservas').props.accessibilityState).toMatchObject({ expanded: true });
  });

  it('opens the create-booking form from the option, and closes the menu', () => {
    renderFab();
    fireEvent.press(screen.getByLabelText('Nueva reserva'));

    fireEvent.press(screen.getByLabelText('Agregar reserva'));

    expect(openNewBooking).toHaveBeenCalledTimes(1);
    expect(screen.queryByText('Agregar reserva')).toBeNull();
    expect(screen.getByLabelText('Nueva reserva')).toBeTruthy();
  });

  it('dims the rest of the screen well, so the option stands out', () => {
    renderFab();

    fireEvent.press(screen.getByLabelText('Nueva reserva'));

    const dimming = screen.getByRole('button', { name: 'Cerrar el menú' });
    expect(StyleSheet.flatten(dimming.props.style).backgroundColor).toBe('rgba(11, 26, 64, 0.7)');
  });

  it('closes the menu, without opening anything, from the cross', () => {
    renderFab();
    fireEvent.press(screen.getByLabelText('Nueva reserva'));

    fireEvent.press(screen.getByLabelText('Cerrar el menú de reservas'));

    expect(screen.queryByText('Agregar reserva')).toBeNull();
    expect(openNewBooking).not.toHaveBeenCalled();
  });

  it('closes the menu, without opening anything, from the dimmed screen', () => {
    renderFab();
    fireEvent.press(screen.getByLabelText('Nueva reserva'));

    fireEvent.press(screen.getByRole('button', { name: 'Cerrar el menú' }));

    expect(screen.queryByText('Agregar reserva')).toBeNull();
    expect(openNewBooking).not.toHaveBeenCalled();
  });

  it('closes the menu with the back button of the phone instead of leaving the screen', () => {
    const listeners: (() => boolean | null | undefined)[] = [];
    const remove = jest.fn();
    jest.spyOn(BackHandler, 'addEventListener').mockImplementation((_event, listener) => {
      // The back button handler takes no argument in practice; the type of the event is only for the listener signature.
      listeners.push(listener as unknown as () => boolean | null | undefined);
      return { remove };
    });
    renderFab();
    fireEvent.press(screen.getByLabelText('Nueva reserva'));
    expect(listeners).toHaveLength(1);

    let handled = false;
    act(() => {
      handled = listeners[0]?.() === true;
    });

    expect(handled).toBe(true);
    expect(screen.queryByText('Agregar reserva')).toBeNull();
    expect(remove).toHaveBeenCalled();
  });

  it('listens to the back button only while the menu is open', () => {
    const add = jest.spyOn(BackHandler, 'addEventListener');
    renderFab();
    expect(add).not.toHaveBeenCalled();

    fireEvent.press(screen.getByLabelText('Nueva reserva'));

    expect(add).toHaveBeenCalledTimes(1);
  });

  it('floats at the right, above the navigation bar of the system', () => {
    renderFab();

    const painted = JSON.stringify(screen.toJSON());
    expect(painted).toContain('"bottom":36');
    expect(painted).toContain('"position":"absolute"');
    expect(painted).toContain('"right":16');
  });

  it('is the blue of the logo in the light theme', () => {
    renderFab();

    const painted = JSON.stringify(screen.toJSON());
    expect(painted).toContain(`"backgroundColor":"${LIGHT_PALETTE.primary}"`);
    expect(painted).not.toContain(LIGHT_PALETTE.secondary);
  });

  it('is copper in the dark theme', () => {
    useAppearanceStore.setState({ preference: 'dark' });

    renderFab();

    const painted = JSON.stringify(screen.toJSON());
    expect(painted).toContain(`"backgroundColor":"${DARK_PALETTE.primary}"`);
    expect(painted).not.toContain(LIGHT_PALETTE.primary);
  });
});
