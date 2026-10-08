import { fireEvent, render, screen } from '@testing-library/react-native';
import { createQueryWrapper } from '../../hooks/__fixtures__/queryWrapper';
import { AuthPanel } from '../AuthPanel';

jest.mock('../../session/sessionManager', () => ({ signIn: jest.fn(), register: jest.fn() }));

describe('AuthPanel', () => {
  const { client, Wrapper } = createQueryWrapper();

  afterEach(() => {
    client.clear();
  });

  it('starts on the login form with a way to create an account', () => {
    render(<AuthPanel />, { wrapper: Wrapper });

    expect(screen.getByLabelText('Usuario')).toBeTruthy();
    expect(screen.getByLabelText('Contraseña')).toBeTruthy();
    expect(screen.getByRole('button', { name: 'Iniciar sesión' })).toBeTruthy();
    expect(screen.queryByLabelText('Nombre')).toBeNull();
    expect(screen.getByText('¿No tienes cuenta? Crear cuenta')).toBeTruthy();
  });

  it('shows the account creation form, with its data fields, when asked', () => {
    render(<AuthPanel />, { wrapper: Wrapper });

    fireEvent.press(screen.getByText('¿No tienes cuenta? Crear cuenta'));

    expect(screen.getByLabelText('Nombre')).toBeTruthy();
    expect(screen.getByLabelText('Teléfono')).toBeTruthy();
    expect(screen.getByLabelText('Correo')).toBeTruthy();
    expect(screen.getByLabelText('Usuario')).toBeTruthy();
    expect(screen.getByLabelText('Contraseña')).toBeTruthy();
    expect(screen.getByLabelText('Repite la contraseña')).toBeTruthy();
    expect(screen.getByRole('button', { name: 'Crear cuenta' })).toBeTruthy();
  });

  it('goes back to the login form', () => {
    render(<AuthPanel />, { wrapper: Wrapper });
    fireEvent.press(screen.getByText('¿No tienes cuenta? Crear cuenta'));

    fireEvent.press(screen.getByText('¿Ya tienes cuenta? Iniciar sesión'));

    expect(screen.getByRole('button', { name: 'Iniciar sesión' })).toBeTruthy();
    expect(screen.queryByLabelText('Nombre')).toBeNull();
  });

  it('shows the validation messages of the creation form when it is sent empty', () => {
    render(<AuthPanel />, { wrapper: Wrapper });
    fireEvent.press(screen.getByText('¿No tienes cuenta? Crear cuenta'));

    fireEvent.press(screen.getByRole('button', { name: 'Crear cuenta' }));

    expect(screen.getByText('Escribe tu nombre.')).toBeTruthy();
    expect(screen.getByText('Escribe tu correo.')).toBeTruthy();
    expect(screen.getByText('Escribe un teléfono válido (8 dígitos o con código de país).')).toBeTruthy();
  });
});
