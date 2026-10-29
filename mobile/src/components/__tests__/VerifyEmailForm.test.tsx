import { fireEvent, render, screen } from '@testing-library/react-native';
import { useVerifyEmailForm } from '../../hooks/useVerifyEmailForm';
import type { VerifyEmailFormState } from '../../types/hooks/useVerifyEmailForm.types';
import { VerifyEmailForm } from '../VerifyEmailForm';

jest.mock('../../hooks/useVerifyEmailForm');

/**
 * Builds the state the screen reads, with optional overrides.
 * @param overrides Values to replace.
 * @returns The state.
 */
function formState(overrides: Partial<VerifyEmailFormState> = {}): VerifyEmailFormState {
  return {
    maskedEmail: 'a***@gmail.com',
    code: '',
    codeError: undefined,
    isVerifying: false,
    isSending: false,
    submitError: null,
    notice: null,
    cooldownSeconds: 0,
    setCode: jest.fn(),
    verify: jest.fn(),
    resend: jest.fn(),
    ...overrides,
  };
}

/**
 * Makes the hook return a state.
 * @param state State to return.
 */
function stateIs(state: VerifyEmailFormState): void {
  jest.mocked(useVerifyEmailForm).mockReturnValue(state);
}

describe('VerifyEmailForm', () => {
  it('says where the code went and asks for it', () => {
    stateIs(formState());

    render(<VerifyEmailForm />);

    expect(screen.getByText('Verifica tu correo')).toBeTruthy();
    expect(screen.getByText(/código de 6 dígitos que enviamos a a\*\*\*@gmail\.com/)).toBeTruthy();
    expect(screen.getByLabelText('Código de 6 dígitos')).toBeTruthy();
  });

  it('passes what is typed on, and checks the code from the button', () => {
    const state = formState();
    stateIs(state);
    render(<VerifyEmailForm />);

    fireEvent.changeText(screen.getByLabelText('Código de 6 dígitos'), '123456');
    fireEvent.press(screen.getByRole('button', { name: 'Verificar' }));

    expect(state.setCode).toHaveBeenCalledWith('123456');
    expect(state.verify).toHaveBeenCalledTimes(1);
  });

  it('asks for a new code', () => {
    const state = formState();
    stateIs(state);
    render(<VerifyEmailForm />);

    fireEvent.press(screen.getByRole('button', { name: 'Enviar un código nuevo' }));

    expect(state.resend).toHaveBeenCalledTimes(1);
  });

  it('shows the wait and does not let the user ask again until it ends', () => {
    const state = formState({ cooldownSeconds: 42 });
    stateIs(state);
    render(<VerifyEmailForm />);

    expect(screen.getByText('Enviar un código nuevo (42 s)')).toBeTruthy();
    fireEvent.press(screen.getByRole('button', { name: 'Enviar un código nuevo' }));

    expect(state.resend).not.toHaveBeenCalled();
  });

  it('shows the error, the notice and the validation message', () => {
    stateIs(formState({ submitError: 'El código no es correcto.', notice: 'Te enviamos un código nuevo.', codeError: 'Escribe los 6 dígitos del código.' }));

    render(<VerifyEmailForm />);

    expect(screen.getByText('El código no es correcto.')).toBeTruthy();
    expect(screen.getByText('Te enviamos un código nuevo.')).toBeTruthy();
    expect(screen.getByText('Escribe los 6 dígitos del código.')).toBeTruthy();
  });

  it('says it is working while it checks the code or sends one', () => {
    stateIs(formState({ isVerifying: true, isSending: true }));

    render(<VerifyEmailForm />);

    expect(screen.getByText('Verificando…')).toBeTruthy();
    expect(screen.getByText('Enviando…')).toBeTruthy();
  });
});
