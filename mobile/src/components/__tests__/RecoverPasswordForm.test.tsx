import { fireEvent, render, screen } from '@testing-library/react-native';
import { useRecoverPasswordForm } from '../../hooks/useRecoverPasswordForm';
import type { RecoverPasswordFormState } from '../../types/hooks/useRecoverPasswordForm.types';
import { RecoverPasswordForm } from '../RecoverPasswordForm';

jest.mock('../../hooks/useRecoverPasswordForm');

/**
 * Builds the state the form reads, with optional overrides.
 * @param overrides Values to replace.
 * @returns The state.
 */
function formState(overrides: Partial<RecoverPasswordFormState> = {}): RecoverPasswordFormState {
  return {
    step: 'request',
    values: { identifier: '', code: '', newPassword: '', confirmPassword: '' },
    errors: {},
    isPending: false,
    submitError: null,
    notice: null,
    cooldownSeconds: 0,
    setField: jest.fn(),
    requestCode: jest.fn(),
    confirm: jest.fn(),
    resendCode: jest.fn(),
    changeAccount: jest.fn(),
    ...overrides,
  };
}

/**
 * Makes the hook return a state.
 * @param state State to return.
 */
function stateIs(state: RecoverPasswordFormState): void {
  jest.mocked(useRecoverPasswordForm).mockReturnValue(state);
}

describe('RecoverPasswordForm', () => {
  describe('first step', () => {
    it('asks for the account and sends what is typed', () => {
      const state = formState();
      stateIs(state);
      render(<RecoverPasswordForm onRecovered={jest.fn()} />);

      fireEvent.changeText(screen.getByLabelText('Usuario o correo'), 'ana');
      fireEvent.press(screen.getByRole('button', { name: 'Enviar código' }));

      expect(state.setField).toHaveBeenCalledWith('identifier', 'ana');
      expect(state.requestCode).toHaveBeenCalledTimes(1);
      expect(screen.queryByLabelText('Código de 6 dígitos')).toBeNull();
    });

    it('shows the validation message and the error, and says it is sending', () => {
      stateIs(formState({ errors: { identifier: 'Escribe tu usuario o tu correo.' }, submitError: 'No se pudo enviar la solicitud.', isPending: true }));

      render(<RecoverPasswordForm onRecovered={jest.fn()} />);

      expect(screen.getByText('Escribe tu usuario o tu correo.')).toBeTruthy();
      expect(screen.getByText('No se pudo enviar la solicitud.')).toBeTruthy();
      expect(screen.getByText('Enviando…')).toBeTruthy();
    });
  });

  describe('second step', () => {
    it('shows the notice and the three fields, and changes the password from the button', () => {
      const state = formState({ step: 'confirm', notice: 'Te enviamos un código.' });
      stateIs(state);
      render(<RecoverPasswordForm onRecovered={jest.fn()} />);

      expect(screen.getByText('Te enviamos un código.')).toBeTruthy();
      fireEvent.changeText(screen.getByLabelText('Código de 6 dígitos'), '123456');
      fireEvent.changeText(screen.getByLabelText('Contraseña nueva'), 'a new password');
      fireEvent.changeText(screen.getByLabelText('Repite la contraseña nueva'), 'a new password');
      fireEvent.press(screen.getByRole('button', { name: 'Cambiar contraseña' }));

      expect(state.setField).toHaveBeenCalledWith('code', '123456');
      expect(state.setField).toHaveBeenCalledWith('newPassword', 'a new password');
      expect(state.setField).toHaveBeenCalledWith('confirmPassword', 'a new password');
      expect(state.confirm).toHaveBeenCalledTimes(1);
    });

    it('asks for another code and lets the user pick another account', () => {
      const state = formState({ step: 'confirm' });
      stateIs(state);
      render(<RecoverPasswordForm onRecovered={jest.fn()} />);

      fireEvent.press(screen.getByRole('button', { name: 'Enviar un código nuevo' }));
      fireEvent.press(screen.getByRole('button', { name: 'Usar otra cuenta' }));

      expect(state.resendCode).toHaveBeenCalledTimes(1);
      expect(state.changeAccount).toHaveBeenCalledTimes(1);
    });

    it('shows the wait and does not let the user ask again until it ends', () => {
      const state = formState({ step: 'confirm', cooldownSeconds: 42 });
      stateIs(state);
      render(<RecoverPasswordForm onRecovered={jest.fn()} />);

      expect(screen.getByText('Enviar un código nuevo (42 s)')).toBeTruthy();
      fireEvent.press(screen.getByRole('button', { name: 'Enviar un código nuevo' }));

      expect(state.resendCode).not.toHaveBeenCalled();
    });

    it('shows the validation messages and the error, and says it is changing', () => {
      stateIs(
        formState({
          step: 'confirm',
          errors: { code: 'Escribe los 6 dígitos del código.', confirmPassword: 'Las contraseñas no coinciden.' },
          submitError: 'El código no es correcto o ya venció.',
          isPending: true,
        }),
      );

      render(<RecoverPasswordForm onRecovered={jest.fn()} />);

      expect(screen.getByText('Escribe los 6 dígitos del código.')).toBeTruthy();
      expect(screen.getByText('Las contraseñas no coinciden.')).toBeTruthy();
      expect(screen.getByText('El código no es correcto o ya venció.')).toBeTruthy();
      expect(screen.getByText('Cambiando…')).toBeTruthy();
    });
  });
});
