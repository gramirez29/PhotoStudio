import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { jsonResponse, portalBookingPayload, SAMPLE_TOKEN } from '../test/fixtures';
import { BookingPage } from './BookingPage';

const CODE_SENT = { maskedEmail: 'm***@example.com', expiresAt: '2026-10-16T12:10:00+00:00' };

/** Identifiers of the four proofs of the gallery of the tests. */
const IDS = ['p1', 'p2', 'p3', 'p4'];

/**
 * Builds the gallery as the API sends it: four proofs and two photos included.
 * @param overrides What differs from a gallery whose selection is open.
 * @returns The body.
 */
function galleryBody(overrides: Record<string, unknown> = {}): Record<string, unknown> {
  return {
    status: 'InSelection',
    selectablePhotos: 2,
    extraPhotoPrice: { amount: 3000, currency: 'CRC' },
    photos: IDS.map((id) => ({ id, url: `https://account.r2.cloudflarestorage.com/${id}?sig=secret`, expiresAt: '2026-10-30T12:20:00+00:00' })),
    selection: null,
    ...overrides,
  };
}

/**
 * Builds the answer of verifying a code: a session that lasts half an hour from now.
 * @returns The response.
 */
function sessionResponse(): Response {
  return jsonResponse(200, { accessToken: 'portal-jwt', expiresAt: new Date(Date.now() + 30 * 60_000).toISOString() });
}

