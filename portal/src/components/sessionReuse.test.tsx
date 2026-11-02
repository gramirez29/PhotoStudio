import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { jsonResponse, portalBookingPayload, SAMPLE_TOKEN } from '../test/fixtures';
import { storeSession } from '../utils/portalSession';
import { BookingPage } from './BookingPage';

describe('the session remembered in the browser', () => {
  const fetchMock = vi.fn<typeof fetch>();

  beforeEach(() => {
    vi.stubGlobal('fetch', fetchMock);
    fetchMock.mockImplementation((input) => {
      const url = String(input);
      if (url.endsWith('/gallery/summary')) {
        return Promise.resolve(jsonResponse(404, { code: 'resource.not_found', detail: 'no' }));
      }

      if (url.endsWith('/contract')) {
        return Promise.resolve(jsonResponse(200, { templateVersion: 'v1', text: 'Texto del contrato firmado', signed: true }));
      }

      return Promise.resolve(jsonResponse(200, { ...portalBookingPayload, contractSigned: true, canSignContract: false }));
    });
  });

  afterEach(() => {
    fetchMock.mockReset();
    vi.unstubAllGlobals();
  });

  /**
   * Renders the booking page of the sample link.
   * @returns The user event driver.
   */
  function renderPage(): ReturnType<typeof userEvent.setup> {
    const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
    render(
      <QueryClientProvider client={client}>
        <BookingPage token={SAMPLE_TOKEN} />
      </QueryClientProvider>,
    );
    return userEvent.setup();
  }

  it('says nothing while the client has not confirmed their email', async () => {
    renderPage();

    expect(await screen.findByText('Firmado')).toBeInTheDocument();
    expect(screen.queryByText(/ya está confirmado en este dispositivo/)).not.toBeInTheDocument();
  });

  it('is restored on the next visit, tells until when, and is forgotten when the client closes it', async () => {
    storeSession(SAMPLE_TOKEN, { accessToken: 'portal-jwt', expiresAt: new Date(Date.now() + 20 * 24 * 60 * 60_000).toISOString() });
    const user = renderPage();

    expect(await screen.findByText(/Tu correo ya está confirmado en este dispositivo hasta el/)).toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: 'Cerrar sesión en este dispositivo' }));

    await waitFor(() => expect(screen.queryByText(/ya está confirmado en este dispositivo/)).not.toBeInTheDocument());
    expect(window.localStorage.length).toBe(0);
  });

  it('opens the signed contract without asking for another code', async () => {
    storeSession(SAMPLE_TOKEN, { accessToken: 'portal-jwt', expiresAt: new Date(Date.now() + 20 * 24 * 60 * 60_000).toISOString() });
    const user = renderPage();

    await user.click(await screen.findByRole('button', { name: 'Ver contrato firmado' }));

    expect(await screen.findByText(/Texto del contrato firmado/)).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Enviar código' })).not.toBeInTheDocument();
    expect(fetchMock.mock.calls.some(([url]) => String(url).endsWith('/code'))).toBe(false);
  });

  it('asks for the code when there is no session, to read the signed contract', async () => {
    const user = renderPage();

    await user.click(await screen.findByRole('button', { name: 'Ver contrato firmado' }));

    expect(await screen.findByRole('button', { name: 'Enviar código' })).toBeInTheDocument();
  });

  it('is not restored when it already expired', async () => {
    storeSession(SAMPLE_TOKEN, { accessToken: 'portal-jwt', expiresAt: new Date(Date.now() - 60_000).toISOString() });
    renderPage();

    expect(await screen.findByText('Firmado')).toBeInTheDocument();
    expect(screen.queryByText(/ya está confirmado en este dispositivo/)).not.toBeInTheDocument();
  });
});
