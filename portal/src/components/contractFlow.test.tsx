import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { jsonResponse, portalBookingPayload, SAMPLE_TOKEN } from '../test/fixtures';
import { BookingPage } from './BookingPage';

const CODE_SENT = { maskedEmail: 'm***@example.com', expiresAt: '2026-10-16T12:10:00+00:00' };

const CONTRACT_TEXT = [
  'CONTRATO DE SERVICIOS FOTOGRÁFICOS – PHOTOSENSE STUDIO',
  'Entre PhotoSense Studio, en adelante "el Fotógrafo", y María Pérez, cédula ________, en adelante "el Cliente", se acuerda lo siguiente:',
  '',
  '12. Uso de imagen. El Cliente elige una opción:',
  '    [ ] Autorizo que el Fotógrafo publique fotos de la sesión.',
  '    [ ] No autorizo la publicación de las fotos.',
].join('\n');

/**
 * Builds the answer of verifying a code: a session that lasts half an hour from now.
 * @returns The response.
 */
function sessionResponse(): Response {
  return jsonResponse(200, { accessToken: 'portal-jwt', expiresAt: new Date(Date.now() + 30 * 60_000).toISOString() });
}

/** The booking after the client signed. */
const SIGNED_BOOKING = { ...portalBookingPayload, contractSigned: true, canSignContract: false };

