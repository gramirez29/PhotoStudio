import { fireEvent, render, screen } from '@testing-library/react-native';
import { useNewBookingForm } from '../../hooks/useNewBookingForm';
import type { NewBookingFormState } from '../../types/hooks/useNewBookingForm.types';
import { NewBookingForm } from '../NewBookingForm';

jest.mock('../../hooks/useNewBookingForm');

/**
 * Builds the state the form reads, with optional overrides.
 * @param overrides Values to replace.
 * @returns The state.
 */
function formState(overrides: Partial<NewBookingFormState> = {}): NewBookingFormState {
  return {
    today: new Date(2026, 9, 10),
    values: {
      clientName: '',
      clientPhone: '',
      clientEmail: '',
      packageName: '',
      packageTier: null,
      price: '',
      extras: [],
      discount: '',
      start: new Date(2026, 9, 11, 10),
      durationHours: 2,
    },
    totals: { packagePrice: 0, extrasTotal: 0, discount: 0, total: 0 },
    errors: {},
    isPending: false,
    submitError: null,
    setText: jest.fn(),
    setPhone: jest.fn(),
    setSessionType: jest.fn(),
    setPackageTier: jest.fn(),
    setExtras: jest.fn(),
    setStart: jest.fn(),
    setDuration: jest.fn(),
    submit: jest.fn(),
    ...overrides,
  };
}

describe('NewBookingForm', () => {
  it('asks for the type of session and, apart from it, for the package', () => {
    jest.mocked(useNewBookingForm).mockReturnValue(formState());

    render(<NewBookingForm />);

    expect(screen.getByLabelText('Tipo de sesión')).toBeTruthy();
    expect(screen.getByLabelText('Paquete')).toBeTruthy();
  });

  it('offers the packages and passes the one picked on', () => {
    const state = formState();
    jest.mocked(useNewBookingForm).mockReturnValue(state);
    render(<NewBookingForm />);

    fireEvent.press(screen.getByLabelText('Paquete'));
    expect(screen.getByLabelText('Básico')).toBeTruthy();
    expect(screen.getByLabelText('Estándar')).toBeTruthy();
    expect(screen.getByLabelText('Premium')).toBeTruthy();
    expect(screen.getByLabelText('Custom')).toBeTruthy();
    expect(screen.getByLabelText('Otro')).toBeTruthy();
    fireEvent.press(screen.getByLabelText('Estándar'));

    expect(state.setPackageTier).toHaveBeenCalledWith('Estándar');
  });

  it('shows the package that is chosen', () => {
    jest.mocked(useNewBookingForm).mockReturnValue(formState({ values: { ...formState().values, packageTier: 'Premium' } }));

    render(<NewBookingForm />);

    expect(screen.getByLabelText('Paquete').props.accessibilityValue.text).toBe('Premium');
  });

  it('opens the extras and hands what is confirmed to the form', () => {
    const state = formState();
    jest.mocked(useNewBookingForm).mockReturnValue(state);
    render(<NewBookingForm />);

    fireEvent.press(screen.getAllByRole('button', { name: 'Agregar Extras' })[0]!);
    fireEvent.press(screen.getByLabelText('Agregar Maquillaje'));
    fireEvent.press(screen.getAllByRole('button', { name: 'Agregar Extras' }).at(-1)!);

    expect(state.setExtras).toHaveBeenCalledWith([expect.objectContaining({ code: 'makeup', quantity: 1 })]);
    expect(screen.queryByLabelText('Cerrar extras')).toBeNull();
  });

  it('keeps the booking as it was when the extras are closed without confirming', () => {
    const state = formState();
    jest.mocked(useNewBookingForm).mockReturnValue(state);
    render(<NewBookingForm />);
    fireEvent.press(screen.getByRole('button', { name: 'Agregar Extras' }));

    fireEvent.press(screen.getByLabelText('Cerrar extras'));

    expect(state.setExtras).not.toHaveBeenCalled();
    expect(screen.queryByLabelText('Cerrar extras')).toBeNull();
  });

  it('shows the total of the extras in its own field, empty while there are none', () => {
    jest.mocked(useNewBookingForm).mockReturnValue(formState());
    const { unmount } = render(<NewBookingForm />);
    expect(screen.getByLabelText('Extras (₡)').props.value).toBe('');
    unmount();

    jest.mocked(useNewBookingForm).mockReturnValue(
      formState({
        values: { ...formState().values, extras: [{ code: 'makeup', name: 'Maquillaje', unitPrice: 20000, quantity: 2 }] },
        totals: { packagePrice: 0, extrasTotal: 40000, discount: 0, total: 40000 },
      }),
    );
    render(<NewBookingForm />);

    expect(screen.getByLabelText('Extras (₡)').props.value).toMatch(/40\D*000/);
    expect(screen.getByLabelText('Extras (₡)').props.editable).toBe(false);
  });

  it('shows the total to pay before the button that creates the booking, bigger and in bold', () => {
    jest.mocked(useNewBookingForm).mockReturnValue(formState({ totals: { packagePrice: 65000, extrasTotal: 0, discount: 0, total: 65000 } }));

    render(<NewBookingForm />);

    const total = screen.getByLabelText('Total a pagar');
    expect(total.props.children).toMatch(/65\D*000/);
    expect(total.props.style).toEqual(expect.objectContaining({ fontWeight: '700', fontSize: 26 }));
    expect(screen.getByText('Total a pagar')).toBeTruthy();
  });

  it('passes the discount typed on and shows its validation message', () => {
    const state = formState({ errors: { discount: 'Escribe un descuento válido o déjalo vacío.' } });
    jest.mocked(useNewBookingForm).mockReturnValue(state);
    render(<NewBookingForm />);

    fireEvent.changeText(screen.getByLabelText('Descuento (₡), opcional'), '5000');

    expect(state.setText).toHaveBeenCalledWith('discount', '5000');
    expect(screen.getByText('Escribe un descuento válido o déjalo vacío.')).toBeTruthy();
  });
});
