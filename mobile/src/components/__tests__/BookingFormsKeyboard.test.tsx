import { render, screen } from '@testing-library/react-native';
import { KeyboardAvoidingView, ScrollView } from 'react-native';
import { parseBooking } from '../../api/bookingsApi';
import { bookingPayload } from '../../api/__fixtures__/testResponses';
import { useNewBookingForm } from '../../hooks/useNewBookingForm';
import { useReasonForm } from '../../hooks/useReasonForm';
import type { NewBookingFormState } from '../../types/hooks/useNewBookingForm.types';
import type { ReasonFormState } from '../../types/hooks/useReasonForm.types';
import { NewBookingForm } from '../NewBookingForm';
import { ReasonForm } from '../ReasonForm';

jest.mock('../../hooks/useNewBookingForm');
jest.mock('../../hooks/useReasonForm');

/**
 * Builds the state of the create-booking form with nothing typed.
 * @returns The state.
 */
function newBookingState(): NewBookingFormState {
  return {
    today: new Date(2026, 9, 10),
    values: { clientName: '', clientPhone: '', packageName: '', price: '', start: new Date(2026, 9, 11, 10), durationHours: 2 },
    errors: {},
    isPending: false,
    submitError: null,
    setText: jest.fn(),
    setPhone: jest.fn(),
    setPackage: jest.fn(),
    setStart: jest.fn(),
    setDuration: jest.fn(),
    submit: jest.fn(),
  };
}

/**
 * Builds the state of the reason form with nothing typed.
 * @returns The state.
 */
function reasonState(): ReasonFormState {
  return { text: '', validationError: null, submitError: null, isPending: false, setText: jest.fn(), submit: jest.fn() };
}

describe('forms with text fields on the booking screens', () => {
  it('keep the field being typed above the keyboard on the create-booking screen', () => {
    jest.mocked(useNewBookingForm).mockReturnValue(newBookingState());

    render(<NewBookingForm />);

    expect(screen.UNSAFE_getByType(KeyboardAvoidingView).props.behavior).toBe('padding');
    expect(screen.UNSAFE_getByType(ScrollView).props.keyboardShouldPersistTaps).toBe('handled');
    expect(screen.getByLabelText('Cliente')).toBeTruthy();
  });

  it('keep the field being typed above the keyboard on the reason screen', () => {
    jest.mocked(useReasonForm).mockReturnValue(reasonState());

    render(<ReasonForm booking={parseBooking(bookingPayload)} action="Cancel" />);

    expect(screen.UNSAFE_getByType(KeyboardAvoidingView).props.behavior).toBe('padding');
    expect(screen.UNSAFE_getByType(ScrollView).props.keyboardShouldPersistTaps).toBe('handled');
    expect(screen.getByLabelText('Motivo de la cancelación')).toBeTruthy();
  });
});
