import { MaterialIcons } from '@expo/vector-icons';
import { render, screen } from '@testing-library/react-native';
import { Image, StyleSheet } from 'react-native';
import { LIGHT_PALETTE } from '../../theme/palettes';
import { HeaderBrand } from '../HeaderBrand';
import { SignOutButton } from '../SignOutButton';

jest.mock('../../push/pushSignOut', () => ({ signOutOfDevice: jest.fn() }));

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
    it('is an icon of leaving, in the color of the text of the band, and has no text of its own', () => {
      render(<SignOutButton />);

      const icon = screen.UNSAFE_getByType(MaterialIcons);
      expect(icon.props.name).toBe('logout');
      expect(icon.props.color).toBe(LIGHT_PALETTE.onBrand);
      expect(screen.queryByText('Salir')).toBeNull();
    });

    it('is named for the screen reader, and the drawing is hidden from it', () => {
      render(<SignOutButton />);

      expect(screen.getByRole('button', { name: 'Cerrar sesión' })).toBeTruthy();
      expect(screen.UNSAFE_getByType(MaterialIcons).props.accessible).toBe(false);
    });
  });
});
