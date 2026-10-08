import { fireEvent, render, screen } from '@testing-library/react-native';
import { ActionButton } from '../ActionButton';

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
