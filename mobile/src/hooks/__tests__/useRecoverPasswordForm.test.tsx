import { act, renderHook, waitFor } from '@testing-library/react-native';
import { authApi } from '../../api/authClient';
import { ApiError } from '../../api/httpClient';
import { createQueryWrapper } from '../__fixtures__/queryWrapper';
import { useRecoverPasswordForm } from '../useRecoverPasswordForm';

jest.mock('../../api/authClient', () => ({ authApi: { requestPasswordReset: jest.fn(), confirmPasswordReset: jest.fn() } }));

describe('useRecoverPasswordForm', () => {
  const { client, Wrapper } = createQueryWrapper();
  const onRecovered = jest.fn();

  beforeEach(() => {
    jest.clearAllMocks();
  });

  afterEach(() => {
    client.clear();
  });

  /**
   * Renders the hook.
   * @returns The hook result holder.
   */
  function render(): ReturnType<typeof renderHook<ReturnType<typeof useRecoverPasswordForm>, unknown>> {
    return renderHook(() => useRecoverPasswordForm(onRecovered), { wrapper: Wrapper });
  }

  /**
   * Gets the hook to the second step, with the account typed.
   * @param result Hook result holder.
   * @param result.current Latest state of the hook.
   */
  async function reachTheSecondStep(result: { readonly current: ReturnType<typeof useRecoverPasswordForm> }): Promise<void> {
    jest.mocked(authApi.requestPasswordReset).mockResolvedValue(undefined);
    act(() => result.current.setField('identifier', ' ana '));
    act(() => result.current.requestCode());
    await waitFor(() => expect(result.current.step).toBe('confirm'));
  }

  it('does not call the backend until an account is typed', () => {
    const { result } = render();

    act(() => result.current.requestCode());

    expect(result.current.errors.identifier).toBe('Escribe tu usuario o tu correo.');
    expect(authApi.requestPasswordReset).not.toHaveBeenCalled();
  });

  it('asks for the code, moves on and says to look at the email without saying whether the account exists', async () => {
    const { result } = render();

    await reachTheSecondStep(result);

    expect(authApi.requestPasswordReset).toHaveBeenCalledWith({ identifier: 'ana' });
    expect(result.current.notice).toMatch(/Si hay una cuenta con ese usuario o correo/);
    expect(result.current.cooldownSeconds).toBeGreaterThan(0);
  });

  it('keeps only the digits of the code', async () => {
    const { result } = render();
    await reachTheSecondStep(result);

    act(() => result.current.setField('code', '12 34a56789'));

    expect(result.current.values.code).toBe('123456');
  });

  it('does not call the backend with a code or a password that cannot be right', async () => {
    const { result } = render();
    await reachTheSecondStep(result);

    act(() => result.current.confirm());

    expect(result.current.errors.code).toBe('Escribe los 6 dígitos del código.');
    expect(result.current.errors.newPassword).toBe('La contraseña debe tener al menos 8 caracteres.');
    expect(authApi.confirmPasswordReset).not.toHaveBeenCalled();
  });

  it('changes the password and tells the screen it is done', async () => {
    jest.mocked(authApi.confirmPasswordReset).mockResolvedValue(undefined);
    const { result } = render();
    await reachTheSecondStep(result);
    act(() => {
      result.current.setField('code', '123456');
      result.current.setField('newPassword', 'a new password');
      result.current.setField('confirmPassword', 'a new password');
    });

    act(() => result.current.confirm());

    await waitFor(() => expect(onRecovered).toHaveBeenCalledTimes(1));
    expect(authApi.confirmPasswordReset).toHaveBeenCalledWith({ identifier: 'ana', code: '123456', newPassword: 'a new password' });
  });

  it('explains a code that is not accepted and stays on the step', async () => {
    jest.mocked(authApi.confirmPasswordReset).mockRejectedValue(new ApiError(422, 'wrong', 'auth.invalid_reset_code'));
    const { result } = render();
    await reachTheSecondStep(result);
    act(() => {
      result.current.setField('code', '000000');
      result.current.setField('newPassword', 'a new password');
      result.current.setField('confirmPassword', 'a new password');
    });

    act(() => result.current.confirm());

    await waitFor(() => expect(result.current.submitError).toMatch(/El código no es correcto o ya venció/));
    expect(result.current.step).toBe('confirm');
    expect(onRecovered).not.toHaveBeenCalled();
  });

  it('explains too many requests when asking for the code', async () => {
    jest.mocked(authApi.requestPasswordReset).mockRejectedValue(new ApiError(429, 'slow', 'rate_limit.exceeded'));
    const { result } = render();
    act(() => result.current.setField('identifier', 'ana'));

    act(() => result.current.requestCode());

    await waitFor(() => expect(result.current.submitError).toMatch(/demasiadas solicitudes/));
    expect(result.current.step).toBe('request');
  });

  it('asks for another code for the same account', async () => {
    const { result } = render();
    await reachTheSecondStep(result);

    act(() => result.current.resendCode());

    await waitFor(() => expect(authApi.requestPasswordReset).toHaveBeenCalledTimes(2));
    expect(authApi.requestPasswordReset).toHaveBeenLastCalledWith({ identifier: 'ana' });
  });

  it('goes back to the first step keeping the account typed and forgetting the rest', async () => {
    const { result } = render();
    await reachTheSecondStep(result);
    act(() => result.current.setField('newPassword', 'something'));

    act(() => result.current.changeAccount());

    expect(result.current.step).toBe('request');
    expect(result.current.values.identifier).toBe(' ana ');
    expect(result.current.values.newPassword).toBe('');
    expect(result.current.notice).toBeNull();
  });
});