describe('contract flow', () => {
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

  /**
   * Walks from the button to the contract: asks for the code, types it and verifies it.
   * @param user The user event driver.
   */
  async function openContract(user: ReturnType<typeof userEvent.setup>): Promise<void> {
    await user.click(await screen.findByRole('button', { name: 'Leer y firmar contrato' }));
    await user.click(screen.getByRole('button', { name: 'Enviar código' }));
    expect(await screen.findByText('m***@example.com')).toBeInTheDocument();
    await user.type(screen.getByLabelText('Código de 6 dígitos'), '123456');
    await user.click(screen.getByRole('button', { name: 'Verificar' }));
  }

  /**
   * Fills the form with valid values, leaving the acceptance for the test to tick.
   * @param user The user event driver.
   */
  async function fillForm(user: ReturnType<typeof userEvent.setup>): Promise<void> {
    await user.type(await screen.findByLabelText(/Tu nombre completo/), 'María Pérez');
    await user.type(screen.getByLabelText('Cédula o DIMEX'), '1-1234-5678');
    await user.type(screen.getByLabelText(/Cuántas personas|personas vienen/), '3');
    await user.type(screen.getByLabelText(/Lugar de la sesión/), 'Parque La Sabana');
    await user.click(screen.getByLabelText(/No autorizo la publicación/));
  }

  it('offers the contract while it is unsigned, and says nothing about signing when it is not possible', async () => {
    answer({ 'GET /': [jsonResponse(200, { ...portalBookingPayload, canSignContract: false, canReceiveCode: false })] });
    renderPage();

    expect(await screen.findByText('Pendiente de firma')).toBeInTheDocument();
    expect(screen.getByText(/debe registrar tu correo/)).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Leer y firmar contrato' })).not.toBeInTheDocument();
  });

  it('walks from the button to a signed contract, sending what the client declared', async () => {
    answer({
      'GET /': [jsonResponse(200, portalBookingPayload)],
      'POST /code': [jsonResponse(202, CODE_SENT)],
      'POST /code/verify': [sessionResponse()],
      'GET /contract': [jsonResponse(200, { templateVersion: 'v1', text: CONTRACT_TEXT, signed: false })],
      'POST /contract': [jsonResponse(200, SIGNED_BOOKING)],
    });
    const user = renderPage();

    await openContract(user);

    const document = await screen.findByRole('region', { name: 'Texto del contrato' });
    expect(within(document).getByText('CONTRATO DE SERVICIOS FOTOGRÁFICOS – PHOTOSENSE STUDIO')).toBeInTheDocument();
    expect(within(document).getByText(/cédula ________/)).toBeInTheDocument();

    await fillForm(user);
    await user.click(screen.getByLabelText('He leído el contrato completo y lo acepto.'));
    await user.click(screen.getByRole('button', { name: 'Firmar contrato' }));

    expect(await screen.findByRole('heading', { name: 'Firmaste tu contrato' })).toBeInTheDocument();
    const signCall = fetchMock.mock.calls.find(([url, init]) => String(url).endsWith('/contract') && init?.method === 'POST');
    expect(JSON.parse(String(signCall?.[1]?.body))).toEqual({
      templateVersion: 'v1',
      signerName: 'María Pérez',
      idNumber: '112345678',
      participantCount: 3,
      sessionLocation: 'Parque La Sabana',
      imageUse: 'NotAuthorized',
      acceptsTerms: true,
    });
    expect(signCall?.[1]?.headers).toMatchObject({ Authorization: 'Bearer portal-jwt' });

    await user.click(screen.getByRole('button', { name: 'Listo' }));
    expect(await screen.findByText('Firmado')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Leer y firmar contrato' })).not.toBeInTheDocument();
  });

  it('does not send anything until the form is complete, and says what is missing next to each field', async () => {
    answer({
      'GET /': [jsonResponse(200, portalBookingPayload)],
      'POST /code': [jsonResponse(202, CODE_SENT)],
      'POST /code/verify': [sessionResponse()],
      'GET /contract': [jsonResponse(200, { templateVersion: 'v1', text: CONTRACT_TEXT, signed: false })],
    });
    const user = renderPage();
    await openContract(user);

    await user.click(await screen.findByRole('button', { name: 'Firmar contrato' }));

    expect(await screen.findByText('Escribe tu nombre completo.')).toBeInTheDocument();
    expect(screen.getByText(/Escribe tu cédula/)).toBeInTheDocument();
    expect(screen.getByText('Elige si autorizas o no el uso de las fotos.')).toBeInTheDocument();
    expect(screen.getByText('Debes aceptar el contrato para firmarlo.')).toBeInTheDocument();
    expect(fetchMock.mock.calls.some(([url, init]) => String(url).endsWith('/contract') && init?.method === 'POST')).toBe(false);
  });

  it('shows the server reason when it refuses the signature, and keeps what was typed', async () => {
    answer({
      'GET /': [jsonResponse(200, portalBookingPayload)],
      'POST /code': [jsonResponse(202, CODE_SENT)],
      'POST /code/verify': [sessionResponse()],
      'GET /contract': [jsonResponse(200, { templateVersion: 'v1', text: CONTRACT_TEXT, signed: false })],
      'POST /contract': [jsonResponse(409, { code: 'contract.template_changed', detail: 'changed' })],
    });
    const user = renderPage();
    await openContract(user);
    await fillForm(user);
    await user.click(screen.getByLabelText('He leído el contrato completo y lo acepto.'));

    await user.click(screen.getByRole('button', { name: 'Firmar contrato' }));

    expect(await screen.findByText(/El contrato cambió mientras lo leías/)).toBeInTheDocument();
    expect(screen.getByLabelText('Cédula o DIMEX')).toHaveValue('1-1234-5678');
  });

  it('goes back to the code when the server refuses the session, keeping what was typed', async () => {
    answer({
      'GET /': [jsonResponse(200, portalBookingPayload)],
      'POST /code': [jsonResponse(202, CODE_SENT), jsonResponse(202, CODE_SENT)],
      'POST /code/verify': [sessionResponse(), sessionResponse()],
      'GET /contract': [
        jsonResponse(200, { templateVersion: 'v1', text: CONTRACT_TEXT, signed: false }),
        jsonResponse(200, { templateVersion: 'v1', text: CONTRACT_TEXT, signed: false }),
      ],
      'POST /contract': [jsonResponse(401, { code: null })],
    });
    const user = renderPage();
    await openContract(user);
    await fillForm(user);
    await user.click(screen.getByLabelText('He leído el contrato completo y lo acepto.'));

    await user.click(screen.getByRole('button', { name: 'Firmar contrato' }));

    expect(await screen.findByText(/Tu verificación venció/)).toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: 'Enviar código' }));
    await user.type(await screen.findByLabelText('Código de 6 dígitos'), '123456');
    await user.click(screen.getByRole('button', { name: 'Verificar' }));
    expect(await screen.findByLabelText('Cédula o DIMEX')).toHaveValue('1-1234-5678');
  });

  it('shows a contract that is already signed as text only, with no form', async () => {
    answer({
      'GET /': [jsonResponse(200, SIGNED_BOOKING)],
      'POST /code': [jsonResponse(202, CODE_SENT)],
      'POST /code/verify': [sessionResponse()],
      'GET /contract': [jsonResponse(200, { templateVersion: 'v1', text: CONTRACT_TEXT, signed: true })],
    });
    const user = renderPage();

    await user.click(await screen.findByRole('button', { name: 'Ver contrato firmado' }));
    await user.click(screen.getByRole('button', { name: 'Enviar código' }));
    await user.type(await screen.findByLabelText('Código de 6 dígitos'), '123456');
    await user.click(screen.getByRole('button', { name: 'Verificar' }));

    expect(await screen.findByRole('region', { name: 'Texto del contrato' })).toBeInTheDocument();
    expect(screen.getByText('Este contrato ya está firmado.')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Firmar contrato' })).not.toBeInTheDocument();
  });

  it('asks for the email code only once for the contract and the payment proof', async () => {
    answer({
      'GET /': [jsonResponse(200, portalBookingPayload)],
      'POST /code': [jsonResponse(202, CODE_SENT)],
      'POST /code/verify': [sessionResponse()],
      'GET /contract': [jsonResponse(200, { templateVersion: 'v1', text: CONTRACT_TEXT, signed: false })],
    });
    const user = renderPage();
    await openContract(user);
    await user.click(await screen.findByRole('button', { name: 'Cancelar' }));

    await user.click(await screen.findByRole('button', { name: 'Subir comprobante' }));

    expect(await screen.findByLabelText('Monto que transferiste')).toBeInTheDocument();
    await waitFor(() => {
      expect(fetchMock.mock.calls.filter(([url]) => String(url).endsWith('/code')).length).toBe(1);
    });
  });
});
