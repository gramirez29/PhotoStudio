import { act, renderHook, waitFor } from '@testing-library/react-native';
import { accountApi } from '../../api/client';
import { ApiError } from '../../api/httpClient';
import { adoptSession } from '../../session/sessionManager';
import { useSessionStore } from '../../session/sessionStore';
import type { AuthSessionResponse } from '../../types/api/auth';
import { createQueryWrapper } from '../__fixtures__/queryWrapper';
import { useVerifyEmailForm } from '../useVerifyEmailForm';

jest.mock('../../api/client', () => ({ accountApi: { verifyEmail: jest.fn(), resendEmailVerification: jest.fn() } }));
jest.mock('../../session/sessionManager', () => ({ adoptSession: jest.fn() }));

/** The session the backend returns once the email is verified. */
const verifiedSession: AuthSessionResponse = {
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

describe('useVerifyEmailForm', () => {
  const { client, Wrapper } = createQueryWrapper();

  beforeEach(() => {
    jest.clearAllMocks();
    useSessionStore.getState().setSignedIn({
      accessToken: 'access-1',
      accessTokenExpiresAt: Date.now() + 900_000,
      photographerId: verifiedSession.photographerId,
      username: 'ana',
      email: 'ana@example.com',
      name: 'Ana Pérez',
      isSystemOperator: false,
      emailVerified: false,
    });
  });

  afterEach(() => {
    client.clear();
    useSessionStore.getState().setSignedOut();
  });

  it('shows the address the code goes to, partly hidden', () => {
    const { result } = renderHook(() => useVerifyEmailForm(), { wrapper: Wrapper });

    expect(result.current.maskedEmail).toBe('a***@example.com');
  });

  it('keeps only the digits of what is typed', () => {
    const { result } = renderHook(() => useVerifyEmailForm(), { wrapper: Wrapper });

    act(() => result.current.setCode('12 34a56789'));

    expect(result.current.code).toBe('123456');
  });

  it('does not call the backend while the code is incomplete, and says what is missing', () => {
    const { result } = renderHook(() => useVerifyEmailForm(), { wrapper: Wrapper });
    act(() => result.current.setCode('123'));

    act(() => result.current.verify());

    expect(result.current.codeError).toBe('Escribe los 6 dígitos del código.');
    expect(accountApi.verifyEmail).not.toHaveBeenCalled();
  });

  it('sends the code and adopts the new session, which opens the app', async () => {
    jest.mocked(accountApi.verifyEmail).mockResolvedValue(verifiedSession);
    const { result } = renderHook(() => useVerifyEmailForm(), { wrapper: Wrapper });
    act(() => result.current.setCode('123456'));

    act(() => result.current.verify());

    await waitFor(() => expect(adoptSession).toHaveBeenCalledWith(verifiedSession));
    expect(accountApi.verifyEmail).toHaveBeenCalledWith({ code: '123456' });
  });

  it('explains a wrong code and does not adopt a session', async () => {
    jest.mocked(accountApi.verifyEmail).mockRejectedValue(new ApiError(422, 'wrong', 'auth.invalid_email_code'));
    const { result } = renderHook(() => useVerifyEmailForm(), { wrapper: Wrapper });
    act(() => result.current.setCode('000000'));

    act(() => result.current.verify());

    await waitFor(() => expect(result.current.submitError).toMatch(/El código no es correcto o ya venció/));
    expect(adoptSession).not.toHaveBeenCalled();
  });

  it('asks for a new code, says where it went and makes the user wait before asking again', async () => {
    jest.mocked(accountApi.resendEmailVerification).mockResolvedValue({ maskedEmail: 'a***@example.com', expiresAt: '2026-10-08T21:10:00+00:00' });
    const { result } = renderHook(() => useVerifyEmailForm(), { wrapper: Wrapper });

    act(() => result.current.resend());

    await waitFor(() => expect(result.current.notice).toBe('Te enviamos un código nuevo a a***@example.com.'));
    expect(result.current.cooldownSeconds).toBeGreaterThan(0);
  });

  it('explains that a code was sent a moment ago when asking again too soon', async () => {
    jest.mocked(accountApi.resendEmailVerification).mockRejectedValue(new ApiError(429, 'wait', 'auth.email_code_rate_limited'));
    const { result } = renderHook(() => useVerifyEmailForm(), { wrapper: Wrapper });

    act(() => result.current.resend());

    await waitFor(() => expect(result.current.submitError).toMatch(/Ya te enviamos un código hace poco/));
    expect(result.current.cooldownSeconds).toBe(0);
  });
});
