import { fireEvent, render, screen } from '@testing-library/react-native';
import { parseBooking } from '../../api/bookingsApi';
import { bookingPayload } from '../../api/__fixtures__/testResponses';
import { useContractForm } from '../../hooks/useContractForm';
import { initialContractValues } from '../../forms/contractForm';
import type { ContractFormState } from '../../types/hooks/useContractForm.types';
import { ContractForm } from '../ContractForm';
import { ContractText } from '../ContractText';

jest.mock('../../hooks/useContractForm');

const booking = parseBooking(bookingPayload);

const CONTRACT = 'CONTRATO DE SERVICIOS\nEntre PhotoSense Studio y María Pérez.\n12. Uso de imagen.\n    [ ] Autorizo\n    [ ] No autorizo';

/**
 * Builds the state the screen reads: signing on the phone with the contract loaded, with optional overrides.
 * @param overrides Values to replace.
 * @returns The state.
 */
function formState(overrides: Partial<ContractFormState> = {}): ContractFormState {
  return {
    values: initialContractValues('María Pérez'),
    errors: {},
    submitError: null,
    isPending: false,
    showsContract: true,
    contractText: CONTRACT,
    isContractLoading: false,
    contractError: null,
    reloadContract: jest.fn(),
    setKind: jest.fn(),
    setText: jest.fn(),
    setChoice: jest.fn(),
    submit: jest.fn(),
    ...overrides,
  };
}

/**
 * Makes the hook return a state.
 * @param state State to return.
 */
function stateIs(state: ContractFormState): void {
  jest.mocked(useContractForm).mockReturnValue(state);
}

describe('ContractText', () => {
  it('shows the title and every paragraph, with the options of the image use as separate lines', () => {
    render(<ContractText text={CONTRACT} />);

    expect(screen.getByLabelText('Texto del contrato')).toBeTruthy();
    expect(screen.getByText('CONTRATO DE SERVICIOS')).toBeTruthy();
    expect(screen.getByText('Entre PhotoSense Studio y María Pérez.')).toBeTruthy();
    expect(screen.getByText('[ ] Autorizo')).toBeTruthy();
    expect(screen.getByText('[ ] No autorizo')).toBeTruthy();
  });
});

describe('ContractForm', () => {
  it('shows the contract for the client to read and what they have to give, when they sign on the phone', () => {
    stateIs(formState());

    render(<ContractForm booking={booking} />);

    expect(screen.getByText(/Entrega el celular al cliente para que lea el contrato/)).toBeTruthy();
    expect(screen.getByText('CONTRATO DE SERVICIOS')).toBeTruthy();
    expect(screen.getByLabelText('Nombre de quien firma (su firma)')).toBeTruthy();
    expect(screen.getByLabelText('Cédula o DIMEX')).toBeTruthy();
    expect(screen.getByLabelText('¿Cuántas personas vienen a la sesión?')).toBeTruthy();
    expect(screen.getByLabelText('Lugar de la sesión (opcional)')).toBeTruthy();
    expect(screen.getByLabelText('El cliente leyó el contrato y lo acepta')).toBeTruthy();
  });

  it('sends what is typed to the form', () => {
    const state = formState();
    stateIs(state);
    render(<ContractForm booking={booking} />);

    fireEvent.changeText(screen.getByLabelText('Cédula o DIMEX'), '1-1234-5678');
    fireEvent.changeText(screen.getByLabelText('¿Cuántas personas vienen a la sesión?'), '3');
    fireEvent(screen.getByLabelText('El cliente leyó el contrato y lo acepta'), 'valueChange', true);
    fireEvent.press(screen.getByRole('button', { name: 'Registrar firma' }));

    expect(state.setText).toHaveBeenCalledWith('idNumber', '1-1234-5678');
    expect(state.setText).toHaveBeenCalledWith('participantCount', '3');
    expect(state.setChoice).toHaveBeenCalledWith('accepted', true);
    expect(state.submit).toHaveBeenCalledTimes(1);
  });

  it('shows the validation messages next to what is missing', () => {
    stateIs(
      formState({
        errors: {
          idNumber: 'Escribe la cédula (9 dígitos) o el DIMEX (11 o 12 dígitos).',
          imageUse: 'Elige si autoriza o no el uso de las fotos.',
          accepted: 'El cliente tiene que leer y aceptar el contrato.',
        },
      }),
    );

    render(<ContractForm booking={booking} />);

    expect(screen.getByText('Escribe la cédula (9 dígitos) o el DIMEX (11 o 12 dígitos).')).toBeTruthy();
    expect(screen.getByText('Elige si autoriza o no el uso de las fotos.')).toBeTruthy();
    expect(screen.getByText('El cliente tiene que leer y aceptar el contrato.')).toBeTruthy();
  });

  it('shows only the name for a contract on paper, with no contract to read', () => {
    stateIs(formState({ values: { ...initialContractValues('María Pérez'), kind: 'Paper' }, showsContract: false, contractText: null }));

    render(<ContractForm booking={booking} />);

    expect(screen.getByLabelText('Nombre que aparece en el contrato')).toBeTruthy();
    expect(screen.queryByLabelText('Cédula o DIMEX')).toBeNull();
    expect(screen.queryByLabelText('Texto del contrato')).toBeNull();
    expect(screen.getByText(/aquí solo queda registrado que se firmó en papel/)).toBeTruthy();
  });

  it('says the contract is loading, and does not let anyone sign before it is read', () => {
    stateIs(formState({ contractText: null, isContractLoading: true }));

    render(<ContractForm booking={booking} />);

    expect(screen.getByText('Cargando el contrato…')).toBeTruthy();
    expect(screen.queryByLabelText('Cédula o DIMEX')).toBeNull();
    fireEvent.press(screen.getByRole('button', { name: 'Registrar firma' }));
    expect(jest.mocked(useContractForm).mock.results[0]?.value.submit).not.toHaveBeenCalled();
  });

  it('says why the contract could not be loaded, and offers to try again', () => {
    const state = formState({ contractText: null, contractError: 'No se pudo cargar la reserva.' });
    stateIs(state);
    render(<ContractForm booking={booking} />);

    fireEvent.press(screen.getByRole('button', { name: 'Reintentar' }));

    expect(screen.getByText('No se pudo cargar la reserva.')).toBeTruthy();
    expect(state.reloadContract).toHaveBeenCalledTimes(1);
  });

  it('shows the error of a failed signature and says it is sending', () => {
    stateIs(formState({ submitError: 'El contrato cambió mientras se leía.', isPending: true }));

    render(<ContractForm booking={booking} />);

    expect(screen.getByText('El contrato cambió mientras se leía.')).toBeTruthy();
    expect(screen.getByText('Enviando…')).toBeTruthy();
  });
});
