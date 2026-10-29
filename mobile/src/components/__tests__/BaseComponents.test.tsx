import { fireEvent, render, screen } from '@testing-library/react-native';
import { Text as NativeText } from 'react-native';
import { useAppearanceStore } from '../../appearance/appearanceStore';
import { DARK_PALETTE, LIGHT_PALETTE } from '../../theme/palettes';
import { BOOKING_STATUSES } from '../../constants/booking';
import { STATUS_COLOR_ROLES } from '../../utils/statusColors';
import { AmountRow } from '../AmountRow';
import { AppCard } from '../AppCard';
import { AppThemeProvider } from '../AppThemeProvider';
import { DurationPicker } from '../DurationPicker';
import { FormInput } from '../FormInput';
import { Placeholder } from '../Placeholder';
import { ScreenLoader } from '../ScreenLoader';
import { ScreenMessage } from '../ScreenMessage';
import { StatusBadge } from '../StatusBadge';

/**
 * Tells whether some element of the rendered tree is painted with a background color.
 * @param color Color to look for, as #rrggbb.
 * @returns True when an element has it.
 */
function isPaintedWith(color: string): boolean {
  return JSON.stringify(screen.toJSON()).includes(`"backgroundColor":"${color}"`);
}

describe('the base components', () => {
  beforeEach(() => {
    useAppearanceStore.setState({ preference: 'light' });
  });

  describe('FormInput', () => {
    it('is found by its label and passes the typed text on', () => {
      const onChangeText = jest.fn();
      render(<FormInput label="Cliente" value="María" onChangeText={onChangeText} keyboardType="phone-pad" />);

      const input = screen.getByLabelText('Cliente');
      fireEvent.changeText(input, 'Marta');

      expect(input.props.value).toBe('María');
      expect(input.props.keyboardType).toBe('phone-pad');
      expect(onChangeText).toHaveBeenCalledWith('Marta');
    });

    it('shows the validation message below the field', () => {
      render(<FormInput label="Correo" value="" onChangeText={jest.fn()} error="Escribe un correo válido." />);

      expect(screen.getByText('Escribe un correo válido.')).toBeTruthy();
    });

    it('shows no message when the field is valid', () => {
      render(<FormInput label="Correo" value="" onChangeText={jest.fn()} />);

      expect(screen.queryByText('Escribe un correo válido.')).toBeNull();
    });

    it('does not take what is typed while it is read-only', () => {
      const onChangeText = jest.fn();
      render(<FormInput label="Extras (₡)" value="₡0" editable={false} onChangeText={onChangeText} />);

      fireEvent.changeText(screen.getByLabelText('Extras (₡)'), 'x');

      expect(onChangeText).not.toHaveBeenCalled();
    });
  });

  describe('DurationPicker', () => {
    it('offers every duration with a label for the screen reader', () => {
      render(<DurationPicker label="Duración" options={[1, 2, 3, 4]} value={2} onChange={jest.fn()} />);

      expect(screen.getByText('Duración')).toBeTruthy();
      expect(screen.getByLabelText('1 hora')).toBeTruthy();
      expect(screen.getByLabelText('2 horas')).toBeTruthy();
      expect(screen.getByLabelText('4 horas')).toBeTruthy();
    });

    it('hands the new duration over as a number', () => {
      const onChange = jest.fn();
      render(<DurationPicker label="Duración" options={[1, 2, 3, 4]} value={2} onChange={onChange} />);

      fireEvent.press(screen.getByLabelText('3 horas'));

      expect(onChange).toHaveBeenCalledWith(3);
    });
  });

  describe('AppCard', () => {
    it('holds its content', () => {
      render(
        <AppCard>
          <NativeText>dentro</NativeText>
        </AppCard>,
      );

      expect(screen.getByText('dentro')).toBeTruthy();
    });

    it('is painted with the tonal color of the light theme', () => {
      render(
        <AppThemeProvider>
          <AppCard>
            <NativeText>dentro</NativeText>
          </AppCard>
        </AppThemeProvider>,
      );

      expect(isPaintedWith(LIGHT_PALETTE.surfaceVariant)).toBe(true);
    });

    it('is painted with the tonal color of the dark theme when the photographer chooses it', () => {
      useAppearanceStore.setState({ preference: 'dark' });

      render(
        <AppThemeProvider>
          <AppCard>
            <NativeText>dentro</NativeText>
          </AppCard>
        </AppThemeProvider>,
      );

      expect(isPaintedWith(DARK_PALETTE.surfaceVariant)).toBe(true);
      expect(isPaintedWith(LIGHT_PALETTE.surfaceVariant)).toBe(false);
    });
  });

  describe('StatusBadge', () => {
    it.each(BOOKING_STATUSES)('marks %s with a dot of the color its role has in the palette', (status) => {
      render(<StatusBadge status={status} />);

      expect(isPaintedWith(LIGHT_PALETTE[STATUS_COLOR_ROLES[status]])).toBe(true);
    });

    it('marks a status with another color in the dark theme', () => {
      useAppearanceStore.setState({ preference: 'dark' });

      render(<StatusBadge status="Confirmed" />);

      expect(isPaintedWith(DARK_PALETTE.success)).toBe(true);
      expect(isPaintedWith(LIGHT_PALETTE.success)).toBe(false);
    });

    it('uses a role that exists in both palettes for every status', () => {
      for (const status of BOOKING_STATUSES) {
        const role = STATUS_COLOR_ROLES[status];
        expect(LIGHT_PALETTE[role]).toMatch(/^#[0-9a-f]{6}$/);
        expect(DARK_PALETTE[role]).toMatch(/^#[0-9a-f]{6}$/);
      }
    });
  });

  describe('screen states', () => {
    it('ScreenLoader tells the screen reader what is loading', () => {
      render(<ScreenLoader accessibilityLabel="Cargando reserva" />);

      expect(screen.getByLabelText('Cargando reserva')).toBeTruthy();
    });

    it('ScreenMessage shows its message', () => {
      render(<ScreenMessage message="No se pudo cargar." />);

      expect(screen.getByText('No se pudo cargar.')).toBeTruthy();
    });

    it('Placeholder shows its message and a retry button only when it can retry', () => {
      const onRetry = jest.fn();
      const { rerender } = render(<Placeholder message="Sin conexión." />);
      expect(screen.queryByText('Reintentar')).toBeNull();

      rerender(<Placeholder message="Sin conexión." onRetry={onRetry} />);
      fireEvent.press(screen.getByRole('button', { name: 'Reintentar' }));

      expect(screen.getByText('Sin conexión.')).toBeTruthy();
      expect(onRetry).toHaveBeenCalledTimes(1);
    });
  });

  describe('AmountRow', () => {
    it('shows the label and the amount in colones', () => {
      render(<AmountRow label="Anticipo" money={{ amount: 50000, currency: 'CRC' }} />);

      expect(screen.getByText('Anticipo')).toBeTruthy();
      expect(screen.getByText(/50\D*000/)).toBeTruthy();
    });

    it('shows an emphasized row too', () => {
      render(<AmountRow label="Saldo" money={{ amount: 1000, currency: 'CRC' }} emphasized />);

      expect(screen.getByText('Saldo')).toBeTruthy();
    });
  });
});
