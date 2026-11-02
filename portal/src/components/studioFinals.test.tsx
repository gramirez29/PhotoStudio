import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { galleryPayload, jsonResponse, studioUserPayload } from '../test/fixtures';
import { StudioPage } from './StudioPage';

const ID = '0197a000-0000-7000-8000-000000000001';
const GALLERY_PATH = `/api/web/galleries/${ID}`;
const SESSION = { user: studioUserPayload };
const SENT = '2026-11-03T12:00:00+00:00';

/**
 * Builds the gallery as the API sends it, with the selection of the client already sent and the photographer editing.
 * @param overrides What differs from that.
 * @returns The body.
 */
function gallery(overrides: Record<string, unknown> = {}): Record<string, unknown> {
  return {
    ...galleryPayload,
    status: 'InEditing',
    photoCount: 3,
    selectedPhotoCount: 3,
    extraPhotoCount: 1,
    selectionSubmittedAt: SENT,
    allowedActions: ['UploadFinals'],
    ...overrides,
  };
}

/**
 * Builds the selection as the API sends it: two photos of the package and one extra.
 * @param finals Sizes of the finals already uploaded, by photo.
 * @returns The body.
 */
function selection(finals: Record<string, number> = {}): Record<string, unknown> {
  return {
    submittedAt: SENT,
    extraPhotos: 1,
    extraTotal: { amount: 3000, currency: 'CRC' },
    photos: [
      { id: 'p1', name: 'IMG_0001.CR2', isExtra: false, finalBytes: finals.p1 ?? null, thumbUrl: 'https://account.r2.cloudflarestorage.com/t-p1?sig=secret', thumbIsFinal: finals.p1 !== undefined },
      { id: 'p2', name: 'IMG_0002.CR2', isExtra: false, finalBytes: finals.p2 ?? null, thumbUrl: 'https://account.r2.cloudflarestorage.com/t-p2?sig=secret', thumbIsFinal: finals.p2 !== undefined },
      { id: 'p3', name: 'IMG_0003.CR2', isExtra: true, finalBytes: finals.p3 ?? null, thumbUrl: 'https://account.r2.cloudflarestorage.com/t-p3?sig=secret', thumbIsFinal: finals.p3 !== undefined },
    ],
  };
}

vi.mock('../media/browserThumbRenderer', () => ({
  browserThumbRenderer: { render: () => Promise.resolve(new Blob([new Uint8Array(6)], { type: 'image/jpeg' })) },
}));

