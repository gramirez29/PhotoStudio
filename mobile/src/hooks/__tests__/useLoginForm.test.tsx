import { act, renderHook, waitFor } from '@testing-library/react-native';
import { ApiError } from '../../api/httpClient';
import { signIn } from '../../session/sessionManager';
import { createQueryWrapper } from '../__fixtures__/queryWrapper';
import { useLoginForm } from '../useLoginForm';

jest.mock('../../session/sessionManager', () => ({ signIn: jest.fn() }));

describe('useLoginForm', () => {
  const { client, Wrapper } = createQueryWrapper();

  afterEach(() => {
    client.clear();
  });

  beforeEach(() => {
    jest.clearAllMocks();
  });

  it('asks for both fields and does not call the backend when the form is empty', () => {
    const { result } = renderHook(() => useLoginForm(), { wrapper: Wrapper });

    act(() => result.current.submit());

    expect(result.current.errors).toEqual({ email: 'Escribe tu email.', password: 'Escribe tu contraseña.' });
    expect(signIn).not.toHaveBeenCalled();
  });

  it('clears the message of a field as soon as it is edited', () => {
    const { result } = renderHook(() => useLoginForm(), { wrapper: Wrapper });
    act(() => result.current.submit());

    act(() => result.current.setField('email', 'a'));

    expect(result.current.errors.email).toBeUndefined();
    expect(result.current.errors.password).toBe('Escribe tu contraseña.');
  });

  it('signs in with the trimmed email and the password as typed', async () => {
    jest.mocked(signIn).mockResolvedValue(undefined);
    const { result } = renderHook(() => useLoginForm(), { wrapper: Wrapper });
    act(() => {
      result.current.setField('email', '  ana@example.com ');
      result.current.setField('password', 'secret password');
    });

    act(() => result.current.submit());

    await waitFor(() => expect(signIn).toHaveBeenCalledWith('ana@example.com', 'secret password'));
    expect(result.current.submitError).toBeNull();
  });

  it('explains wrong credentials without saying which part was wrong', async () => {
    jest.mocked(signIn).mockRejectedValue(new ApiError(401, 'x', 'auth.invalid_credentials'));
    const { result } = renderHook(() => useLoginForm(), { wrapper: Wrapper });
    act(() => {
      result.current.setField('email', 'ana@example.com');
      result.current.setField('password', 'wrong');
    });

    act(() => result.current.submit());

    await waitFor(() => expect(result.current.submitError).toBe('El email o la contraseña no son correctos.'));
  });

  it('does not repeat the request on its own after a failure', async () => {
    jest.mocked(signIn).mockRejectedValue(new ApiError(401, 'x', 'auth.invalid_credentials'));
    const { result } = renderHook(() => useLoginForm(), { wrapper: Wrapper });
    act(() => {
      result.current.setField('email', 'ana@example.com');
      result.current.setField('password', 'wrong');
    });

    act(() => result.current.submit());
    await waitFor(() => expect(result.current.submitError).not.toBeNull());

    expect(signIn).toHaveBeenCalledTimes(1);
  });
});
