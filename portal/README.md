# Portal del cliente (PhotoSense Studio)

La página que el cliente abre desde el enlace de su reserva (`/b/{token}`). Plan y alcance en `docs/ClientPortalWeb.md`; el
backend que consume, en `docs/ClientPortal.md`.

React 19 + Vite + TypeScript estricto. Sin librería de UI. Tipografías **Josefin Sans** (títulos) y **DM Sans** (texto),
alojadas en el propio sitio (`@fontsource-variable`), así que la página no hace pedidos a terceros.

## Comandos

```bash
cd portal
npm install
cp .env.example .env     # VITE_API_URL (por defecto http://localhost:8080)
npm run dev              # http://localhost:5173 (el API ya permite este origen en Development)
npm run verify           # tsc + eslint + vitest
npm run build            # dist/
```

Para probarlo con datos reales: levanta el backend, crea una reserva con correo desde la app, toca "Enviar enlace por WhatsApp" y
cambia el origen de la URL por `http://localhost:5173` (la ruta `/b/{token}` es la misma).

## Estructura

```
src/
├── api/          httpClient, guards (validación de respuestas), portalApi, queryClient
├── components/   Un componente por archivo (solo JSX)
├── hooks/        usePortalBooking
├── types/        TODAS las interfaces y tipos, en espejo de las capas
├── utils/        Funciones puras: formato es-CR, textos de estado, ruta, errores
├── theme/        tokens.css (paleta de la app + color de marca), global.css, booking.css, prueba de contraste
├── constants/    Valores `as const` del backend
└── config/       env.ts
```

Las mismas reglas que `mobile/`: sin `any`, nada de tipos dentro de componentes/hooks/utils/api, colores solo desde los tokens.

## Tema

La paleta es la de la app del fotógrafo más un color de marca del logo (`#3A4466`: banda superior, títulos y aro de foco). Es una
sola variación: no sigue el modo oscuro del dispositivo. Los colores viven solo en `src/theme/tokens.css`; `tokens.test.ts` comprueba
el contraste de cada par de texto y fondo (WCAG AA) y que la paleta siga siendo la de la app.

## Despliegue

El sitio (página principal en `/` y portal en `/b/{token}`) se publica en Cloudflare Pages desde GitHub Actions al hacer push a `develop`, solo si el CI pasa. Los pasos para ponerlo en marcha, los dominios y las variables están en `docs/Deployment.md`.

Variables de compilación (públicas, terminan en el paquete): `VITE_API_URL` (dirección del API) y `VITE_ENVIRONMENT` (`develop` muestra la franja "Ambiente de pruebas"; vacío en producción).

`public/_redirects` hace que cualquier ruta sirva la app (SPA), `public/_headers` pone las cabeceras de seguridad (`Referrer-Policy: no-referrer`, `X-Robots-Tag: noindex`, CSP) y `public/robots.txt` pide no indexar. El sitio no se indexa mientras la página principal no tenga su contenido definitivo.
