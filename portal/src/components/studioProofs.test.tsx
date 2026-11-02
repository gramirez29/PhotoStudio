import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { galleryPayload, jsonResponse, studioUserPayload } from '../test/fixtures';
import { StudioPage } from './StudioPage';

vi.mock('../media/browserProofRenderer', () => ({
  browserProofRenderer: { render: () => Promise.resolve({ blob: new Blob([new Uint8Array(50)], { type: 'image/jpeg' }), size: { width: 720, height: 480 }, quality: 0.4 }) },
}));

const ID = '0197a000-0000-7000-8000-000000000001';
const GALLERY_PATH = `/api/web/galleries/${ID}`;
const ACTIONS = ['EditTerms', 'UploadProofs', 'RemoveProof', 'OpenSelection'];
const SESSION = { user: studioUserPayload };
const PROOF = { id: 'p-1', name: 'IMG_0001.jpg', bytes: 1234, url: 'https://account.r2.cloudflarestorage.com/p-1?sig=secret', expiresAt: '2026-10-30T12:10:00+00:00' };

describe('the page of a gallery and its proofs', () => {
  const fetchMock = vi.fn<typeof fetch>();

  beforeEach(() => {
    vi.stubGlobal('fetch', fetchMock);
    vi.stubGlobal('URL', Object.assign(URL, { createObjectURL: () => 'blob:sample', revokeObjectURL: () => undefined }));
  });

  afterEach(() => {
    fetchMock.mockReset();
    vi.unstubAllGlobals();
    window.sessionStorage.clear();
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

  /**
   * Builds the gallery as the API sends it.
   * @param overrides What differs from a gallery waiting for its proofs.
   * @returns The body.
   */
  function gallery(overrides: Record<string, unknown> = {}): Record<string, unknown> {
    return { ...galleryPayload, allowedActions: ACTIONS, photoCount: 1, ...overrides };
  }

  it('shows the gallery and its proofs, with the count and a way to remove each one', async () => {
    answer({
      'GET /api/web/auth/session': [jsonResponse(200, SESSION)],
      [`GET ${GALLERY_PATH}`]: [jsonResponse(200, gallery())],
      [`GET ${GALLERY_PATH}/proofs`]: [jsonResponse(200, { items: [PROOF] })],
    });
    renderPage();

    expect(await screen.findByRole('heading', { name: 'María Pérez' })).toBeInTheDocument();
    expect(screen.getByText('1 de 150 pruebas')).toBeInTheDocument();
    const image = await screen.findByAltText('IMG_0001.jpg');
    expect(image).toHaveAttribute('src', PROOF.url);
    expect(screen.getByRole('button', { name: 'Quitar IMG_0001.jpg' })).toBeInTheDocument();
    expect(screen.getByRole('link', { name: '← Tus galerías' })).toHaveAttribute('href', '/studio');
  });

  it('removes a proof and loads the list again', async () => {
    answer({
      'GET /api/web/auth/session': [jsonResponse(200, SESSION)],
      [`GET ${GALLERY_PATH}`]: [jsonResponse(200, gallery())],
      [`GET ${GALLERY_PATH}/proofs`]: [jsonResponse(200, { items: [PROOF] }), jsonResponse(200, { items: [] })],
      [`POST ${GALLERY_PATH}/proofs/p-1/remove`]: [jsonResponse(200, gallery({ photoCount: 0 }))],
    });
    const user = renderPage();

    await user.click(await screen.findByRole('button', { name: 'Quitar IMG_0001.jpg' }));

    expect(await screen.findByText('Todavía no hay pruebas.')).toBeInTheDocument();
    expect(screen.getByText('0 de 150 pruebas')).toBeInTheDocument();
  });

  it('shows a sample, then prepares, uploads straight to the storage and registers the photos', async () => {
    answer({
      'GET /api/web/auth/session': [jsonResponse(200, SESSION)],
      [`GET ${GALLERY_PATH}`]: [jsonResponse(200, gallery({ photoCount: 0 }))],
      [`GET ${GALLERY_PATH}/proofs`]: [jsonResponse(200, { items: [] }), jsonResponse(200, { items: [PROOF] })],
      [`POST ${GALLERY_PATH}/proofs/uploads`]: [
        jsonResponse(200, { slots: [{ photoId: 'p-1', url: 'https://account.r2.cloudflarestorage.com/p-1?sig=secret', headers: { 'Content-Type': 'image/jpeg' }, expiresAt: '2026-10-30T12:15:00+00:00' }] }),
      ],
      'PUT /p-1?sig=secret': [new Response(null, { status: 200 })],
      [`POST ${GALLERY_PATH}/proofs`]: [jsonResponse(200, { gallery: gallery({ photoCount: 1 }), rejected: [] })],
    });
    const user = renderPage();

    await user.upload(await screen.findByLabelText('Elegir fotos'), [new File([new Uint8Array(8)], 'IMG_0001.jpg', { type: 'image/jpeg' })]);

    expect(await screen.findByRole('heading', { name: 'Así verá tu cliente las pruebas' })).toBeInTheDocument();
    expect(await screen.findByAltText(/Muestra de una prueba/)).toBeInTheDocument();
    expect(requests().some((request) => request.startsWith('PUT'))).toBe(false);

    await user.click(screen.getByRole('button', { name: 'Preparar y subir 1 foto' }));

    expect(await screen.findByAltText('IMG_0001.jpg')).toBeInTheDocument();
    expect(screen.getByText('1 de 150 pruebas')).toBeInTheDocument();
    expect(requests()).toEqual(
      expect.arrayContaining([`POST ${GALLERY_PATH}/proofs/uploads`, 'PUT /p-1?sig=secret', `POST ${GALLERY_PATH}/proofs`]),
    );
    const put = fetchMock.mock.calls.find(([, init]) => init?.method === 'PUT');
    expect(put?.[1]).toMatchObject({ credentials: 'omit', headers: { 'Content-Type': 'image/jpeg' } });
    expect(screen.queryByRole('region', { name: 'Fotos por subir' })).not.toBeInTheDocument();
  });

  it('keeps a photo the storage refused in the list with its reason, to try again', async () => {
    answer({
      'GET /api/web/auth/session': [jsonResponse(200, SESSION)],
      [`GET ${GALLERY_PATH}`]: [jsonResponse(200, gallery({ photoCount: 0 }))],
      [`GET ${GALLERY_PATH}/proofs`]: [jsonResponse(200, { items: [] })],
      [`POST ${GALLERY_PATH}/proofs/uploads`]: [
        jsonResponse(200, { slots: [{ photoId: 'p-1', url: 'https://account.r2.cloudflarestorage.com/p-1?sig=secret', headers: { 'Content-Type': 'image/jpeg' }, expiresAt: '2026-10-30T12:15:00+00:00' }] }),
      ],
      'PUT /p-1?sig=secret': [new Response('denied', { status: 403 })],
    });
    const user = renderPage();

    await user.upload(await screen.findByLabelText('Elegir fotos'), [new File([new Uint8Array(8)], 'IMG_0002.jpg', { type: 'image/jpeg' })]);
    await user.click(await screen.findByRole('button', { name: 'Preparar y subir 1 foto' }));

    const queue = await screen.findByRole('region', { name: 'Fotos por subir' });
    expect(within(queue).getByText('IMG_0002.jpg')).toBeInTheDocument();
    expect(within(queue).getByText('El permiso para subir esta foto venció. Vuelve a intentarlo.')).toBeInTheDocument();
    expect(within(queue).getByRole('button', { name: 'Reintentar' })).toBeInTheDocument();

    await user.click(within(queue).getByRole('button', { name: 'Quitar las que fallaron' }));
    await waitFor(() => expect(screen.queryByRole('region', { name: 'Fotos por subir' })).not.toBeInTheDocument());
  });

  it('goes back to the picker, sending nothing, when the person cancels at the sample', async () => {
    answer({
      'GET /api/web/auth/session': [jsonResponse(200, SESSION)],
      [`GET ${GALLERY_PATH}`]: [jsonResponse(200, gallery({ photoCount: 0 }))],
      [`GET ${GALLERY_PATH}/proofs`]: [jsonResponse(200, { items: [] })],
    });
    const user = renderPage();
    const input = await screen.findByLabelText('Elegir fotos');

    await user.upload(input, [new File([new Uint8Array(8)], 'a.jpg', { type: 'image/jpeg' })]);
    await user.click(await screen.findByRole('button', { name: 'Cancelar' }));

    expect(screen.getByLabelText('Elegir fotos')).toBeInTheDocument();
    expect(requests().some((request) => request.startsWith('POST'))).toBe(false);
  });

  it('publishes the proofs after asking to confirm, and then shows them as published', async () => {
    answer({
      'GET /api/web/auth/session': [jsonResponse(200, SESSION)],
      [`GET ${GALLERY_PATH}`]: [jsonResponse(200, gallery())],
      [`GET ${GALLERY_PATH}/proofs`]: [jsonResponse(200, { items: [PROOF] })],
      [`POST ${GALLERY_PATH}/selection/open`]: [jsonResponse(200, gallery({ status: 'InSelection', allowedActions: ['EditTerms'] }))],
    });
    const user = renderPage();

    await user.click(await screen.findByRole('button', { name: 'Publicar pruebas' }));
    expect(requests().some((request) => request.includes('selection/open'))).toBe(false);
    await user.click(screen.getByRole('button', { name: 'Sí, publicar 1 prueba' }));

    expect(await screen.findByText(/Las pruebas ya están publicadas/)).toBeInTheDocument();
    expect(screen.queryByLabelText('Elegir fotos')).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /Quitar/ })).not.toBeInTheDocument();
    expect(screen.getByText('El cliente está eligiendo')).toBeInTheDocument();
  });

  it('shows what the client chose, with the names of the files, and copies them one per line', async () => {
    answer({
      'GET /api/web/auth/session': [jsonResponse(200, SESSION)],
      [`GET ${GALLERY_PATH}`]: [
        jsonResponse(200, gallery({ status: 'InEditing', allowedActions: [], selectedPhotoCount: 2, extraPhotoCount: 1, selectionSubmittedAt: '2026-10-30T12:00:00+00:00' })),
      ],
      [`GET ${GALLERY_PATH}/proofs`]: [jsonResponse(200, { items: [PROOF] })],
      [`GET ${GALLERY_PATH}/selection`]: [
        jsonResponse(200, {
          submittedAt: '2026-10-30T12:00:00+00:00',
          extraPhotos: 1,
          extraTotal: { amount: 3000, currency: 'CRC' },
          photos: [
            { id: 'p-3', name: 'IMG_0003.jpg' },
            { id: 'p-1', name: 'IMG_0001.jpg' },
          ],
        }),
      ],
    });
    const user = renderPage();
    const writeText = vi.spyOn(navigator.clipboard, 'writeText');

    expect(await screen.findByRole('heading', { name: 'Selección del cliente' })).toBeInTheDocument();
    expect(screen.getByText('2 fotos elegidas')).toBeInTheDocument();
    expect(screen.getByText(/1 es adicional: .*3.*000/)).toBeInTheDocument();
    const names = within(screen.getByRole('heading', { name: 'Selección del cliente' }).closest('section') as HTMLElement).getAllByRole('listitem');
    expect(names.map((item) => item.textContent)).toEqual(['IMG_0003.jpg', 'IMG_0001.jpg']);
    expect(screen.queryByText(/Las pruebas ya están publicadas/)).not.toBeInTheDocument();
    expect(screen.queryByLabelText('Elegir fotos')).not.toBeInTheDocument();

    await user.click(screen.getByRole('button', { name: 'Copiar los nombres de los archivos' }));

    expect(writeText).toHaveBeenCalledWith(['IMG_0003.jpg', 'IMG_0001.jpg'].join('\n'));
    expect(await screen.findByText(/Pégalos en la búsqueda/)).toBeInTheDocument();
  });

  it('says so when the browser does not let the names be copied', async () => {
    answer({
      'GET /api/web/auth/session': [jsonResponse(200, SESSION)],
      [`GET ${GALLERY_PATH}`]: [jsonResponse(200, gallery({ status: 'InEditing', allowedActions: [], selectedPhotoCount: 1, selectionSubmittedAt: '2026-10-30T12:00:00+00:00' }))],
      [`GET ${GALLERY_PATH}/proofs`]: [jsonResponse(200, { items: [] })],
      [`GET ${GALLERY_PATH}/selection`]: [
        jsonResponse(200, { submittedAt: '2026-10-30T12:00:00+00:00', extraPhotos: 0, extraTotal: { amount: 0, currency: 'CRC' }, photos: [{ id: 'p-1', name: 'IMG_0001.jpg' }] }),
      ],
    });
    const user = renderPage();
    vi.spyOn(navigator.clipboard, 'writeText').mockRejectedValue(new Error('denied'));

    await user.click(await screen.findByRole('button', { name: 'Copiar los nombres de los archivos' }));

    expect(await screen.findByRole('alert')).toHaveTextContent('Tu navegador no permitió copiar');
  });

  it('does not offer to publish without proofs', async () => {
    answer({
      'GET /api/web/auth/session': [jsonResponse(200, SESSION)],
      [`GET ${GALLERY_PATH}`]: [jsonResponse(200, gallery({ photoCount: 0 }))],
      [`GET ${GALLERY_PATH}/proofs`]: [jsonResponse(200, { items: [] })],
    });
    renderPage();

    expect(await screen.findByRole('button', { name: 'Publicar pruebas' })).toBeDisabled();
  });

  it('says so when the gallery is not found', async () => {
    answer({
      'GET /api/web/auth/session': [jsonResponse(200, SESSION)],
      [`GET ${GALLERY_PATH}`]: [jsonResponse(404, { code: 'resource.not_found', detail: 'no' })],
    });
    renderPage();

    expect(await screen.findByText('No pudimos abrir esta galería')).toBeInTheDocument();
    expect(screen.getByText('No encontramos esta galería.')).toBeInTheDocument();
    expect(screen.getByRole('link', { name: '← Tus galerías' })).toBeInTheDocument();
  });

  it('asks to sign in when there is no session, whatever gallery the address names', async () => {
    answer({ 'GET /api/web/auth/session': [jsonResponse(401, { code: 'auth.unauthenticated', detail: 'no' })] });
    renderPage();

    expect(await screen.findByLabelText('Usuario')).toBeInTheDocument();
    expect(requests()).toEqual(['GET /api/web/auth/session']);
  });
});
