import { fireEvent, render, screen } from '@testing-library/react-native';
import { LIGHT_PALETTE } from '../../theme/palettes';
import { ActionButton } from '../ActionButton';
import { AppThemeProvider } from '../AppThemeProvider';

describe('ActionButton', () => {
  it('shows its label and calls onPress when tapped', () => {
    const onPress = jest.fn();
    render(<ActionButton label="Reprogramar" onPress={onPress} />);

    fireEvent.press(screen.getByText('Reprogramar'));

    expect(onPress).toHaveBeenCalledTimes(1);
  });

  it('ignores taps and tells screen readers it is disabled when disabled', () => {
    const onPress = jest.fn();
    render(<ActionButton label="Cancelar" onPress={onPress} disabled variant="danger" />);

    fireEvent.press(screen.getByText('Cancelar'));

    expect(onPress).not.toHaveBeenCalled();
    expect(screen.getByRole('button', { disabled: true })).toBeTruthy();
  });
});

describe('ActionButton colors', () => {
  it('fills the main action with the main color of the app, which is not red', () => {
    render(
      <AppThemeProvider>
        <ActionButton label="Guardar" onPress={jest.fn()} />
      </AppThemeProvider>,
    );

    const painted = JSON.stringify(screen.toJSON());
    expect(painted).toContain(LIGHT_PALETTE.primary);
    expect(painted).not.toContain(LIGHT_PALETTE.error);
  });

  it('outlines a destructive action in red, so it is never mistaken for the main action', () => {
    render(
      <AppThemeProvider>
        <ActionButton label="Cancelar reserva" onPress={jest.fn()} variant="danger" />
      </AppThemeProvider>,
    );

    const painted = JSON.stringify(screen.toJSON());
    const red = LIGHT_PALETTE.error;
    expect(painted).toContain(`"borderColor":"${red}"`);
    expect(painted).toContain(`"color":"${red}"`);
    expect(painted).not.toContain(LIGHT_PALETTE.primary);
  });

  it('shows the label of a destructive action and takes the tap', () => {
    const onPress = jest.fn();
    render(<ActionButton label="Cancelar reserva" onPress={onPress} variant="danger" />);

    fireEvent.press(screen.getByText('Cancelar reserva'));

    expect(onPress).toHaveBeenCalledTimes(1);
  });
});
