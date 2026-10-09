import { fireEvent, render, screen } from '@testing-library/react-native';
import { maintenanceApi } from '../../api/client';
import { ApiError } from '../../api/httpClient';
import { createQueryWrapper } from '../../hooks/__fixtures__/queryWrapper';
import { MaintenanceButton } from '../MaintenanceButton';

jest.mock('../../api/client', () => ({
  maintenanceApi: { runMaintenance: jest.fn() },
}));

describe('MaintenanceButton', () => {
  const { client, Wrapper } = createQueryWrapper();

  beforeEach(() => {
    jest.clearAllMocks();
  });

  afterEach(() => {
    client.clear();
  });

  it('runs the maintenance pass when pressed and shows what it did', async () => {
    jest.mocked(maintenanceApi.runMaintenance).mockResolvedValue({
      bookingsExpired: 2,
      bookingsSkipped: 0,
      eventsProcessed: 3,
      notificationsDelivered: 0,
      moreWorkPending: false,
    });
    render(<MaintenanceButton />, { wrapper: Wrapper });

    fireEvent.press(screen.getByRole('button', { name: 'Liberar reservas vencidas' }));

    expect(await screen.findByText('2 reservas vencidas liberadas.')).toBeTruthy();
    expect(maintenanceApi.runMaintenance).toHaveBeenCalledTimes(1);
  });

  it('disables the button while the request runs', async () => {
    let finish: (() => void) | undefined;
    jest.mocked(maintenanceApi.runMaintenance).mockImplementation(
      () =>
        new Promise((resolve) => {
          finish = () => resolve({ bookingsExpired: 0, bookingsSkipped: 0, eventsProcessed: 0, notificationsDelivered: 0, moreWorkPending: false });
        }),
    );
    render(<MaintenanceButton />, { wrapper: Wrapper });

    fireEvent.press(screen.getByRole('button', { name: 'Liberar reservas vencidas' }));

    const pending = await screen.findByRole('button', { name: 'Actualizando…' });
    expect(pending.props.accessibilityState).toMatchObject({ disabled: true });

    finish?.();
    expect(await screen.findByText('No había reservas vencidas.')).toBeTruthy();
  });

  it('explains the rate limit', async () => {
    jest.mocked(maintenanceApi.runMaintenance).mockRejectedValue(new ApiError(429, 'Too many requests.', 'rate_limit.exceeded'));
    render(<MaintenanceButton />, { wrapper: Wrapper });

    fireEvent.press(screen.getByRole('button', { name: 'Liberar reservas vencidas' }));

    expect(await screen.findByText('Ya se ejecutó hace un momento. Espera un minuto e inténtalo de nuevo.')).toBeTruthy();
  });
});