describe('the final photos on the page of a gallery', () => {
  const fetchMock = vi.fn<typeof fetch>();

  beforeEach(() => {
    vi.stubGlobal('fetch', fetchMock);
  });

  afterEach(() => {
    fetchMock.mockReset();
    vi.unstubAllGlobals();
  });

  /**
   * Renders the page of the gallery with a client that never retries.
   * @returns The user event driver.
   */
  function renderPage(): ReturnType<typeof userEvent.setup> {
    const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
    render(
      <QueryClientProvider client={client}>
        <StudioPage galleryId={ID} />
      </QueryClientProvider>,
    );
    return userEvent.setup();
  }

  /**
   * Answers the requests by method and path; each answer is used once, in order, and the last one stays for the next requests.
   * @param answers Answers by `METHOD path`.
   */
  function answer(answers: Record<string, Response[]>): void {
    fetchMock.mockImplementation((input, init) => {
      const url = String(input).replace(/^https?:\/\/[^/]+/, '');
      const key = `${init?.method ?? 'GET'} ${url}`;
      const queue = answers[key];
      const next = queue === undefined ? undefined : queue.length > 1 ? queue.shift() : queue[0]?.clone();
      return Promise.resolve(next ?? jsonResponse(500, { code: 'unexpected', detail: `No answer planned for ${key}` }));
    });
  }

  /**
   * Lists the requests the page sent, as `METHOD path`.
   * @returns The requests in order.
   */
  function requests(): string[] {
    return fetchMock.mock.calls.map(([input, init]) => `${init?.method ?? 'GET'} ${String(input).replace(/^https?:\/\/[^/]+/, '')}`);
  }

  /** The answers every scenario needs: the session, the proofs and the selection. */
  function base(extra: Record<string, Response[]> = {}): Record<string, Response[]> {
    return {
      'GET /api/web/auth/session': [jsonResponse(200, SESSION)],
      [`GET ${GALLERY_PATH}/proofs`]: [jsonResponse(200, { items: [] })],
      ...extra,
    };
  }

  it('says how many photos of the package have their final, and offers to choose the files', async () => {
    answer(base({ [`GET ${GALLERY_PATH}`]: [jsonResponse(200, gallery())], [`GET ${GALLERY_PATH}/selection`]: [jsonResponse(200, selection({ p1: 4_000_000 }))] }));
    renderPage();

    expect(await screen.findByRole('heading', { name: 'Fotos finales' })).toBeInTheDocument();
    expect(await screen.findByText('1 de 2 del paquete con su final')).toBeInTheDocument();
    expect(screen.getByLabelText('Elegir las fotos finales')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Publicar finales' })).not.toBeInTheDocument();
  });

  it('pairs the files with the photos the client chose, shows the pairs first, and uploads them straight to the storage', async () => {
    answer(
      base({
        [`GET ${GALLERY_PATH}`]: [jsonResponse(200, gallery())],
        [`GET ${GALLERY_PATH}/selection`]: [jsonResponse(200, selection()), jsonResponse(200, selection({ p1: 5, p2: 5 }))],
        [`POST ${GALLERY_PATH}/finals/uploads`]: [
          jsonResponse(200, {
            slots: [
              { photoId: 'p1', url: 'https://account.r2.cloudflarestorage.com/f1?sig=secret', headers: { 'Content-Type': 'image/jpeg' }, expiresAt: '2026-11-03T12:15:00+00:00', thumbUrl: 'https://account.r2.cloudflarestorage.com/th1?sig=secret', thumbHeaders: { 'Content-Type': 'image/jpeg' } },
              { photoId: 'p2', url: 'https://account.r2.cloudflarestorage.com/f2?sig=secret', headers: { 'Content-Type': 'image/jpeg' }, expiresAt: '2026-11-03T12:15:00+00:00', thumbUrl: 'https://account.r2.cloudflarestorage.com/th2?sig=secret', thumbHeaders: { 'Content-Type': 'image/jpeg' } },
            ],
          }),
        ],
        'PUT /f1?sig=secret': [new Response(null, { status: 200 })],
        'PUT /f2?sig=secret': [new Response(null, { status: 200 })],
        'PUT /th1?sig=secret': [new Response(null, { status: 200 })],
        'PUT /th2?sig=secret': [new Response(null, { status: 200 })],
        [`POST ${GALLERY_PATH}/finals`]: [jsonResponse(200, { gallery: gallery({ allowedActions: ['UploadFinals', 'PublishFinals'] }), rejected: [] })],
      }),
    );
    const user = renderPage();

    await user.upload(await screen.findByLabelText('Elegir las fotos finales'), [
      new File([new Uint8Array(5)], 'IMG_0001-Edit.jpg', { type: 'image/jpeg' }),
      new File([new Uint8Array(5)], 'img_0002.jpg', { type: 'image/jpeg' }),
      new File([new Uint8Array(5)], 'vacaciones.jpg', { type: 'image/jpeg' }),
    ]);

    expect(await screen.findByText(/Se subirán 2 finales/)).toBeInTheDocument();
    expect(screen.getByText('→ IMG_0001.CR2')).toBeInTheDocument();
    expect(screen.getByText(/No se tomaron: 1 no coincide con ninguna foto que eligió el cliente/)).toBeInTheDocument();
    expect(requests().some((request) => request.includes('/finals'))).toBe(false);

    await user.click(screen.getByRole('button', { name: 'Subir 2 finales' }));

    await waitFor(() => expect(requests()).toEqual(expect.arrayContaining([`POST ${GALLERY_PATH}/finals/uploads`, 'PUT /f1?sig=secret', 'PUT /f2?sig=secret', 'PUT /th1?sig=secret', 'PUT /th2?sig=secret', `POST ${GALLERY_PATH}/finals`])));
    const registered = fetchMock.mock.calls.find(([input, init]) => String(input).endsWith('/finals') && init?.method === 'POST');
    expect(JSON.parse(String(registered?.[1]?.body))).toEqual({
      photoIds: ['p1', 'p2'],
      details: [
        { photoId: 'p1', width: null, height: null, capturedAt: null },
        { photoId: 'p2', width: null, height: null, capturedAt: null },
      ],
    });
    expect(await screen.findByRole('button', { name: 'Publicar finales' })).toBeInTheDocument();
  });

  it('opens a photo in its real quality, the final itself and not the small image, and keeps the proof for one without a final', async () => {
    answer(
      base({
        [`GET ${GALLERY_PATH}`]: [jsonResponse(200, gallery())],
        [`GET ${GALLERY_PATH}/selection`]: [jsonResponse(200, selection({ p1: 4_000_000 }))],
        [`GET ${GALLERY_PATH}/finals/p1/download`]: [
          jsonResponse(200, { fileName: 'IMG_0001.jpg', url: 'https://account.r2.cloudflarestorage.com/final-p1?sig=secret', expiresAt: '2026-11-03T12:05:00+00:00' }),
        ],
      }),
    );
    const user = renderPage();

    await user.click(await screen.findByRole('button', { name: 'Ver IMG_0001.CR2' }));

    const dialog = await screen.findByRole('dialog');
    await waitFor(() => expect(within(dialog).getByRole('img')).toHaveAttribute('src', 'https://account.r2.cloudflarestorage.com/final-p1?sig=secret'));
    expect(within(dialog).getByText(/final subido/)).toBeInTheDocument();
    await user.click(within(dialog).getByRole('button', { name: 'Siguiente' }));
    expect(within(dialog).getByRole('img')).toHaveAttribute('src', 'https://account.r2.cloudflarestorage.com/t-p2?sig=secret');
    expect(within(dialog).getByText(/prueba, todavía sin final/)).toBeInTheDocument();
    expect(requests().some((request) => request.includes('/finals/p2/download'))).toBe(false);
    await user.click(within(dialog).getByRole('button', { name: 'Cerrar' }));
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
  });

  it('keeps a final that was refused in the queue with its reason', async () => {
    answer(
      base({
        [`GET ${GALLERY_PATH}`]: [jsonResponse(200, gallery())],
        [`GET ${GALLERY_PATH}/selection`]: [jsonResponse(200, selection())],
        [`POST ${GALLERY_PATH}/finals/uploads`]: [
          jsonResponse(200, { slots: [{ photoId: 'p1', url: 'https://account.r2.cloudflarestorage.com/f1?sig=secret', headers: {}, expiresAt: '2026-11-03T12:15:00+00:00' }] }),
        ],
        'PUT /f1?sig=secret': [new Response(null, { status: 200 })],
        [`POST ${GALLERY_PATH}/finals`]: [jsonResponse(200, { gallery: gallery(), rejected: [{ photoId: 'p1', reason: 'too_big' }] })],
      }),
    );
    const user = renderPage();

    await user.upload(await screen.findByLabelText('Elegir las fotos finales'), [new File([new Uint8Array(5)], 'IMG_0001.jpg', { type: 'image/jpeg' })]);
    await user.click(await screen.findByRole('button', { name: 'Subir 1 final' }));

    expect(await screen.findByText('La foto pesa más de lo permitido.')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Reintentar' })).toBeInTheDocument();
  });

  it('asks to confirm before publishing the package, and says what happens after', async () => {
    answer(
      base({
        [`GET ${GALLERY_PATH}`]: [jsonResponse(200, gallery({ allowedActions: ['UploadFinals', 'PublishFinals'] }))],
        [`GET ${GALLERY_PATH}/selection`]: [jsonResponse(200, selection({ p1: 5, p2: 5 }))],
        [`POST ${GALLERY_PATH}/finals/publish`]: [jsonResponse(200, gallery({ status: 'Ready', allowedActions: ['UploadFinals'], finalsPublishedAt: '2026-11-03T13:00:00+00:00' }))],
      }),
    );
    const user = renderPage();

    await user.click(await screen.findByRole('button', { name: 'Publicar finales' }));
    expect(screen.getByText(/cuando se verifique su pago|descargarán/i)).toBeInTheDocument();
    expect(requests().some((request) => request.endsWith('/finals/publish'))).toBe(false);
    await user.click(screen.getByRole('button', { name: 'Sí, publicar los finales' }));

    expect(await screen.findByText(/Publicadas el .*El cliente las descargará en cuanto su saldo esté en cero/)).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Publicar finales' })).not.toBeInTheDocument();
  });

  it('says why publishing was refused when a photo of the package has no final', async () => {
    answer(
      base({
        [`GET ${GALLERY_PATH}`]: [jsonResponse(200, gallery({ allowedActions: ['UploadFinals', 'PublishFinals'] }))],
        [`GET ${GALLERY_PATH}/selection`]: [jsonResponse(200, selection({ p1: 5, p2: 5 }))],
        [`POST ${GALLERY_PATH}/finals/publish`]: [jsonResponse(409, { code: 'gallery.finals_incomplete', detail: 'x' })],
      }),
    );
    const user = renderPage();

    await user.click(await screen.findByRole('button', { name: 'Publicar finales' }));
    await user.click(screen.getByRole('button', { name: 'Sí, publicar los finales' }));

    expect(await screen.findByRole('alert')).toHaveTextContent('Faltan finales');
  });

  it('releases the extras by hand, after asking whether the client already paid, and says so afterwards', async () => {
    answer(
      base({
        [`GET ${GALLERY_PATH}`]: [jsonResponse(200, gallery({ status: 'Delivered', allowedActions: ['UploadFinals', 'ReleaseExtras'], deliveredAt: '2026-11-03T13:00:00+00:00', finalsPublishedAt: '2026-11-03T13:00:00+00:00' }))],
        [`GET ${GALLERY_PATH}/selection`]: [jsonResponse(200, selection({ p1: 5, p2: 5, p3: 5 }))],
        [`POST ${GALLERY_PATH}/extras/release`]: [
          jsonResponse(200, gallery({ status: 'Delivered', allowedActions: ['UploadFinals'], deliveredAt: '2026-11-03T13:00:00+00:00', extrasReleasedAt: '2026-11-03T14:00:00+00:00' })),
        ],
      }),
    );
    const user = renderPage();

    expect(await screen.findByText(/Entregada el .*el cliente ya puede descargar sus fotos/)).toBeInTheDocument();
    await user.click(await screen.findByRole('button', { name: 'Entregar las adicionales' }));
    await user.click(screen.getByRole('button', { name: 'Sí, el cliente ya pagó' }));

    expect(await screen.findByText('Las fotos adicionales ya están disponibles para el cliente.')).toBeInTheDocument();
    expect(within(screen.getByRole('group', { name: 'Fotos adicionales (1)' })).getByText('Descargas hechas: 0')).toBeInTheDocument();
  });

  it('reopens the selection only after asking for the reason and confirming, and the page goes back to the client choosing', async () => {
    answer(
      base({
        [`GET ${GALLERY_PATH}`]: [jsonResponse(200, gallery({ allowedActions: ['UploadFinals', 'ReopenSelection'] }))],
        [`GET ${GALLERY_PATH}/selection`]: [jsonResponse(200, selection())],
        [`POST ${GALLERY_PATH}/selection/reopen`]: [
          jsonResponse(200, gallery({ status: 'InSelection', selectionSubmittedAt: null, selectedPhotoCount: 0, allowedActions: ['EditTerms'], selectionReopenReason: 'cambio', selectionReopenedAt: '2026-11-04T12:00:00+00:00' })),
        ],
      }),
    );
    const user = renderPage();

    await user.click(await screen.findByRole('button', { name: 'Reabrir la selección' }));
    expect(screen.getByRole('button', { name: 'Sí, reabrir la selección' })).toBeDisabled();
    await user.type(screen.getByLabelText('Motivo (solo lo ves tú)'), '  El cliente quiere cambiar dos fotos ');
    expect(requests().some((request) => request.endsWith('/selection/reopen'))).toBe(false);
    await user.click(screen.getByRole('button', { name: 'Sí, reabrir la selección' }));

    await waitFor(() => expect(screen.queryByRole('heading', { name: 'Fotos finales' })).not.toBeInTheDocument());
    const sent = fetchMock.mock.calls.find(([input]) => String(input).endsWith('/selection/reopen'));
    expect(JSON.parse(String(sent?.[1]?.body))).toEqual({ reason: 'El cliente quiere cambiar dos fotos' });
  });

  it('says why reopening was refused', async () => {
    answer(
      base({
        [`GET ${GALLERY_PATH}`]: [jsonResponse(200, gallery({ allowedActions: ['UploadFinals', 'ReopenSelection'] }))],
        [`GET ${GALLERY_PATH}/selection`]: [jsonResponse(200, selection())],
        [`POST ${GALLERY_PATH}/selection/reopen`]: [jsonResponse(409, { code: 'gallery.invalid_state', detail: 'x' })],
      }),
    );
    const user = renderPage();

    await user.click(await screen.findByRole('button', { name: 'Reabrir la selección' }));
    await user.type(screen.getByLabelText('Motivo (solo lo ves tú)'), 'cambio');
    await user.click(screen.getByRole('button', { name: 'Sí, reabrir la selección' }));

    expect(await screen.findByRole('alert')).toHaveTextContent('ya no está en un estado que permita esta acción');
  });

  describe('the ZIP', () => {
    const DELIVERED = { status: 'Delivered', deliveredAt: '2026-11-03T13:00:00+00:00', finalsPublishedAt: '2026-11-03T13:00:00+00:00' };

    it('says there is none yet and offers to choose one, once the finals are published', async () => {
      answer(
        base({
          [`GET ${GALLERY_PATH}`]: [jsonResponse(200, gallery({ ...DELIVERED, allowedActions: ['UploadFinals', 'UploadZip'] }))],
          [`GET ${GALLERY_PATH}/selection`]: [jsonResponse(200, selection({ p1: 5, p2: 5 }))],
        }),
      );
      renderPage();

      expect(await screen.findByRole('heading', { name: 'ZIP con las fotos (opcional)' })).toBeInTheDocument();
      expect(screen.getByText('Todavía no hay un ZIP.')).toBeInTheDocument();
      expect(screen.getByLabelText('Elegir el ZIP')).toBeInTheDocument();
      expect(screen.queryByRole('button', { name: 'Borrar el ZIP' })).not.toBeInTheDocument();
    });

    it('uploads the ZIP straight to the storage and shows its size and that the client has not downloaded it', async () => {
      const withZip = { bytes: 220_000_000, uploadedAt: '2026-11-03T14:00:00+00:00', downloads: 0, lastDownloadedAt: null };
      answer(
        base({
          [`GET ${GALLERY_PATH}`]: [jsonResponse(200, gallery({ ...DELIVERED, allowedActions: ['UploadFinals', 'UploadZip'] }))],
          [`GET ${GALLERY_PATH}/selection`]: [jsonResponse(200, selection({ p1: 5, p2: 5 }))],
          [`POST ${GALLERY_PATH}/zip/upload`]: [jsonResponse(200, { url: 'https://account.r2.cloudflarestorage.com/zip?sig=secret', headers: { 'Content-Type': 'application/zip' }, expiresAt: '2026-11-03T14:15:00+00:00' })],
          'PUT /zip?sig=secret': [new Response(null, { status: 200 })],
          [`POST ${GALLERY_PATH}/zip`]: [jsonResponse(200, gallery({ ...DELIVERED, allowedActions: ['UploadFinals', 'UploadZip', 'RemoveZip'], zip: withZip }))],
        }),
      );
      const user = renderPage();

      await user.upload(await screen.findByLabelText('Elegir el ZIP'), new File([new Uint8Array(8)], 'Fotos.zip', { type: 'application/zip' }));

      expect(await screen.findByText('El cliente todavía no lo ha descargado.')).toBeInTheDocument();
      expect(screen.getByText(/209,8 MB/)).toBeInTheDocument();
      expect(requests()).toEqual(expect.arrayContaining([`POST ${GALLERY_PATH}/zip/upload`, 'PUT /zip?sig=secret', `POST ${GALLERY_PATH}/zip`]));
      const asked = fetchMock.mock.calls.find(([input]) => String(input).endsWith('/zip/upload'));
      expect(JSON.parse(String(asked?.[1]?.body))).toEqual({ bytes: 8 });
      expect(screen.getByRole('button', { name: 'Borrar el ZIP' })).toBeInTheDocument();
    });

    it('refuses a file that is not a ZIP or weighs more than 500 MB, without asking the API', async () => {
      answer(
        base({
          [`GET ${GALLERY_PATH}`]: [jsonResponse(200, gallery({ ...DELIVERED, allowedActions: ['UploadFinals', 'UploadZip'] }))],
          [`GET ${GALLERY_PATH}/selection`]: [jsonResponse(200, selection({ p1: 5, p2: 5 }))],
        }),
      );
      renderPage();
      const input = await screen.findByLabelText('Elegir el ZIP');

      fireEvent.change(input, { target: { files: [new File([new Uint8Array(8)], 'fotos.rar', { type: 'application/x-rar' })] } });
      expect(await screen.findByText('El archivo no es un ZIP.')).toBeInTheDocument();

      const big = new File([new Uint8Array(8)], 'big.zip', { type: 'application/zip' });
      Object.defineProperty(big, 'size', { value: 524_288_001 });
      fireEvent.change(input, { target: { files: [big] } });
      expect(await screen.findByText(/pesa más de 500 MB/)).toBeInTheDocument();
      expect(requests().some((request) => request.includes('/zip'))).toBe(false);
    });

    it('shows how many times and when the client downloaded the ZIP, and deletes it after confirming', async () => {
      const withZip = { bytes: 220_000_000, uploadedAt: '2026-11-03T14:00:00+00:00', downloads: 2, lastDownloadedAt: '2026-11-04T15:00:00+00:00' };
      answer(
        base({
          [`GET ${GALLERY_PATH}`]: [jsonResponse(200, gallery({ ...DELIVERED, allowedActions: ['UploadFinals', 'UploadZip', 'RemoveZip'], zip: withZip }))],
          [`GET ${GALLERY_PATH}/selection`]: [jsonResponse(200, selection({ p1: 5, p2: 5 }))],
          [`POST ${GALLERY_PATH}/zip/remove`]: [jsonResponse(200, gallery({ ...DELIVERED, allowedActions: ['UploadFinals', 'UploadZip'], zip: null }))],
        }),
      );
      const user = renderPage();

      expect(await screen.findByText(/El cliente lo ha descargado 2 veces \(la última, el /)).toBeInTheDocument();
      await user.click(screen.getByRole('button', { name: 'Borrar el ZIP' }));
      expect(requests().some((request) => request.endsWith('/zip/remove'))).toBe(false);
      await user.click(screen.getByRole('button', { name: 'Sí, borrar el ZIP' }));

      expect(await screen.findByText('Todavía no hay un ZIP.')).toBeInTheDocument();
    });
  });

  it('downloads the final of a photo for the photographer, with a short-lived address that is not counted', async () => {
    const started: string[] = [];
    const click = vi.spyOn(HTMLAnchorElement.prototype, 'click').mockImplementation(function (this: HTMLAnchorElement) {
      started.push(`${this.download}|${this.getAttribute('href')}`);
    });
    answer(
      base({
        [`GET ${GALLERY_PATH}`]: [jsonResponse(200, gallery())],
        [`GET ${GALLERY_PATH}/selection`]: [jsonResponse(200, selection({ p1: 4_000_000 }))],
        [`GET ${GALLERY_PATH}/finals/p1/download`]: [jsonResponse(200, { fileName: 'IMG_0001.jpg', url: 'https://account.r2.cloudflarestorage.com/f1?sig=secret', expiresAt: '2026-11-03T12:05:00+00:00' })],
      }),
    );
    const user = renderPage();

    await user.click(await screen.findByRole('button', { name: 'Descargar el final de IMG_0001.CR2' }));

    await waitFor(() => expect(started).toEqual(['IMG_0001.jpg|https://account.r2.cloudflarestorage.com/f1?sig=secret']));
    expect(screen.queryByRole('button', { name: 'Descargar el final de IMG_0002.CR2' })).not.toBeInTheDocument();
    click.mockRestore();
  });

  it('shows nothing to do with finals before the client sends the selection', async () => {
    answer(base({ [`GET ${GALLERY_PATH}`]: [jsonResponse(200, gallery({ status: 'InSelection', selectionSubmittedAt: null, allowedActions: ['EditTerms'] }))] }));
    renderPage();

    expect(await screen.findByRole('heading', { name: 'María Pérez' })).toBeInTheDocument();
    expect(screen.queryByRole('heading', { name: 'Fotos finales' })).not.toBeInTheDocument();
  });
});
