import { fireEvent, render, screen } from '@testing-library/react-native';
import { ExtrasModal } from '../ExtrasModal';

jest.mock('../../config/pricing', () => ({
  MAX_EXTRA_QUANTITY: 99,
  PACKAGES: [],
  EXTRAS: [
    { code: 'extra-hour', label: 'Hora adicional', unitPrice: 15000 },
    { code: 'makeup', label: 'Maquillaje', unitPrice: 20000 },
  ],
}));

describe('ExtrasModal', () => {
  it('lists every extra with the price of one unit, none chosen yet', () => {
    render(<ExtrasModal selected={[]} onConfirm={jest.fn()} onClose={jest.fn()} />);

    expect(screen.getByText('Hora adicional')).toBeTruthy();
    expect(screen.getByText('Maquillaje')).toBeTruthy();
    expect(screen.getAllByText(/c\/u/)).toHaveLength(2);
    expect(screen.getByLabelText('Cantidad de Maquillaje').props.children).toBe(0);
  });

  it('adds and removes units, and shows the total of the extras chosen', () => {
    render(<ExtrasModal selected={[]} onConfirm={jest.fn()} onClose={jest.fn()} />);

    fireEvent.press(screen.getByLabelText('Agregar Hora adicional'));
    fireEvent.press(screen.getByLabelText('Agregar Hora adicional'));
    fireEvent.press(screen.getByLabelText('Agregar Maquillaje'));

    expect(screen.getByLabelText('Cantidad de Hora adicional').props.children).toBe(2);
    expect(screen.getByText(/50\D*000/)).toBeTruthy();

    fireEvent.press(screen.getByLabelText('Quitar Hora adicional'));

    expect(screen.getByLabelText('Cantidad de Hora adicional').props.children).toBe(1);
    expect(screen.getByText(/35\D*000/)).toBeTruthy();
  });

  it('cannot go below zero', () => {
    render(<ExtrasModal selected={[]} onConfirm={jest.fn()} onClose={jest.fn()} />);

    expect(screen.getByLabelText('Quitar Maquillaje').props.accessibilityState.disabled).toBe(true);
  });

  it('starts from the extras the booking already has', () => {
    const selected = [{ code: 'makeup', name: 'Maquillaje', unitPrice: 20000, quantity: 3 }];

    render(<ExtrasModal selected={selected} onConfirm={jest.fn()} onClose={jest.fn()} />);

    expect(screen.getByLabelText('Cantidad de Maquillaje').props.children).toBe(3);
  });

  it('hands over what was chosen when the photographer confirms', () => {
    const onConfirm = jest.fn();
    render(<ExtrasModal selected={[]} onConfirm={onConfirm} onClose={jest.fn()} />);
    fireEvent.press(screen.getByLabelText('Agregar Maquillaje'));
    fireEvent.press(screen.getByLabelText('Agregar Maquillaje'));

    fireEvent.press(screen.getByRole('button', { name: 'Agregar Extras' }));

    expect(onConfirm).toHaveBeenCalledWith([{ code: 'makeup', name: 'Maquillaje', unitPrice: 20000, quantity: 2 }]);
  });

  it('closes without confirming anything', () => {
    const onConfirm = jest.fn();
    const onClose = jest.fn();
    render(<ExtrasModal selected={[]} onConfirm={onConfirm} onClose={onClose} />);
    fireEvent.press(screen.getByLabelText('Agregar Maquillaje'));

    fireEvent.press(screen.getByLabelText('Cerrar extras'));

    expect(onClose).toHaveBeenCalledTimes(1);
    expect(onConfirm).not.toHaveBeenCalled();
  });
});
