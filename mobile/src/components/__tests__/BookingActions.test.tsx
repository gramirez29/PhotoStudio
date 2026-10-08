import { fireEvent, render, screen } from '@testing-library/react-native';
import { parseBooking } from '../../api/bookingsApi';
import { bookingPayload } from '../../api/__fixtures__/testResponses';
import { useBookingActions } from '../../hooks/useBookingActions';
import type { BookingResponse } from '../../types/api/booking';
import type { BookingActionsState } from '../../types/hooks/useBookingActions.types';
import { BookingActions } from '../BookingActions';

jest.mock('../../hooks/useBookingActions');

const mockedUseBookingActions = jest.mocked(useBookingActions);

/**
 * Builds the state the hook returns, with every handler mocked.
 * @param overrides Values to replace.
 * @returns The state.
 */
function actionsState(overrides: Partial<BookingActionsState> = {}): BookingActionsState {
  return {
    isPending: false,
    errorMessage: null,
    reschedule: jest.fn(),
    cancel: jest.fn(),
    complete: jest.fn(),
    markClientAbsent: jest.fn(),
    revertClientAbsent: jest.fn(),
    ...overrides,
  };
}

/**
 * Builds the sample booking with the given allowed actions.
 * @param allowedActions Actions the API allows.
 * @returns The booking.
 */
function bookingWith(allowedActions: BookingResponse['allowedActions']): BookingResponse {
  return { ...parseBooking(bookingPayload), allowedActions };
}

describe('BookingActions', () => {
  it('shows a button only for the allowed actions the app supports', () => {
    mockedUseBookingActions.mockReturnValue(actionsState());

    render(<BookingActions booking={bookingWith(['Reschedule', 'Cancel'])} />);

    expect(screen.getByText('Reprogramar')).toBeTruthy();
    expect(screen.getByText('Cancelar')).toBeTruthy();
    expect(screen.queryByText('Marcar sesión completada')).toBeNull();
    expect(screen.queryByText('Marcar cliente ausente')).toBeNull();
  });

  it('runs the handler of the action that is tapped', () => {
    const state = actionsState();
    mockedUseBookingActions.mockReturnValue(state);
    render(<BookingActions booking={bookingWith(['Complete', 'MarkClientAbsent', 'Cancel'])} />);

    fireEvent.press(screen.getByText('Marcar sesión completada'));
    fireEvent.press(screen.getByText('Marcar cliente ausente'));

    expect(state.complete).toHaveBeenCalledTimes(1);
    expect(state.markClientAbsent).toHaveBeenCalledTimes(1);
    expect(state.cancel).not.toHaveBeenCalled();
  });

  it('lists the allowed actions the app cannot perform yet instead of showing dead buttons', () => {
    mockedUseBookingActions.mockReturnValue(actionsState());

    render(<BookingActions booking={bookingWith(['RecordInPersonPayment', 'Cancel'])} />);

    expect(screen.getByText(/Próximamente en la app: Registrar pago presencial\./)).toBeTruthy();
    expect(screen.queryByRole('button', { name: /Registrar pago presencial/ })).toBeNull();
  });

  it('says there is nothing to do when the booking has no allowed actions', () => {
    mockedUseBookingActions.mockReturnValue(actionsState());

    render(<BookingActions booking={bookingWith([])} />);

    expect(screen.getByText('No hay acciones disponibles en este estado.')).toBeTruthy();
  });

  it('disables the buttons while an action is being sent and shows the last error', () => {
    mockedUseBookingActions.mockReturnValue(actionsState({ isPending: true, errorMessage: 'No se pudo completar la acción.' }));

    render(<BookingActions booking={bookingWith(['Complete'])} />);

    expect(screen.getByRole('button', { disabled: true })).toBeTruthy();
    expect(screen.getByText('No se pudo completar la acción.')).toBeTruthy();
  });
});
