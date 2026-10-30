import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { act, render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { galleryPayload, galleryPayloadNumber, jsonResponse, studioUserPayload } from '../test/fixtures';
import { StudioPage } from './StudioPage';

const SESSION = 'GET /api/web/auth/session';
const LOGIN = 'POST /api/web/auth/login';
const VERIFY = 'POST /api/web/auth/code/verify';
const RESEND = 'POST /api/web/auth/code/resend';
const LOGOUT = 'POST /api/web/auth/logout';
const GALLERIES = 'GET /api/web/galleries?limit=5';
const CODE_REQUIRED = { status: 'CodeRequired', maskedEmail: 'a***@example.com', expiresAt: '2026-10-30T12:10:00+00:00' };

describe('the web area of the photographer', () => {
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
   * Renders the page with a client that never retries.
   * @returns The user event driver.
   */
  function renderPage(): ReturnType<typeof userEvent.setup> {
    const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
    render(
      <QueryClientProvider client={client}>
        <StudioPage />
      </QueryClientProvider>,
    );
    return userEvent.setup();
  }

  /**
   * Renders the page and gives back the user event driver it was handed, for tests that run on fake timers.
   * @param user The user event driver.
   * @returns The same driver.
   */
  function renderPageWith(user: ReturnType<typeof userEvent.setup>): ReturnType<typeof userEvent.setup> {
    renderPage();
    return user;
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
   * Types the username and the password and sends them.
   * @param user The user event driver.
   */
  async function typePassword(user: ReturnType<typeof userEvent.setup>): Promise<void> {
    await user.type(await screen.findByLabelText('Usuario'), 'ana');
    await user.type(screen.getByLabelText('Contraseña'), 'correct horse');
    await user.click(screen.getByRole('button', { name: 'Entrar' }));
  }

  it('shows the password when nobody is signed in, with a field that hides what is typed', async () => {
    answer({ [SESSION]: [jsonResponse(401, { code: 'x' })] });

    renderPage();

    expect(await screen.findByRole('heading', { name: 'Entrar a tu estudio' })).toBeInTheDocument();
    expect(screen.getByLabelText('Contraseña')).toHaveAttribute('type', 'password');
    expect(screen.getByLabelText('Contraseña')).toHaveAttribute('autocomplete', 'current-password');
    expect(screen.getByLabelText('Usuario')).toHaveAttribute('autocomplete', 'username');
    expect(screen.getByText(/solo para el fotógrafo/)).toBeInTheDocument();
  });

  it('asks the server who is signed in before it shows anything', async () => {
    answer({ [SESSION]: [jsonResponse(401, { code: 'x' })] });

    renderPage();
    await screen.findByLabelText('Usuario');

    expect(requests()[0]).toBe(SESSION);
    expect(fetchMock.mock.calls[0]?.[1]).toMatchObject({ credentials: 'include' });
  });

  it('keeps search engines out of the page', async () => {
    answer({ [SESSION]: [jsonResponse(401, { code: 'x' })] });

    const { unmount } = renderPageWithUnmount();
    await screen.findByLabelText('Usuario');

    expect(document.querySelector('meta[name="robots"]')).toHaveAttribute('content', 'noindex, nofollow');
    expect(document.title).toContain('Tu estudio');
    unmount();
    expect(document.querySelector('meta[name="robots"]')).toBeNull();
  });

  /**
   * Renders the page and gives back the unmount function.
   * @returns The render result.
   */
  function renderPageWithUnmount(): ReturnType<typeof render> {
    const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
    return render(
      <QueryClientProvider client={client}>
        <StudioPage />
      </QueryClientProvider>,
    );
  }

  it('asks for both fields before it sends anything', async () => {
    answer({ [SESSION]: [jsonResponse(401, { code: 'x' })] });
    const user = renderPage();

    await user.click(await screen.findByRole('button', { name: 'Entrar' }));

    expect(screen.getByRole('alert')).toHaveTextContent('Escribe tu usuario y tu contraseña.');
    expect(requests()).toEqual([SESSION]);
  });

  it('goes through the code: password, code, then the galleries', async () => {
    answer({
      [SESSION]: [jsonResponse(401, { code: 'x' })],
      [LOGIN]: [jsonResponse(202, CODE_REQUIRED)],
      [VERIFY]: [jsonResponse(200, { user: studioUserPayload })],
      [GALLERIES]: [jsonResponse(200, { items: [galleryPayload], nextCursor: null })],
    });
    const user = renderPage();

    await typePassword(user);
    expect(await screen.findByRole('heading', { name: 'Escribe el código' })).toBeInTheDocument();
    expect(screen.getByText('a***@example.com')).toBeInTheDocument();
    await user.type(screen.getByLabelText('Código de 6 dígitos'), '123456');
    await user.click(screen.getByRole('button', { name: 'Verificar' }));

    expect(await screen.findByRole('heading', { name: 'Tus galerías' })).toBeInTheDocument();
    expect(screen.getByText('Hola, Ana Pérez.')).toBeInTheDocument();
    expect(requests()).toEqual([SESSION, LOGIN, VERIFY, GALLERIES]);
    expect(JSON.parse(String(fetchMock.mock.calls[1]?.[1]?.body))).toEqual({ username: 'ana', password: 'correct horse' });
    expect(JSON.parse(String(fetchMock.mock.calls[2]?.[1]?.body))).toEqual({ code: '123456', rememberDevice: false });
  });

  it('remembers the browser only when the box is ticked, and the box starts empty', async () => {
    answer({
      [SESSION]: [jsonResponse(401, { code: 'x' })],
      [LOGIN]: [jsonResponse(202, CODE_REQUIRED)],
      [VERIFY]: [jsonResponse(200, { user: studioUserPayload })],
      [GALLERIES]: [jsonResponse(200, { items: [], nextCursor: null })],
    });
    const user = renderPage();
    await typePassword(user);

    const box = await screen.findByRole('checkbox', { name: /Recordar este navegador/ });
    expect(box).not.toBeChecked();
    await user.click(box);
    await user.type(screen.getByLabelText('Código de 6 dígitos'), '123456');
    await user.click(screen.getByRole('button', { name: 'Verificar' }));

    await screen.findByRole('heading', { name: 'Tus galerías' });
    expect(JSON.parse(String(fetchMock.mock.calls[2]?.[1]?.body))).toEqual({ code: '123456', rememberDevice: true });
  });

  it('skips the code in a browser the server remembers', async () => {
    answer({
      [SESSION]: [jsonResponse(401, { code: 'x' })],
      [LOGIN]: [jsonResponse(200, { status: 'SignedIn', user: studioUserPayload })],
      [GALLERIES]: [jsonResponse(200, { items: [], nextCursor: null })],
    });
    const user = renderPage();

    await typePassword(user);

    expect(await screen.findByRole('heading', { name: 'Tus galerías' })).toBeInTheDocument();
    expect(requests()).toEqual([SESSION, LOGIN, GALLERIES]);
  });

  it('says a wrong password in words that do not tell which part was wrong, and clears the password field', async () => {
    answer({ [SESSION]: [jsonResponse(401, { code: 'x' })], [LOGIN]: [jsonResponse(401, { code: 'auth.invalid_credentials' })] });
    const user = renderPage();

    await typePassword(user);

    expect(await screen.findByRole('alert')).toHaveTextContent('El usuario o la contraseña no son correctos.');
    expect(screen.getByLabelText('Usuario')).toHaveValue('ana');
  });

  it('tells how long to wait when the account is locked', async () => {
    answer({
      [SESSION]: [jsonResponse(401, { code: 'x' })],
      [LOGIN]: [new Response(JSON.stringify({ code: 'auth.account_locked' }), { status: 429, headers: { 'Content-Type': 'application/json', 'Retry-After': '900' } })],
    });
    const user = renderPage();

    await typePassword(user);

    expect(await screen.findByRole('alert')).toHaveTextContent('15 minutos');
  });

  it('explains an account without a verified email', async () => {
    answer({ [SESSION]: [jsonResponse(401, { code: 'x' })], [LOGIN]: [jsonResponse(403, { code: 'auth.web_requires_verified_email' })] });
    const user = renderPage();

    await typePassword(user);

    expect(await screen.findByRole('alert')).toHaveTextContent('verifica el correo de tu cuenta desde la app');
  });

  it('keeps only the digits of the code, six at most', async () => {
    answer({ [SESSION]: [jsonResponse(401, { code: 'x' })], [LOGIN]: [jsonResponse(202, CODE_REQUIRED)] });
    const user = renderPage();
    await typePassword(user);

    const field = await screen.findByLabelText('Código de 6 dígitos');
    await user.type(field, '12ab34-5678');

    expect(field).toHaveValue('123456');
    expect(field).toHaveAttribute('autocomplete', 'one-time-code');
    expect(field).toHaveAttribute('inputmode', 'numeric');
  });

  it('asks for the six digits before it sends the code', async () => {
    answer({ [SESSION]: [jsonResponse(401, { code: 'x' })], [LOGIN]: [jsonResponse(202, CODE_REQUIRED)] });
    const user = renderPage();
    await typePassword(user);

    await user.type(await screen.findByLabelText('Código de 6 dígitos'), '123');
    await user.click(screen.getByRole('button', { name: 'Verificar' }));

    expect(screen.getByRole('alert')).toHaveTextContent('Escribe los 6 dígitos del código.');
    expect(requests()).toEqual([SESSION, LOGIN]);
  });

  it('says a bad code is not valid, without saying whether it was wrong, expired or spent', async () => {
    answer({
      [SESSION]: [jsonResponse(401, { code: 'x' })],
      [LOGIN]: [jsonResponse(202, CODE_REQUIRED)],
      [VERIFY]: [jsonResponse(422, { code: 'auth.invalid_login_code' })],
    });
    const user = renderPage();
    await typePassword(user);

    await user.type(await screen.findByLabelText('Código de 6 dígitos'), '000000');
    await user.click(screen.getByRole('button', { name: 'Verificar' }));

    expect(await screen.findByRole('alert')).toHaveTextContent('El código no es válido o ya venció');
  });

  it('cannot ask for another code during the first minute, and goes back to the password when asked', async () => {
    answer({ [SESSION]: [jsonResponse(401, { code: 'x' })], [LOGIN]: [jsonResponse(202, CODE_REQUIRED)] });
    const user = renderPage();
    await typePassword(user);

    expect(await screen.findByRole('button', { name: 'Reenviar en 60 s' })).toBeDisabled();
    await user.click(screen.getByRole('button', { name: 'Volver' }));

    expect(await screen.findByRole('heading', { name: 'Entrar a tu estudio' })).toBeInTheDocument();
    expect(window.sessionStorage.length).toBe(0);
  });

  it('continues at the code after a reload, because the page remembers that one is waiting', async () => {
    window.sessionStorage.setItem('photosense.studio.pending-code', JSON.stringify({ maskedEmail: 'a***@example.com' }));
    answer({ [SESSION]: [jsonResponse(401, { code: 'x' })] });

    renderPage();

    expect(await screen.findByRole('heading', { name: 'Escribe el código' })).toBeInTheDocument();
    expect(screen.getByText('a***@example.com')).toBeInTheDocument();
  });

  it('forgets that a code was waiting once the session starts', async () => {
    window.sessionStorage.setItem('photosense.studio.pending-code', JSON.stringify({ maskedEmail: 'a***@example.com' }));
    answer({
      [SESSION]: [jsonResponse(401, { code: 'x' })],
      [VERIFY]: [jsonResponse(200, { user: studioUserPayload })],
      [GALLERIES]: [jsonResponse(200, { items: [], nextCursor: null })],
    });
    const user = renderPage();

    await user.type(await screen.findByLabelText('Código de 6 dígitos'), '123456');
    await user.click(screen.getByRole('button', { name: 'Verificar' }));

    await screen.findByRole('heading', { name: 'Tus galerías' });
    expect(window.sessionStorage.length).toBe(0);
  });

  it('sends a new code once the minute passed, and says so', async () => {
    vi.useFakeTimers({ shouldAdvanceTime: true });
    try {
      answer({
        [SESSION]: [jsonResponse(401, { code: 'x' })],
        [LOGIN]: [jsonResponse(202, CODE_REQUIRED)],
        [RESEND]: [jsonResponse(202, { maskedEmail: 'a***@example.com', expiresAt: '2026-10-30T12:20:00+00:00' })],
      });
      const user = renderPageWith(userEvent.setup({ advanceTimers: (ms) => vi.advanceTimersByTime(ms) }));
      await typePassword(user);
      expect(await screen.findByRole('button', { name: 'Reenviar en 60 s' })).toBeDisabled();

      await act(async () => {
        await vi.advanceTimersByTimeAsync(61_000);
      });
      await user.click(await screen.findByRole('button', { name: 'Reenviar código' }));

      await waitFor(() => expect(requests()).toContain(RESEND));
      expect(await screen.findByRole('status')).toHaveTextContent('Te enviamos un código nuevo.');
    } finally {
      vi.useRealTimers();
    }
  });

  it('shows the galleries of a signed-in photographer, one card each, with their state and terms', async () => {
    answer({
      [SESSION]: [jsonResponse(200, { user: studioUserPayload })],
      [GALLERIES]: [jsonResponse(200, { items: [galleryPayload, { ...galleryPayloadNumber(2), status: 'Delivered', bonusPhotos: 0 }], nextCursor: null })],
    });

    renderPage();

    const cards = await screen.findAllByRole('article');
    expect(cards).toHaveLength(2);
    expect(within(cards[0]!).getByRole('heading', { name: 'María Pérez' })).toBeInTheDocument();
    expect(within(cards[0]!).getByText('Esperando las pruebas')).toBeInTheDocument();
    expect(within(cards[0]!).getByText('30 fotos incluidas · 5 de regalo')).toBeInTheDocument();
    expect(within(cards[0]!).getByText(/Foto extra: ₡/)).toBeInTheDocument();
    expect(within(cards[1]!).getByText('Entregada')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Ver más' })).toBeNull();
  });

  it('says there are no galleries yet, and where they come from', async () => {
    answer({ [SESSION]: [jsonResponse(200, { user: studioUserPayload })], [GALLERIES]: [jsonResponse(200, { items: [], nextCursor: null })] });

    renderPage();

    expect(await screen.findByText('Todavía no tienes galerías')).toBeInTheDocument();
    expect(screen.getByText(/Aparecen solas cuando completas una sesión/)).toBeInTheDocument();
  });

  it('loads the next page with the cursor when "Ver más" is tapped, and keeps what it already shows', async () => {
    answer({
      [SESSION]: [jsonResponse(200, { user: studioUserPayload })],
      [GALLERIES]: [jsonResponse(200, { items: [galleryPayloadNumber(1)], nextCursor: '639.abc' })],
      'GET /api/web/galleries?limit=5&cursor=639.abc': [jsonResponse(200, { items: [galleryPayloadNumber(2)], nextCursor: null })],
    });
    const user = renderPage();

    await user.click(await screen.findByRole('button', { name: 'Ver más' }));

    expect(await screen.findByRole('heading', { name: 'Cliente 2' })).toBeInTheDocument();
    expect(screen.getByRole('heading', { name: 'Cliente 1' })).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Ver más' })).toBeNull();
  });

  it('keeps the list and offers to try again when the next page fails', async () => {
    answer({
      [SESSION]: [jsonResponse(200, { user: studioUserPayload })],
      [GALLERIES]: [jsonResponse(200, { items: [galleryPayloadNumber(1)], nextCursor: '639.abc' })],
      'GET /api/web/galleries?limit=5&cursor=639.abc': [jsonResponse(500, { code: 'server.error' })],
    });
    const user = renderPage();

    await user.click(await screen.findByRole('button', { name: 'Ver más' }));

    expect(await screen.findByRole('button', { name: 'Reintentar' })).toBeInTheDocument();
    expect(screen.getByRole('alert')).toHaveTextContent('No se pudo cargar más');
    expect(screen.getByRole('heading', { name: 'Cliente 1' })).toBeInTheDocument();
  });

  it('explains it when the galleries cannot be loaded, and loads them again on request', async () => {
    answer({
      [SESSION]: [jsonResponse(200, { user: studioUserPayload })],
      [GALLERIES]: [jsonResponse(500, { code: 'server.error' }), jsonResponse(200, { items: [galleryPayload], nextCursor: null })],
    });
    const user = renderPage();

    await user.click(await screen.findByRole('button', { name: 'Intentar de nuevo' }));

    expect(await screen.findByRole('heading', { name: 'María Pérez' })).toBeInTheDocument();
  });

  it('goes back to the password when the server says the session is over', async () => {
    answer({ [SESSION]: [jsonResponse(200, { user: studioUserPayload })], [GALLERIES]: [jsonResponse(401, { code: 'x' })] });

    renderPage();

    expect(await screen.findByRole('heading', { name: 'Entrar a tu estudio' })).toBeInTheDocument();
  });

  it('signs out, and it can also forget the browser, so the next sign-in asks for the code', async () => {
    answer({
      [SESSION]: [jsonResponse(200, { user: studioUserPayload })],
      [GALLERIES]: [jsonResponse(200, { items: [galleryPayload], nextCursor: null })],
      [LOGOUT]: [new Response(null, { status: 204 })],
    });
    const user = renderPage();

    await user.click(await screen.findByRole('button', { name: 'Salir y olvidar este navegador' }));

    expect(await screen.findByRole('heading', { name: 'Entrar a tu estudio' })).toBeInTheDocument();
    expect(JSON.parse(String(fetchMock.mock.calls.find(([input]) => String(input).includes('/logout'))?.[1]?.body))).toEqual({ forgetDevice: true });
    expect(screen.queryByText('María Pérez')).toBeNull();
  });

  it('signs out without forgetting the browser with the plain button', async () => {
    answer({
      [SESSION]: [jsonResponse(200, { user: studioUserPayload })],
      [GALLERIES]: [jsonResponse(200, { items: [], nextCursor: null })],
      [LOGOUT]: [new Response(null, { status: 204 })],
    });
    const user = renderPage();

    await user.click(await screen.findByRole('button', { name: 'Salir' }));

    await screen.findByRole('heading', { name: 'Entrar a tu estudio' });
    expect(JSON.parse(String(fetchMock.mock.calls.find(([input]) => String(input).includes('/logout'))?.[1]?.body))).toEqual({ forgetDevice: false });
  });

  it('shows the sign-in even if signing out failed on the server: the page does not keep what was shown', async () => {
    answer({
      [SESSION]: [jsonResponse(200, { user: studioUserPayload })],
      [GALLERIES]: [jsonResponse(200, { items: [galleryPayload], nextCursor: null })],
      [LOGOUT]: [jsonResponse(500, { code: 'server.error' })],
    });
    const user = renderPage();

    await user.click(await screen.findByRole('button', { name: 'Salir' }));

    await waitFor(() => expect(screen.queryByRole('heading', { name: 'María Pérez' })).toBeNull());
    expect(await screen.findByRole('heading', { name: 'Entrar a tu estudio' })).toBeInTheDocument();
  });

  it('explains it when the server cannot be asked who is signed in, and tries again on request', async () => {
    answer({ [SESSION]: [jsonResponse(500, { code: 'server.error' }), jsonResponse(401, { code: 'x' })] });
    const user = renderPage();

    await user.click(await screen.findByRole('button', { name: 'Intentar de nuevo' }));

    expect(await screen.findByRole('heading', { name: 'Entrar a tu estudio' })).toBeInTheDocument();
  });
});
