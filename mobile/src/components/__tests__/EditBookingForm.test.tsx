import { fireEvent, render, screen } from '@testing-library/react-native';
import { parseBooking } from '../../api/bookingsApi';
import { bookingPayload } from '../../api/__fixtures__/testResponses';
import { initialEditValues } from '../../forms/editBookingForm';
import { useEditBookingForm } from '../../hooks/useEditBookingForm';
import type { EditBookingFormState } from '../../types/hooks/useEditBookingForm.types';
import { EditBookingForm } from '../EditBookingForm';

jest.mock('../../hooks/useEditBookingForm');

const booking = parseBooking({ ...bookingPayload, clientPhone: '+50670189220' });

/**
 * Builds the state the screen reads, with optional overrides.
 * @param overrides Values to replace.
 * @returns The state.
 */
function formState(overrides: Partial<EditBookingFormState> = {}): EditBookingFormState {
  return {
    values: initialEditValues(booking),
    errors: {},
    isPending: false,
    submitError: null,
    packageEditable: true,
    setText: jest.fn(),
    setPhone: jest.fn(),
    setPackage: jest.fn(),
    submit: jest.fn(),
    ...overrides,
  };
}

/**
 * Makes the hook return a state.
 * @param state State to return.
 */
function stateIs(state: EditBookingFormState): void {
  jest.mocked(useEditBookingForm).mockReturnValue(state);
}

describe('EditBookingForm', () => {
  it('shows what the booking has now, with the package and the price to change while that is possible', () => {
    stateIs(formState());

    render(<EditBookingForm booking={booking} />);

    expect(screen.getByLabelText('Cliente').props.value).toBe(booking.clientName);
    expect(screen.getByLabelText('Teléfono').props.value).toBe('7018-9220');
    expect(screen.getByLabelText('Correo (opcional)')).toBeTruthy();
    expect(screen.getByText('Paquete')).toBeTruthy();
    expect(screen.getByLabelText('Precio del paquete (₡)')).toBeTruthy();
  });

  it('shows the package and the price, without letting them change, once they are locked', () => {
    stateIs(formState({ packageEditable: false }));

    render(<EditBookingForm booking={booking} />);

    expect(screen.queryByLabelText('Precio del paquete (₡)')).toBeNull();
    expect(screen.getByText('Paquete y precio')).toBeTruthy();
    expect(screen.getByText(/No se pueden cambiar porque el contrato ya está firmado o hay pagos registrados/)).toBeTruthy();
  });

  it('passes what is typed on and saves from the button', () => {
    const state = formState();
    stateIs(state);
    render(<EditBookingForm booking={booking} />);

    fireEvent.changeText(screen.getByLabelText('Correo (opcional)'), 'nuevo@example.com');
    fireEvent.changeText(screen.getByLabelText('Teléfono'), '70189221');
    fireEvent.press(screen.getByRole('button', { name: 'Guardar cambios' }));

    expect(state.setText).toHaveBeenCalledWith('clientEmail', 'nuevo@example.com');
    expect(state.setPhone).toHaveBeenCalledWith('70189221');
    expect(state.submit).toHaveBeenCalledTimes(1);
  });

  it('shows the validation messages and the error of a failed save', () => {
    stateIs(
      formState({
        errors: { clientName: 'Escribe el nombre del cliente.', clientEmail: 'Escribe un correo válido.' },
        submitError: 'No se pudo completar la acción.',
      }),
    );

    render(<EditBookingForm booking={booking} />);

    expect(screen.getByText('Escribe el nombre del cliente.')).toBeTruthy();
    expect(screen.getByText('Escribe un correo válido.')).toBeTruthy();
    expect(screen.getByText('No se pudo completar la acción.')).toBeTruthy();
  });

  it('says it is saving', () => {
    stateIs(formState({ isPending: true }));

    render(<EditBookingForm booking={booking} />);

    expect(screen.getByText('Guardando…')).toBeTruthy();
  });
});
