import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import App from '../App';
import { jsonResponse, portalBooking, portalBookingPayload, SAMPLE_TOKEN } from '../test/fixtures';
import { BookingPage } from './BookingPage';
import { BookingView } from './BookingView';
import { LoadErrorScreen } from './LoadErrorScreen';

describe('BookingView', () => {
  it('shows whose booking it is, when, and the amounts', () => {
    render(<BookingView booking={portalBooking()} />);

    expect(screen.getByRole('heading', { level: 1, name: 'Hola, María Pérez' })).toBeInTheDocument();
    expect(screen.getByText(/Tu sesión de Retrato Familiar con Ana Pérez/)).toBeInTheDocument();
    expect(screen.getByText(/sábado/i)).toBeInTheDocument();
    expect(screen.getByText('Por confirmar')).toBeInTheDocument();
    expect(screen.getByText('Saldo')).toBeInTheDocument();
    expect(screen.getByText('Pendiente de firma')).toBeInTheDocument();
  });

  it('leaves out the photographer when the API sent no name', () => {
    render(<BookingView booking={portalBooking({ photographerName: '' })} />);

    expect(screen.getByText('Tu sesión de Retrato Familiar')).toBeInTheDocument();
  });

  it('says what comes next', () => {
    render(<BookingView booking={portalBooking()} />);

    expect(screen.getByLabelText('Qué sigue')).toHaveTextContent('falta el anticipo');
  });

  it('lists the payments with their state in words', () => {
    render(
      <BookingView
        booking={portalBooking({
          payments: [
            { amount: { amount: 50000, currency: 'CRC' }, status: 'PendingVerification', recordedAt: '2026-10-16T12:00:00+00:00' },
            { amount: { amount: 20000, currency: 'CRC' }, status: 'Rejected', recordedAt: '2026-10-15T12:00:00+00:00' },
          ],
        })}
      />,
    );

    expect(screen.getByText('En revisión')).toBeInTheDocument();
    expect(screen.getByText('No aceptado')).toBeInTheDocument();
  });

  it('shows no payments section when there are none', () => {
    render(<BookingView booking={portalBooking()} />);

    expect(screen.queryByRole('heading', { name: 'Pagos' })).not.toBeInTheDocument();
  });

  it('shows the contract as signed', () => {
    render(<BookingView booking={portalBooking({ contractSigned: true })} />);

    expect(screen.getByText('Firmado')).toBeInTheDocument();
  });
});

describe('LoadErrorScreen', () => {
  it('offers to try again only when the view allows it', async () => {
    const onRetry = vi.fn();
    const { rerender } = render(<LoadErrorScreen view={{ title: 'Sin conexión', message: 'Revisa tu internet.', canRetry: true }} onRetry={onRetry} />);

    await userEvent.click(screen.getByRole('button', { name: 'Intentar de nuevo' }));
    rerender(<LoadErrorScreen view={{ title: 'Enlace', message: 'No válido.', canRetry: false }} onRetry={onRetry} />);

    expect(onRetry).toHaveBeenCalledTimes(1);
    expect(screen.queryByRole('button')).not.toBeInTheDocument();
    expect(screen.getByRole('alert')).toHaveTextContent('No válido.');
  });
});

describe('BookingPage and App', () => {
  const fetchMock = vi.fn<typeof fetch>();

  beforeEach(() => {
    vi.stubGlobal('fetch', fetchMock);
  });

  afterEach(() => {
    fetchMock.mockReset();
    vi.unstubAllGlobals();
    window.history.pushState({}, '', '/');
  });

  /**
   * Renders the booking page of a token with a query client that never retries, so a failure shows at once.
   * @returns Nothing.
   */
  function renderPage(): void {
    const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
    render(
      <QueryClientProvider client={client}>
        <BookingPage token={SAMPLE_TOKEN} />
      </QueryClientProvider>,
    );
  }

  it('shows the loading message and then the booking', async () => {
    fetchMock.mockResolvedValue(jsonResponse(200, portalBookingPayload));

    renderPage();

    expect(screen.getByRole('status')).toHaveTextContent('Cargando tu reserva');
    expect(await screen.findByRole('heading', { level: 1, name: 'Hola, María Pérez' })).toBeInTheDocument();
    expect(fetchMock.mock.calls[0]?.[0]).toContain(`/public/bookings/${SAMPLE_TOKEN}`);
  });

  it('explains an invalid link without offering to retry', async () => {
    fetchMock.mockResolvedValue(jsonResponse(404, { detail: 'x', code: 'resource.not_found' }));

    renderPage();

    expect(await screen.findByRole('alert')).toHaveTextContent('Este enlace no es válido');
    expect(screen.queryByRole('button', { name: 'Intentar de nuevo' })).not.toBeInTheDocument();
  });

  it('offers to try again after a network failure, and loads the booking on the second try', async () => {
    fetchMock.mockRejectedValueOnce(new TypeError('Failed to fetch')).mockResolvedValueOnce(jsonResponse(200, portalBookingPayload));

    renderPage();
    await userEvent.click(await screen.findByRole('button', { name: 'Intentar de nuevo' }));

    await waitFor(() => expect(screen.getByRole('heading', { level: 1, name: 'Hola, María Pérez' })).toBeInTheDocument());
  });

  it('reads the token from the address', async () => {
    window.history.pushState({}, '', `/b/${SAMPLE_TOKEN}`);
    fetchMock.mockResolvedValue(jsonResponse(200, portalBookingPayload));

    render(<App />);

    expect(await screen.findByRole('heading', { level: 1, name: 'Hola, María Pérez' })).toBeInTheDocument();
    expect(screen.getByText('PhotoSense Studio')).toBeInTheDocument();
  });

  it('shows the main page at the root, and tells where a booking is opened from, without calling the API', () => {
    window.history.pushState({}, '', '/');

    render(<App />);

    expect(screen.getByRole('heading', { level: 1, name: 'Bienvenido a PhotoSense Studio' })).toBeInTheDocument();
    expect(screen.getByRole('heading', { name: '¿Tienes una reserva?' })).toBeInTheDocument();
    expect(fetchMock).not.toHaveBeenCalled();
  });

  it('says the page does not exist for an address that only looks like a link, without calling the API', () => {
    window.history.pushState({}, '', '/b/short');

    render(<App />);

    expect(screen.getByRole('heading', { name: 'Esta página no existe' })).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 've a la página principal' })).toHaveAttribute('href', '/');
    expect(fetchMock).not.toHaveBeenCalled();
  });

  it('shows no test-site mark in production', () => {
    window.history.pushState({}, '', '/');

    render(<App />);

    expect(screen.queryByRole('note')).not.toBeInTheDocument();
  });
});
