import { act, renderHook, waitFor } from '@testing-library/react-native';
import { ApiError } from '../../api/httpClient';
import { register } from '../../session/sessionManager';
import { createQueryWrapper } from '../__fixtures__/queryWrapper';
import { useRegisterForm } from '../useRegisterForm';

jest.mock('../../session/sessionManager', () => ({ register: jest.fn() }));

describe('useRegisterForm', () => {
  const { client, Wrapper } = createQueryWrapper();

  afterEach(() => {
    client.clear();
  });

  beforeEach(() => {
    jest.clearAllMocks();
  });

  /**
   * Types a valid form.
   * @param form Hook result holder.
   * @param form.current Current state of the hook.
   */
  function fillValidForm(form: { readonly current: ReturnType<typeof useRegisterForm> }): void {
    act(() => {
      form.current.setField('name', '  Ana Pérez ');
      form.current.setPhone('70189220');
      form.current.setField('email', '  Ana@Example.com ');
      form.current.setField('username', 'Ana.Photo');
      form.current.setField('password', 'a long password');
      form.current.setField('confirmPassword', 'a long password');
    });
  }

  it('shows the phone as 0000-0000 while it is typed', () => {
    const { result } = renderHook(() => useRegisterForm(), { wrapper: Wrapper });

    act(() => result.current.setPhone('70189220'));

    expect(result.current.values.phone).toBe('7018-9220');
  });

  it('shows the validation messages and does not call the backend when the form is empty', () => {
    const { result } = renderHook(() => useRegisterForm(), { wrapper: Wrapper });

    act(() => result.current.submit());

    expect(Object.keys(result.current.errors).sort()).toEqual(['email', 'name', 'password', 'phone', 'username']);
    expect(register).not.toHaveBeenCalled();
  });

  it('clears the message of a field as soon as it is edited', () => {
    const { result } = renderHook(() => useRegisterForm(), { wrapper: Wrapper });
    act(() => result.current.submit());

    act(() => result.current.setField('name', 'A'));
    act(() => result.current.setPhone('7'));

    expect(result.current.errors.name).toBeUndefined();
    expect(result.current.errors.phone).toBeUndefined();
    expect(result.current.errors.username).toBeDefined();
  });

  it('creates the account with the normalized data', async () => {
    jest.mocked(register).mockResolvedValue(undefined);
    const { result } = renderHook(() => useRegisterForm(), { wrapper: Wrapper });
    fillValidForm(result);

    act(() => result.current.submit());

    await waitFor(() =>
      expect(register).toHaveBeenCalledWith({
        username: 'ana.photo',
        email: 'ana@example.com',
        password: 'a long password',
        name: 'Ana Pérez',
        phone: '+50670189220',
      }),
    );
    expect(result.current.submitError).toBeNull();
  });

  it('explains that the username is taken, and does not repeat the request on its own', async () => {
    jest.mocked(register).mockRejectedValue(new ApiError(409, 'x', 'user.username_taken'));
    const { result } = renderHook(() => useRegisterForm(), { wrapper: Wrapper });
    fillValidForm(result);

    act(() => result.current.submit());

    await waitFor(() => expect(result.current.submitError).toBe('Ese usuario ya existe. Elige otro.'));
    expect(register).toHaveBeenCalledTimes(1);
  });
});
