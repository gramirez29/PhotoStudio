import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { jsonResponse, portalBookingPayload, SAMPLE_TOKEN } from '../test/fixtures';
import { BookingPage } from './BookingPage';

const CODE_SENT = { maskedEmail: 'm***@example.com', expiresAt: '2026-10-16T12:10:00+00:00' };

/**
 * Builds the answer of verifying a code: a session that lasts half an hour from now.
 * @returns The response.
 */
function sessionResponse(): Response {
  return jsonResponse(200, { accessToken: 'portal-jwt', expiresAt: new Date(Date.now() + 30 * 60_000).toISOString() });
}

/** The booking after the client reported 50 000: the proof is in review. */
const BOOKING_WITH_PROOF = {
  ...portalBookingPayload,
  payments: [{ amount: { amount: 50000, currency: 'CRC' }, status: 'PendingVerification', recordedAt: '2026-10-16T12:00:00+00:00' }],
};

describe('payment proof flow', () => {
  const fetchMock = vi.fn<typeof fetch>();

  beforeEach(() => {
    vi.stubGlobal('fetch', fetchMock);
  });

  afterEach(() => {
    fetchMock.mockReset();
    vi.unstubAllGlobals();
  });

  /**
   * Renders the booking page of the sample link with a client that never retries.
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

  /**
   * Answers the requests by their address, in the order each address is asked.
   * @param answers Answers by `METHOD path-suffix`; each one is used once, in order.
   */
  function answer(answers: Record<string, Response[]>): void {
    fetchMock.mockImplementation((input, init) => {
      const url = String(input);
      const suffix = url.replace(/^.*\/public\/bookings\/[^/]+/, '') || '/';
      const key = `${init?.method ?? 'GET'} ${suffix}`;
      const next = answers[key]?.shift();
      if (next === undefined) {
        return Promise.reject(new Error(`Unexpected request: ${key}`));
      }

      return Promise.resolve(next);
    });
  }

  it('walks from the button to a registered proof, and shows it in review', async () => {
    answer({
      'GET /': [jsonResponse(200, portalBookingPayload)],
      'POST /code': [jsonResponse(202, CODE_SENT)],
      'POST /code/verify': [sessionResponse()],
      'POST /payments': [jsonResponse(200, BOOKING_WITH_PROOF)],
    });
    const user = renderPage();

    await user.click(await screen.findByRole('button', { name: 'Subir comprobante' }));
    await user.click(screen.getByRole('button', { name: 'Enviar código' }));
    expect(await screen.findByText('m***@example.com')).toBeInTheDocument();

    await user.type(screen.getByLabelText('Código de 6 dígitos'), '123456');
    await user.click(screen.getByRole('button', { name: 'Verificar' }));

    const amount = await screen.findByLabelText('Monto que transferiste');
    expect(amount).toHaveValue('50000');
    await user.type(screen.getByLabelText(/Número de referencia/), 'SINPE-4455');
    await user.click(screen.getByRole('button', { name: 'Enviar comprobante' }));

    expect(await screen.findByRole('heading', { name: 'Recibimos tu comprobante' })).toBeInTheDocument();
    const paymentsCall = fetchMock.mock.calls.find(([url]) => String(url).endsWith('/payments'));
    expect(JSON.parse(String(paymentsCall?.[1]?.body))).toMatchObject({ amount: 50000, reference: 'SINPE-4455' });
    expect(paymentsCall?.[1]?.headers).toMatchObject({ Authorization: 'Bearer portal-jwt' });

    await user.click(screen.getByRole('button', { name: 'Listo' }));
    expect(await screen.findByText('En revisión')).toBeInTheDocument();
    expect(screen.getByText(/Tu comprobante está en revisión/)).toBeInTheDocument();
  });

  it('reuses the verification for a second proof: no new code is asked for', async () => {
    answer({
      'GET /': [jsonResponse(200, portalBookingPayload)],
      'POST /code': [jsonResponse(202, CODE_SENT)],
      'POST /code/verify': [sessionResponse()],
      'POST /payments': [jsonResponse(200, BOOKING_WITH_PROOF)],
    });
    const user = renderPage();
    await user.click(await screen.findByRole('button', { name: 'Subir comprobante' }));
    await user.click(screen.getByRole('button', { name: 'Enviar código' }));
    await user.type(await screen.findByLabelText('Código de 6 dígitos'), '123456');
    await user.click(screen.getByRole('button', { name: 'Verificar' }));
    await user.click(await screen.findByRole('button', { name: 'Enviar comprobante' }));
    await user.click(await screen.findByRole('button', { name: 'Listo' }));

    await user.click(await screen.findByRole('button', { name: 'Subir comprobante' }));

    expect(await screen.findByLabelText('Monto que transferiste')).toHaveValue('50000');
    expect(screen.queryByRole('button', { name: 'Enviar código' })).not.toBeInTheDocument();
  });

  it('explains a wrong code and lets the client try again', async () => {
    answer({
      'GET /': [jsonResponse(200, portalBookingPayload)],
      'POST /code': [jsonResponse(202, CODE_SENT)],
      'POST /code/verify': [jsonResponse(422, { detail: 'x', code: 'portal.invalid_code' }), sessionResponse()],
    });
    const user = renderPage();
    await user.click(await screen.findByRole('button', { name: 'Subir comprobante' }));
    await user.click(screen.getByRole('button', { name: 'Enviar código' }));
    const field = await screen.findByLabelText('Código de 6 dígitos');

    await user.type(field, '000000');
    await user.click(screen.getByRole('button', { name: 'Verificar' }));
    expect(await screen.findByRole('alert')).toHaveTextContent('no es correcto o ya venció');

    await user.clear(field);
    await user.type(field, '123456');
    await user.click(screen.getByRole('button', { name: 'Verificar' }));
    expect(await screen.findByLabelText('Monto que transferiste')).toBeInTheDocument();
  });

  it('keeps only digits in the code field and asks for all six before calling the API', async () => {
    answer({ 'GET /': [jsonResponse(200, portalBookingPayload)], 'POST /code': [jsonResponse(202, CODE_SENT)] });
    const user = renderPage();
    await user.click(await screen.findByRole('button', { name: 'Subir comprobante' }));
    await user.click(screen.getByRole('button', { name: 'Enviar código' }));
    const field = await screen.findByLabelText('Código de 6 dígitos');

    await user.type(field, '12a-3');
    expect(field).toHaveValue('123');
    await user.click(screen.getByRole('button', { name: 'Verificar' }));

    expect(await screen.findByRole('alert')).toHaveTextContent('Escribe los 6 dígitos');
    expect(fetchMock.mock.calls.some(([url]) => String(url).endsWith('/code/verify'))).toBe(false);
  });

  it('does not let the client go on when the code cannot be sent, and says why', async () => {
    answer({
      'GET /': [jsonResponse(200, portalBookingPayload)],
      'POST /code': [jsonResponse(503, { detail: 'x', code: 'email.delivery_failed' })],
    });
    const user = renderPage();
    await user.click(await screen.findByRole('button', { name: 'Subir comprobante' }));

    await user.click(screen.getByRole('button', { name: 'Enviar código' }));

    expect(await screen.findByRole('alert')).toHaveTextContent('No pudimos enviar el correo');
    expect(screen.queryByLabelText('Código de 6 dígitos')).not.toBeInTheDocument();
  });

  it('starts a wait when the server says the code was asked for too soon', async () => {
    answer({
      'GET /': [jsonResponse(200, portalBookingPayload)],
      'POST /code': [jsonResponse(429, { detail: 'x', code: 'portal.otp_rate_limited' }, { 'Retry-After': '45' })],
    });
    const user = renderPage();
    await user.click(await screen.findByRole('button', { name: 'Subir comprobante' }));

    await user.click(screen.getByRole('button', { name: 'Enviar código' }));

    const button = await screen.findByRole('button', { name: /Espera \d+ s/ });
    expect(button).toBeDisabled();
  });

  it('checks the amount before sending anything', async () => {
    answer({
      'GET /': [jsonResponse(200, portalBookingPayload)],
      'POST /code': [jsonResponse(202, CODE_SENT)],
      'POST /code/verify': [sessionResponse()],
    });
    const user = renderPage();
    await user.click(await screen.findByRole('button', { name: 'Subir comprobante' }));
    await user.click(screen.getByRole('button', { name: 'Enviar código' }));
    await user.type(await screen.findByLabelText('Código de 6 dígitos'), '123456');
    await user.click(screen.getByRole('button', { name: 'Verificar' }));
    const amount = await screen.findByLabelText('Monto que transferiste');

    await user.clear(amount);
    await user.type(amount, '999999');
    await user.click(screen.getByRole('button', { name: 'Enviar comprobante' }));

    expect(await screen.findByRole('alert')).toHaveTextContent('no puede ser mayor');
    expect(fetchMock.mock.calls.some(([url]) => String(url).endsWith('/payments'))).toBe(false);
  });

  it('asks for a new code when the verification expired, and keeps what the client typed', async () => {
    answer({
      'GET /': [jsonResponse(200, portalBookingPayload)],
      'POST /code': [jsonResponse(202, CODE_SENT), jsonResponse(202, CODE_SENT)],
      'POST /code/verify': [sessionResponse(), sessionResponse()],
      'POST /payments': [jsonResponse(401, {}), jsonResponse(200, BOOKING_WITH_PROOF)],
    });
    const user = renderPage();
    await user.click(await screen.findByRole('button', { name: 'Subir comprobante' }));
    await user.click(screen.getByRole('button', { name: 'Enviar código' }));
    await user.type(await screen.findByLabelText('Código de 6 dígitos'), '123456');
    await user.click(screen.getByRole('button', { name: 'Verificar' }));
    const reference = await screen.findByLabelText(/Número de referencia/);
    await user.type(reference, 'REF-9');

    await user.click(screen.getByRole('button', { name: 'Enviar comprobante' }));
    expect(await screen.findByText(/Tu verificación venció/)).toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: 'Enviar código' }));
    await user.type(await screen.findByLabelText('Código de 6 dígitos'), '654321');
    await user.click(screen.getByRole('button', { name: 'Verificar' }));

    expect(await screen.findByLabelText(/Número de referencia/)).toHaveValue('REF-9');
    await user.click(screen.getByRole('button', { name: 'Enviar comprobante' }));
    expect(await screen.findByRole('heading', { name: 'Recibimos tu comprobante' })).toBeInTheDocument();
  });

  it('sends the same idempotency key when the client retries after a failure', async () => {
    answer({
      'GET /': [jsonResponse(200, portalBookingPayload)],
      'POST /code': [jsonResponse(202, CODE_SENT)],
      'POST /code/verify': [sessionResponse()],
      'POST /payments': [jsonResponse(503, { detail: 'x', code: 'server.error' }), jsonResponse(200, BOOKING_WITH_PROOF)],
    });
    const user = renderPage();
    await user.click(await screen.findByRole('button', { name: 'Subir comprobante' }));
    await user.click(screen.getByRole('button', { name: 'Enviar código' }));
    await user.type(await screen.findByLabelText('Código de 6 dígitos'), '123456');
    await user.click(screen.getByRole('button', { name: 'Verificar' }));

    await user.click(await screen.findByRole('button', { name: 'Enviar comprobante' }));
    expect(await screen.findByRole('alert')).toHaveTextContent('No pudimos registrar');
    await user.click(screen.getByRole('button', { name: 'Enviar comprobante' }));

    await waitFor(() => expect(screen.getByRole('heading', { name: 'Recibimos tu comprobante' })).toBeInTheDocument());
    const keys = fetchMock.mock.calls
      .filter(([url]) => String(url).endsWith('/payments'))
      .map(([, init]) => (JSON.parse(String(init?.body)) as { idempotencyKey: string }).idempotencyKey);
    expect(keys).toHaveLength(2);
    expect(keys[0]).toBe(keys[1]);
  });

  it('tells the client to ask the photographer for an email when the booking has none', async () => {
    answer({ 'GET /': [jsonResponse(200, { ...portalBookingPayload, canReceiveCode: false })] });
    renderPage();

    expect(await screen.findByText(/necesita registrar tu correo/)).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Subir comprobante' })).not.toBeInTheDocument();
  });

  it('offers no proof when the API does not allow it', async () => {
    answer({ 'GET /': [jsonResponse(200, { ...portalBookingPayload, status: 'Cancelled', canSubmitProof: false })] });
    renderPage();

    await screen.findByRole('heading', { level: 1 });
    expect(screen.queryByRole('button', { name: 'Subir comprobante' })).not.toBeInTheDocument();
    expect(screen.queryByText(/necesita registrar tu correo/)).not.toBeInTheDocument();
  });

  it('offers no proof once the whole balance is already in review', async () => {
    answer({
      'GET /': [
        jsonResponse(200, {
          ...portalBookingPayload,
          payments: [{ amount: { amount: 100000, currency: 'CRC' }, status: 'PendingVerification', recordedAt: '2026-10-16T12:00:00+00:00' }],
        }),
      ],
    });
    renderPage();

    await screen.findByRole('heading', { level: 1 });
    expect(screen.queryByRole('button', { name: 'Subir comprobante' })).not.toBeInTheDocument();
  });

  it('lets the client cancel at any step', async () => {
    answer({ 'GET /': [jsonResponse(200, portalBookingPayload)] });
    const user = renderPage();
    await user.click(await screen.findByRole('button', { name: 'Subir comprobante' }));

    await user.click(screen.getByRole('button', { name: 'Cancelar' }));

    expect(await screen.findByRole('button', { name: 'Subir comprobante' })).toBeInTheDocument();
  });
});