describe('the gallery of the client', () => {
  const fetchMock = vi.fn<typeof fetch>();

  beforeEach(() => {
    vi.stubGlobal('fetch', fetchMock);
  });

  afterEach(() => {
    fetchMock.mockReset();
    vi.unstubAllGlobals();
    window.sessionStorage.clear();
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
   * Answers the requests by their address, in the order each address is asked; the last answer of an address stays for the next asks.
   * @param answers Answers by `METHOD path-suffix`.
   */
  function answer(answers: Record<string, Response[]>): void {
    fetchMock.mockImplementation((input, init) => {
      const url = String(input);
      const suffix = url.replace(/^.*\/public\/bookings\/[^/]+/, '') || '/';
      const key = `${init?.method ?? 'GET'} ${suffix}`;
      const queue = answers[key];
      const next = queue === undefined ? undefined : queue.length > 1 ? queue.shift() : queue[0]?.clone();
      return next === undefined ? Promise.reject(new Error(`Unexpected request: ${key}`)) : Promise.resolve(next);
    });
  }

  /**
   * Walks from the card to the gallery: asks for the code, types it and verifies it.
   * @param user The user event driver.
   * @param button Text of the button of the card.
   */
  async function openGallery(user: ReturnType<typeof userEvent.setup>, button = 'Ver mis fotos y elegir'): Promise<void> {
    await user.click(await screen.findByRole('button', { name: button }));
    await user.click(screen.getByRole('button', { name: 'Enviar código' }));
    expect(await screen.findByText('m***@example.com')).toBeInTheDocument();
    await user.type(screen.getByLabelText('Código de 6 dígitos'), '123456');
    await user.click(screen.getByRole('button', { name: 'Verificar' }));
  }

  /** The answers every scenario needs: the booking, the summary and the code. */
  function baseAnswers(extra: Record<string, Response[]> = {}): Record<string, Response[]> {
    return {
      'GET /': [jsonResponse(200, portalBookingPayload)],
      'GET /gallery/summary': [jsonResponse(200, { status: 'InSelection' })],
      'POST /code': [jsonResponse(202, CODE_SENT)],
      'POST /code/verify': [sessionResponse()],
      ...extra,
    };
  }

  it('shows nothing about the gallery when there is none for the client to see', async () => {
    answer(baseAnswers({ 'GET /gallery/summary': [jsonResponse(404, { code: 'resource.not_found', detail: 'no' })] }));
    renderPage();

    expect(await screen.findByText('Pendiente de firma')).toBeInTheDocument();
    await waitFor(() => expect(fetchMock.mock.calls.some(([url]) => String(url).endsWith('/gallery/summary'))).toBe(true));
    expect(screen.queryByText('Tus fotos')).not.toBeInTheDocument();
  });

  it('asks for the code before showing any photo, and then shows the legend and the counter', async () => {
    answer(baseAnswers({ 'GET /gallery': [jsonResponse(200, galleryBody())] }));
    const user = renderPage();

    expect(await screen.findByText(/Tus fotos de prueba ya están listas/)).toBeInTheDocument();
    expect(fetchMock.mock.calls.some(([url]) => String(url).endsWith('/gallery'))).toBe(false);
    await openGallery(user);

    expect(await screen.findByText(/calidad baja y con marca de agua/)).toBeInTheDocument();
    expect(screen.getByText(/excelente calidad/)).toBeInTheDocument();
    expect(screen.getByText('Te quedan 2 de 2 fotos incluidas')).toBeInTheDocument();
    expect(screen.getByText(/Si eliges más, cada foto adicional cuesta/)).toBeInTheDocument();
    expect(screen.getAllByRole('listitem').filter((item) => item.classList.contains('gallery-tile'))).toHaveLength(4);
    const galleryCall = fetchMock.mock.calls.find(([url, init]) => String(url).endsWith('/gallery') && (init?.method ?? 'GET') === 'GET');
    expect(galleryCall?.[1]?.headers).toMatchObject({ Authorization: 'Bearer portal-jwt' });
  });

  it('lowers the counter with every photo, then says what an extra one costs, always with the price in view', async () => {
    answer(baseAnswers({ 'GET /gallery': [jsonResponse(200, galleryBody())] }));
    const user = renderPage();
    await openGallery(user);

    await user.click(await screen.findByRole('button', { name: 'Foto 1, sin elegir' }));
    expect(screen.getByText('Te quedan 1 de 2 fotos incluidas')).toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: 'Foto 2, sin elegir' }));
    expect(screen.getByText('Ya elegiste las 2 fotos de tu paquete')).toBeInTheDocument();
    expect(screen.getByText(/Las fotos adicionales cuestan .*3.*000.* cada una\. Si hay otra que te encante, ¡puedes sumarla!/)).toBeInTheDocument();
    expect(screen.queryByText(/foto adicional ×/)).not.toBeInTheDocument();

    await user.click(screen.getByRole('button', { name: 'Foto 3, sin elegir' }));
    expect(screen.getByText(/1 foto adicional × .*3.*000.* = .*3.*000.*Se suma a tu saldo/)).toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: 'Foto 4, sin elegir' }));
    expect(screen.getByText(/2 fotos adicionales × .*3.*000.* = .*6.*000/)).toBeInTheDocument();

    await user.click(screen.getByRole('button', { name: 'Foto 4, elegida' }));
    expect(screen.getByText(/1 foto adicional × /)).toBeInTheDocument();
  });

  it('asks to review before sending, says the photographer is told and the selection locks, and sends the photos in the order chosen', async () => {
    answer(
      baseAnswers({
        'GET /gallery': [jsonResponse(200, galleryBody())],
        'POST /gallery/selection': [
          jsonResponse(
            200,
            galleryBody({
              status: 'InEditing',
              selection: { photoIds: ['p3', 'p1'], submittedAt: '2026-10-30T12:00:00+00:00', extraPhotos: 0, extraTotal: { amount: 0, currency: 'CRC' } },
            }),
          ),
        ],
      }),
    );
    const user = renderPage();
    await openGallery(user);

    expect(await screen.findByRole('button', { name: 'Revisar y enviar' })).toBeDisabled();
    await user.click(screen.getByRole('button', { name: 'Foto 3, sin elegir' }));
    await user.click(screen.getByRole('button', { name: 'Foto 1, sin elegir' }));
    await user.click(screen.getByRole('button', { name: 'Revisar y enviar' }));

    expect(screen.getByText(/le avisamos a tu fotógrafo cuáles fotos va a editar/)).toBeInTheDocument();
    expect(screen.getByText(/no podrás cambiar tu selección/)).toBeInTheDocument();
    expect(fetchMock.mock.calls.some(([, init]) => init?.method === 'POST' && String(init.body).includes('photoIds'))).toBe(false);

    await user.click(screen.getByRole('button', { name: 'Sí, enviar mi selección' }));

    expect(await screen.findByRole('heading', { name: '¡Listo! Recibimos tu selección' })).toBeInTheDocument();
    expect(screen.getByText(/Tu fotógrafo ya sabe cuáles fotos va a editar/)).toBeInTheDocument();
    const call = fetchMock.mock.calls.find(([url]) => String(url).endsWith('/gallery/selection'));
    expect(JSON.parse(String(call?.[1]?.body))).toEqual({ photoIds: ['p3', 'p1'] });
    expect(call?.[1]?.headers).toMatchObject({ Authorization: 'Bearer portal-jwt' });
    expect(window.sessionStorage.length).toBe(0);
  });

  it('says what the extra photos cost in the review, and keeps the choice when the client goes back', async () => {
    answer(baseAnswers({ 'GET /gallery': [jsonResponse(200, galleryBody({ selectablePhotos: 1 }))] }));
    const user = renderPage();
    await openGallery(user);
    for (const number of [1, 2, 3]) {
      await user.click(await screen.findByRole('button', { name: `Foto ${number}, sin elegir` }));
    }

    await user.click(screen.getByRole('button', { name: 'Revisar y enviar' }));
    expect(screen.getByText(/2 fotos adicionales × .*3.*000.* = .*6.*000/)).toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: 'Seguir eligiendo' }));

    expect(screen.getByRole('button', { name: 'Foto 3, elegida' })).toBeInTheDocument();
  });

  it('shows why the selection was not sent, and keeps what the client chose', async () => {
    answer(
      baseAnswers({
        'GET /gallery': [jsonResponse(200, galleryBody())],
        'POST /gallery/selection': [jsonResponse(409, { code: 'gallery.invalid_state', detail: 'closed' })],
      }),
    );
    const user = renderPage();
    await openGallery(user);
    await user.click(await screen.findByRole('button', { name: 'Foto 1, sin elegir' }));
    await user.click(screen.getByRole('button', { name: 'Revisar y enviar' }));
    await user.click(screen.getByRole('button', { name: 'Sí, enviar mi selección' }));

    expect(await screen.findByRole('alert')).toHaveTextContent('Tu selección ya se había enviado');
    expect(screen.getByRole('button', { name: 'Seguir eligiendo' })).toBeInTheDocument();
  });

  it('remembers the choice when the gallery is closed and opened again, without asking for the code again', async () => {
    answer(baseAnswers({ 'GET /gallery': [jsonResponse(200, galleryBody())] }));
    const user = renderPage();
    await openGallery(user);
    await user.click(await screen.findByRole('button', { name: 'Foto 2, sin elegir' }));
    await user.click(screen.getByRole('button', { name: 'Cerrar' }));

    await user.click(await screen.findByRole('button', { name: 'Ver mis fotos y elegir' }));

    expect(await screen.findByRole('button', { name: 'Foto 2, elegida' })).toBeInTheDocument();
    expect(screen.getByText('Te quedan 1 de 2 fotos incluidas')).toBeInTheDocument();
    expect(fetchMock.mock.calls.filter(([url]) => String(url).endsWith('/code')).length).toBe(1);
  });

  it('goes back to the code, keeping the choice, when the server refuses the session', async () => {
    window.sessionStorage.setItem(`photosense.gallery.draft.${SAMPLE_TOKEN}`, JSON.stringify(['p2']));
    answer(
      baseAnswers({
        'GET /gallery': [jsonResponse(401, { code: 'auth.unauthorized', detail: 'no' }), jsonResponse(200, galleryBody())],
        'POST /code/verify': [sessionResponse(), sessionResponse()],
      }),
    );
    const user = renderPage();
    await openGallery(user);

    expect(await screen.findByText(/Tu verificación venció/)).toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: 'Enviar código' }));
    await user.type(await screen.findByLabelText('Código de 6 dígitos'), '123456');
    await user.click(screen.getByRole('button', { name: 'Verificar' }));

    expect(await screen.findByRole('button', { name: 'Foto 2, elegida' })).toBeInTheDocument();
  });

  it('opens a photo larger, lets the client pick it there, and closes with Escape', async () => {
    answer(baseAnswers({ 'GET /gallery': [jsonResponse(200, galleryBody())] }));
    const user = renderPage();
    await openGallery(user);

    await user.click(await screen.findByRole('button', { name: 'Ver la foto 2 más grande' }));
    const dialog = screen.getByRole('dialog', { name: 'Foto 2 de 4' });
    await user.click(within(dialog).getByRole('button', { name: 'Elegir esta foto' }));
    expect(within(dialog).getByRole('button', { name: 'Quitar de mi selección' })).toBeInTheDocument();
    await user.click(within(dialog).getByRole('button', { name: 'Siguiente' }));
    expect(screen.getByRole('dialog', { name: 'Foto 3 de 4' })).toBeInTheDocument();

    await user.keyboard('{Escape}');

    expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Foto 2, elegida' })).toBeInTheDocument();
  });

  it('shows only what was chosen once the selection was sent, with what the extras cost, and nothing to change', async () => {
    answer(
      baseAnswers({
        'GET /gallery/summary': [jsonResponse(200, { status: 'InEditing' })],
        'GET /gallery': [
          jsonResponse(
            200,
            galleryBody({
              status: 'InEditing',
              selection: { photoIds: ['p4', 'p2', 'p1'], submittedAt: '2026-10-30T12:00:00+00:00', extraPhotos: 1, extraTotal: { amount: 3000, currency: 'CRC' } },
            }),
          ),
        ],
      }),
    );
    const user = renderPage();

    expect(await screen.findByText(/Ya enviaste tu selección/)).toBeInTheDocument();
    await openGallery(user, 'Ver mi selección');

    expect(await screen.findByRole('heading', { name: 'Tu selección' })).toBeInTheDocument();
    expect(screen.getByText(/fotos elegidas./)).toHaveTextContent('3 fotos elegidas.');
    expect(screen.getByText(/1 foto adicional: .*3.*000.*Se suma a tu saldo/)).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Revisar y enviar' })).not.toBeInTheDocument();
    expect(screen.getAllByRole('button', { name: /^Foto \d, elegida$/ })).toHaveLength(3);
    expect(screen.getAllByRole('button', { name: /^Foto \d, elegida$/ }).every((tile) => (tile as HTMLButtonElement).disabled)).toBe(true);
  });

  it('does not offer the browser menu on a photo', async () => {
    answer(baseAnswers({ 'GET /gallery': [jsonResponse(200, galleryBody())] }));
    const user = renderPage();
    await openGallery(user);
    const tile = (await screen.findByRole('button', { name: 'Foto 1, sin elegir' })).closest('li') as HTMLElement;

    const notPrevented = fireEvent.contextMenu(tile);
    const image = within(tile).getByRole('presentation', { hidden: true });

    expect(notPrevented).toBe(false);
    expect(fireEvent.contextMenu(image)).toBe(false);
    expect(image).toHaveAttribute('draggable', 'false');
  });

  it('says a failed load in words and lets the client try again', async () => {
    answer(baseAnswers({ 'GET /gallery': [jsonResponse(500, { code: 'server.error', detail: 'x' }), jsonResponse(200, galleryBody())] }));
    const user = renderPage();
    await openGallery(user);

    expect(await screen.findByRole('alert')).toHaveTextContent('No pudimos cargar tus fotos');
    await user.click(screen.getByRole('button', { name: 'Reintentar' }));

    expect(await screen.findByText('Te quedan 2 de 2 fotos incluidas')).toBeInTheDocument();
  });
});
