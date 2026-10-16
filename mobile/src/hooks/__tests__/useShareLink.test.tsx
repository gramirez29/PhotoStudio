import { act, renderHook, waitFor } from '@testing-library/react-native';
import { Alert, type AlertButton } from 'react-native';
import { bookingsApi } from '../../api/client';
import { parseBooking } from '../../api/bookingsApi';
import { bookingPayload } from '../../api/__fixtures__/testResponses';
import { openExternalUrl } from '../../navigation/externalLinks';
import { createQueryWrapper } from '../__fixtures__/queryWrapper';
import { useShareLink } from '../useShareLink';

jest.mock('../../api/client', () => ({ bookingsApi: { createBookingLink: jest.fn() } }));
jest.mock('../../navigation/externalLinks');

const link = { token: 'secret', url: 'https://book.example.test/b/secret', expiresAt: '2027-01-01T00:00:00+00:00' };
const booking = { ...parseBooking(bookingPayload), clientPhone: '+50670189220', clientEmail: 'maria@example.com' };

/**
 * Taps the confirm button of the last dialog.
 */
function confirmDialog(): void {
  const buttons: readonly AlertButton[] = jest.mocked(Alert.alert).mock.calls.at(-1)?.[2] ?? [];
  buttons.find((button) => button.text === 'Crear y enviar')?.onPress?.();
}

describe('useShareLink', () => {
  const { client, Wrapper } = createQueryWrapper();

  afterEach(() => {
    client.clear();
  });

  beforeEach(() => {
    jest.clearAllMocks();
    jest.spyOn(Alert, 'alert').mockImplementation(() => undefined);
  });

  it('warns that a previous link stops working and creates nothing until the photographer accepts', () => {
    const { result } = renderHook(() => useShareLink(booking), { wrapper: Wrapper });

    act(() => result.current.share());

    expect(Alert.alert).toHaveBeenCalledWith('Enlace para el cliente', expect.stringContaining('dejará de funcionar'), expect.any(Array));
    expect(bookingsApi.createBookingLink).not.toHaveBeenCalled();
  });

  it('creates the link and opens WhatsApp with the message and the URL', async () => {
    jest.mocked(bookingsApi.createBookingLink).mockResolvedValue(link);
    jest.mocked(openExternalUrl).mockResolvedValue(true);
    const { result } = renderHook(() => useShareLink(booking), { wrapper: Wrapper });

    act(() => result.current.share());
    act(() => confirmDialog());

    await waitFor(() => expect(openExternalUrl).toHaveBeenCalledTimes(1));
    expect(bookingsApi.createBookingLink).toHaveBeenCalledWith(booking.id);
    const opened = jest.mocked(openExternalUrl).mock.calls[0]?.[0] ?? '';
    expect(opened.startsWith('https://wa.me/50670189220?text=')).toBe(true);
    expect(decodeURIComponent(opened)).toContain(link.url);
    expect(result.current.errorMessage).toBeNull();
  });

  it('says so when WhatsApp cannot be opened', async () => {
    jest.mocked(bookingsApi.createBookingLink).mockResolvedValue(link);
    jest.mocked(openExternalUrl).mockResolvedValue(false);
    const { result } = renderHook(() => useShareLink(booking), { wrapper: Wrapper });

    act(() => result.current.share());
    act(() => confirmDialog());

    await waitFor(() => expect(result.current.errorMessage).toContain('WhatsApp'));
  });

  it('says so when the client phone has no digits, without opening anything', async () => {
    jest.mocked(bookingsApi.createBookingLink).mockResolvedValue(link);
    const { result } = renderHook(() => useShareLink({ ...booking, clientPhone: '---' }), { wrapper: Wrapper });

    act(() => result.current.share());
    act(() => confirmDialog());

    await waitFor(() => expect(result.current.errorMessage).toContain('teléfono'));
    expect(openExternalUrl).not.toHaveBeenCalled();
  });

  it('reports a failure to create the link', async () => {
    jest.mocked(bookingsApi.createBookingLink).mockRejectedValue(new Error('offline'));
    const { result } = renderHook(() => useShareLink(booking), { wrapper: Wrapper });

    act(() => result.current.share());
    act(() => confirmDialog());

    await waitFor(() => expect(result.current.errorMessage).toContain('conectar'));
    expect(openExternalUrl).not.toHaveBeenCalled();
  });
});
