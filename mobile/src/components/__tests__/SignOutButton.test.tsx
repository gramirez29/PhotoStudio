import { fireEvent, render, screen } from '@testing-library/react-native';
import { Alert, type AlertButton } from 'react-native';
import { signOut } from '../../session/sessionManager';
import { SignOutButton } from '../SignOutButton';

jest.mock('../../session/sessionManager', () => ({ signOut: jest.fn() }));

/**
 * Returns the buttons of the last confirmation dialog.
 * @returns The buttons.
 */
function dialogButtons(): readonly AlertButton[] {
  return jest.mocked(Alert.alert).mock.calls.at(-1)?.[2] ?? [];
}

describe('SignOutButton', () => {
  beforeEach(() => {
    jest.clearAllMocks();
    jest.spyOn(Alert, 'alert').mockImplementation(() => undefined);
  });

  it('asks for confirmation and signs out nothing until the photographer confirms', () => {
    render(<SignOutButton />);

    fireEvent.press(screen.getByRole('button', { name: 'Cerrar sesión' }));

    expect(Alert.alert).toHaveBeenCalledTimes(1);
    expect(signOut).not.toHaveBeenCalled();
  });

  it('signs out when the photographer confirms', () => {
    render(<SignOutButton />);
    fireEvent.press(screen.getByRole('button', { name: 'Cerrar sesión' }));

    dialogButtons().find((button) => button.style === 'destructive')?.onPress?.();

    expect(signOut).toHaveBeenCalledTimes(1);
  });

  it('keeps the session when the photographer cancels', () => {
    render(<SignOutButton />);
    fireEvent.press(screen.getByRole('button', { name: 'Cerrar sesión' }));

    dialogButtons().find((button) => button.style === 'cancel')?.onPress?.();

    expect(signOut).not.toHaveBeenCalled();
  });
});
