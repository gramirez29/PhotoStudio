import { fireEvent, render, screen } from '@testing-library/react-native';
import { Image, StyleSheet } from 'react-native';
import { openNewBooking } from '../../navigation/appNavigation';
import { LIGHT_PALETTE } from '../../theme/palettes';
import { HeaderBrand } from '../HeaderBrand';
import { NewBookingFab } from '../NewBookingFab';
import { SignOutButton } from '../SignOutButton';

jest.mock('../../navigation/appNavigation', () => ({ openNewBooking: jest.fn() }));
jest.mock('../../push/pushSignOut', () => ({ signOutOfDevice: jest.fn() }));
jest.mock('react-native-safe-area-context', () => ({
  useSafeAreaInsets: () => ({ top: 24, bottom: 20, left: 0, right: 0 }),
}));

describe('the structure of the app', () => {
  describe('HeaderBrand', () => {
    it('names the studio for the screen reader and shows its logo as decoration', () => {
      render(<HeaderBrand />);

      expect(screen.getByLabelText('PhotoSense Studio')).toBeTruthy();
      expect(screen.UNSAFE_getByType(Image).props.accessible).toBe(false);
    });

    it('writes the name in capitals in the color of the text of the band', () => {
      render(<HeaderBrand />);

      const name = screen.getByText('PHOTOSENSE STUDIO', { includeHiddenElements: true });
      expect(StyleSheet.flatten(name.props.style)).toMatchObject({ color: LIGHT_PALETTE.onBrand });
    });

    it('sets the logo apart with the silver border of the mark', () => {
      render(<HeaderBrand />);

      expect(StyleSheet.flatten(screen.UNSAFE_getByType(Image).props.style)).toMatchObject({
        borderColor: LIGHT_PALETTE.silver,
        borderWidth: 2,
      });
    });
  });

  describe('SignOutButton on the band', () => {
    it('writes its label in the color of the text of the band', () => {
      render(<SignOutButton />);

      const label = screen.getByText('Salir');
      expect(StyleSheet.flatten(label.props.style)).toMatchObject({ color: LIGHT_PALETTE.onBrand });
    });
  });

  describe('NewBookingFab', () => {
    beforeEach(() => {
      jest.clearAllMocks();
    });

    it('offers to create a booking, with its name for the screen reader', () => {
      render(<NewBookingFab />);

      expect(screen.getByText('Nueva reserva')).toBeTruthy();
      expect(screen.getByLabelText('Nueva reserva')).toBeTruthy();
    });

    it('opens the create-booking form when it is tapped', () => {
      render(<NewBookingFab />);

      fireEvent.press(screen.getByLabelText('Nueva reserva'));

      expect(openNewBooking).toHaveBeenCalledTimes(1);
    });

    it('floats at the right, above the system navigation bar', () => {
      render(<NewBookingFab />);

      const fab = screen.getByLabelText('Nueva reserva');
      const flat = StyleSheet.flatten(fab.props.style);
      const placed = JSON.stringify(screen.toJSON());
      expect(flat).toBeDefined();
      expect(placed).toContain('"bottom":36');
      expect(placed).toContain('"position":"absolute"');
    });
  });
});
