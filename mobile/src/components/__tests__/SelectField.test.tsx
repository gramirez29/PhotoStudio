import { fireEvent, render, screen } from '@testing-library/react-native';
import { SelectField } from '../SelectField';

const OPTIONS = ['Compromiso', 'Pre-Boda', 'Producto'] as const;

/**
 * Renders the field with the sample options.
 * @param value Selected option.
 * @param onChange Handler called when an option is picked.
 * @param error Validation message, if any.
 */
function renderField(value = '', onChange: (option: string) => void = jest.fn(), error?: string): void {
  render(
    <SelectField
      label="Paquete"
      options={OPTIONS}
      value={value}
      onChange={onChange}
      placeholder="Selecciona un paquete"
      {...(error === undefined ? {} : { error })}
    />,
  );
}

describe('SelectField', () => {
  it('shows the placeholder while nothing is selected', () => {
    renderField();
    expect(screen.getByText('Selecciona un paquete')).toBeTruthy();
  });

  it('shows the selected option in the field', () => {
    renderField('Pre-Boda');

    expect(screen.getByText('Pre-Boda')).toBeTruthy();
    expect(screen.queryByText('Selecciona un paquete')).toBeNull();
  });

  it('keeps the list closed until the field is tapped, and then lists every option in order', () => {
    renderField();
    expect(screen.queryAllByRole('menuitem')).toHaveLength(0);

    fireEvent.press(screen.getByRole('combobox'));

    expect(screen.getAllByRole('menuitem').map((item) => item.props.accessibilityLabel)).toEqual([...OPTIONS]);
  });

  it('calls onChange with the picked option and closes the list', () => {
    const onChange = jest.fn();
    renderField('', onChange);
    fireEvent.press(screen.getByRole('combobox'));

    fireEvent.press(screen.getByRole('menuitem', { name: 'Producto' }));

    expect(onChange).toHaveBeenCalledTimes(1);
    expect(onChange).toHaveBeenCalledWith('Producto');
    expect(screen.queryAllByRole('menuitem')).toHaveLength(0);
  });

  it('marks the selected option in the list', () => {
    renderField('Compromiso');
    fireEvent.press(screen.getByRole('combobox'));

    expect(screen.getAllByRole('menuitem', { selected: true })).toHaveLength(1);
    expect(screen.getByRole('menuitem', { name: 'Compromiso', selected: true })).toBeTruthy();
  });

  it('closes the list without changing anything when the photographer taps outside it', () => {
    const onChange = jest.fn();
    renderField('', onChange);
    fireEvent.press(screen.getByRole('combobox'));

    fireEvent.press(screen.getByLabelText('Cerrar lista'));

    expect(onChange).not.toHaveBeenCalled();
    expect(screen.queryAllByRole('menuitem')).toHaveLength(0);
  });

  it('shows the validation message when there is one', () => {
    renderField('', jest.fn(), 'Elige un paquete.');

    expect(screen.getByText('Elige un paquete.')).toBeTruthy();
  });
});
