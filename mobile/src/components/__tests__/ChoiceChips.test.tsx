import { fireEvent, render, screen } from '@testing-library/react-native';
import { ChoiceChips } from '../ChoiceChips';

const OPTIONS = ['Compromiso', 'Pre-Boda', 'Producto'] as const;

describe('ChoiceChips', () => {
  it('shows the label and every option in the given order', () => {
    render(<ChoiceChips label="Paquete" options={OPTIONS} value="" onChange={jest.fn()} />);

    expect(screen.getByText('Paquete')).toBeTruthy();
    expect(screen.getAllByRole('button').map((chip) => chip.props.accessibilityLabel)).toEqual([...OPTIONS]);
  });

  it('calls onChange with the option that is tapped', () => {
    const onChange = jest.fn();
    render(<ChoiceChips label="Paquete" options={OPTIONS} value="" onChange={onChange} />);

    fireEvent.press(screen.getByText('Pre-Boda'));

    expect(onChange).toHaveBeenCalledTimes(1);
    expect(onChange).toHaveBeenCalledWith('Pre-Boda');
  });

  it('marks only the selected option as selected', () => {
    render(<ChoiceChips label="Paquete" options={OPTIONS} value="Producto" onChange={jest.fn()} />);

    expect(screen.getAllByRole('button', { selected: true })).toHaveLength(1);
    expect(screen.getByRole('button', { name: 'Producto', selected: true })).toBeTruthy();
  });

  it('shows the validation message when there is one', () => {
    render(<ChoiceChips label="Paquete" options={OPTIONS} value="" onChange={jest.fn()} error="Elige un paquete." />);

    expect(screen.getByText('Elige un paquete.')).toBeTruthy();
  });
});
