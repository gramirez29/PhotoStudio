import { act, renderHook, waitFor } from '@testing-library/react-native';
import { Alert } from 'react-native';
import { accountApi } from '../../api/client';
import { ApiError } from '../../api/httpClient';
import { goBack } from '../../navigation/appNavigation';
import { adoptSession } from '../../session/sessionManager';
import type { AuthSessionResponse } from '../../types/api/auth';
import { createQueryWrapper } from '../__fixtures__/queryWrapper';
import { useChangePasswordForm } from '../useChangePasswordForm';

jest.mock('../../api/client', () => ({ accountApi: { changePassword: jest.fn() } }));
jest.mock('../../session/sessionManager', () => ({ adoptSession: jest.fn() }));
jest.mock('../../navigation/appNavigation', () => ({ goBack: jest.fn() }));

/** The session the backend returns after changing the password. */
const renewedSession: AuthSessionResponse = {
  accessToken: 'access-2',
  accessTokenExpiresAt: '2026-10-08T21:15:00+00:00',
  refreshToken: 'refresh-2',
  refreshTokenExpiresAt: '2026-11-07T21:00:00+00:00',
  photographerId: '0199a1b2-0000-7000-8000-000000000002',
  username: 'ana',
  email: 'ana@example.com',
  name: 'Ana Pérez',
  isSystemOperator: false,
  emailVerified: true,
};

describe('useChangePasswordForm', () => {
  const { client, Wrapper } = createQueryWrapper();

  beforeEach(() => {
    jest.clearAllMocks();
    jest.spyOn(Alert, 'alert').mockImplementation(() => undefined);
  });

  afterEach(() => {
    client.clear();
  });

  /**
   * Types the three fields of the form.
   * @param result Hook result holder.
   * @param result.current Latest state of the hook.
   * @param confirmation Value typed in the confirmation field.
   */
  function fill(result: { readonly current: ReturnType<typeof useChangePasswordForm> }, confirmation = 'new password'): void {
    act(() => {
      result.current.setField('currentPassword', 'old password');
      result.current.setField('newPassword', 'new password');
      result.current.setField('confirmPassword', confirmation);
    });
  }

  it('does not call the backend while the form is invalid', () => {
    const { result } = renderHook(() => useChangePasswordForm(), { wrapper: Wrapper });
    fill(result, 'something else');

    act(() => result.current.submit());

    expect(result.current.errors.confirmPassword).toBe('Las contraseñas no coinciden.');
    expect(accountApi.changePassword).not.toHaveBeenCalled();
  });

  it('adopts the session of this device and closes the screen when the password changed', async () => {
    jest.mocked(accountApi.changePassword).mockResolvedValue(renewedSession);
    const { result } = renderHook(() => useChangePasswordForm(), { wrapper: Wrapper });
    fill(result);

    act(() => result.current.submit());

    await waitFor(() => expect(goBack).toHaveBeenCalledTimes(1));
    expect(accountApi.changePassword).toHaveBeenCalledWith({ currentPassword: 'old password', newPassword: 'new password' });
    expect(adoptSession).toHaveBeenCalledWith(renewedSession);
  });

  it('explains a wrong current password and stays on the screen', async () => {
    jest.mocked(accountApi.changePassword).mockRejectedValue(new ApiError(422, 'x', 'user.invalid_current_password'));
    const { result } = renderHook(() => useChangePasswordForm(), { wrapper: Wrapper });
    fill(result);

    act(() => result.current.submit());

    await waitFor(() => expect(result.current.submitError).toBe('La contraseña actual no es correcta.'));
    expect(adoptSession).not.toHaveBeenCalled();
    expect(goBack).not.toHaveBeenCalled();
  });
});
