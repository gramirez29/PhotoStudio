# PhotoStudio (PhotoSense Studio)

Plataforma para fotógrafos (retrato y eventos, mercado inicial Costa Rica): reservas, contratos, cobros y entrega de galerías. El fotógrafo usa **una app móvil**; el cliente nunca instala nada: usa **un enlace web por reserva**.

El principio del producto: la app solo vale si evita pérdida de dinero (clientes que no se presentan, saldos sin cobrar, entregas atrasadas).

## Estructura

```
PhotoStudio/
├── backend/                  .NET 10 · Clean Architecture · monolito modular · MongoDB
│   ├── PhotoStudio.slnx
│   ├── src/
│   │   ├── PhotoStudio.Domain          Agregados (Booking, Notification, PushDevice, User…), value objects, eventos
│   │   ├── PhotoStudio.Application     Casos de uso, puertos (interfaces), respuestas
│   │   ├── PhotoStudio.Infrastructure  MongoDB, outbox, correo (Resend), push (Expo), autenticación
│   │   ├── PhotoStudio.Api             Minimal API, errores, rate limiting, composición
│   │   └── PhotoStudio.Worker          Entrega el outbox, expira reservas y entrega avisos (cron o siempre encendido)
│   └── tests/                  Domain · Application · Infrastructure (Mongo real) · Api (en memoria, Mongo real)
├── mobile/                   App del fotógrafo: Expo SDK 57 · Expo Router · TypeScript estricto
├── portal/                   Sitio web: página principal y portal del cliente (React 19 + Vite, Cloudflare Pages)
├── docs/                     Documentación detallada (ver abajo)
└── .github/workflows/        CI y despliegue del ambiente develop
```

## Documentación

| Documento | De qué trata |
|---|---|
| [`docs/Authentication.md`](docs/Authentication.md) | Cuentas, JWT, refresh tokens rotativos, rol de sistema |
| [`docs/ClientPortal.md`](docs/ClientPortal.md) · [`ClientPortalWeb.md`](docs/ClientPortalWeb.md) | Enlace por reserva, código por correo, comprobante SINPE (API y sitio web) |
| [`docs/Notifications.md`](docs/Notifications.md) | La bandeja de avisos y los recordatorios |
| [`docs/PushNotifications.md`](docs/PushNotifications.md) | **Push al teléfono:** herramientas, plataformas, arquitectura, decisiones, configuración, pruebas y TODO |
| [`docs/Billing.md`](docs/Billing.md) | Reembolsos y retenciones |
| [`docs/Outbox-Worker Functionality.md`](docs/Outbox-Worker%20Functionality.md) | Outbox transaccional, worker, pasadas de mantenimiento y despertador de la API |
| [`docs/Deployment.md`](docs/Deployment.md) | Ambiente develop (GitHub Actions, Railway, Cloudflare Pages), build de EAS |
| [`portal/README.md`](portal/README.md) | El sitio web |

## Backend

Requisitos: .NET SDK 10.0.1xx o superior y MongoDB (el de `docker-compose.yml` es un replica set de un nodo, necesario para las transacciones).

```bash
cd backend
docker compose up -d --wait        # MongoDB local en 127.0.0.1:27019
dotnet restore && dotnet build && dotnet test
dotnet run --project src/PhotoStudio.Api --launch-profile PhotoStudio.Api   # http://localhost:8080
```

Las pruebas de integración se saltan solas si no hay MongoDB (`MONGODB_TEST_CONNECTION_STRING`, por defecto el compose local). En Windows, Smart App Control puede bloquear el DLL recién compilado y hacer fallar todas las pruebas; no es un fallo del código: corre la suite en un contenedor `mcr.microsoft.com/dotnet/sdk:10.0` sobre una copia sin `bin/obj` (es lo mismo que hace el CI).

Variables de entorno principales (todo por variables; nada de secretos en `appsettings`):

| Variable | Dónde | Descripción |
|---|---|---|
| `MONGODB_CONNECTION_STRING` | API, worker | Obligatoria (Atlas en producción) |
| `MONGODB_DATABASE_NAME` | API, worker | Por defecto `photostudio` |
| `JWT_SIGNING_KEY` | API | Obligatoria, ≥ 32 caracteres; en local `launchSettings.json` trae una de desarrollo |
| `JWT_ISSUER`, `JWT_AUDIENCE`, `JWT_ACCESS_TOKEN_MINUTES`, `JWT_REFRESH_TOKEN_DAYS` | API | Ver `docs/Authentication.md` |
| `AUTH_REGISTRATION_ENABLED` | API | `true` por defecto; `false` cierra el registro |
| `PORTAL_BASE_URL` | API | Obligatoria fuera de Development: dirección de los enlaces y origen CORS |
| `RESEND_API_KEY`, `EMAIL_FROM` | API | Correo del portal (en Development se escribe en el log) |
| `EXPO_ACCESS_TOKEN` | API, worker | Opcional: seguridad mejorada del push de Expo |
| `WORKER_RUN_ONCE` | worker | `true`: hace una pasada y termina (cron) |
| `BOOKING_*` | API | Políticas de reserva (ver `EnvironmentBookingPolicyProvider`) |
| `PORT` | API | Lo asigna Railway; 8080 por defecto en el contenedor |

La lista de endpoints está en el documento OpenAPI (`GET /openapi/v1.json`) y, por módulo, en los documentos de arriba. Salud: `/health/live` y `/health/ready` (incluye MongoDB).

## App móvil

Requisitos: Node 20+.

```bash
cd mobile
npm install
cp .env.example .env               # EXPO_PUBLIC_API_URL
npm run verify                     # tsc + ESLint (sin any) + pruebas Jest
npx expo start --clear             # solo sirve para lo que no necesita código nativo
```

- En un dispositivo físico, `EXPO_PUBLIC_API_URL` debe ser la IP de tu computadora en la red local (o la API de develop: `https://develop-api.photosensestudio.com`), no `localhost`. Las variables `EXPO_PUBLIC_*` van dentro del paquete: nunca secretos.
- **Push y build de desarrollo:** las notificaciones push no funcionan en Expo Go. Se prueba con el APK de desarrollo de EAS (`npx eas-cli build --platform android --profile development`) y un servidor `npx expo start --dev-client`. Pasos, credenciales (Firebase) y solución de problemas en [`docs/PushNotifications.md`](docs/PushNotifications.md) y [`docs/Deployment.md`](docs/Deployment.md) §7.
- Estructura de `mobile/src`: `app/` (pantallas) · `components/` · `hooks/` · `forms/` · `api/` · `types/` (todos los tipos, en espejo de las capas) · `utils/` (funciones puras) · `navigation/` · `constants/` · `push/` (efectos de notificaciones) · `session/` · `storage/` · `theme/` · `config/`.

## Sitio web

Página principal y portal del cliente (`/b/{token}`) en `portal/` (React 19 + Vite). Ver [`portal/README.md`](portal/README.md).

## Ambientes

El ambiente **develop** se despliega solo con cada push a `develop` (GitHub Actions → API y worker en Railway, sitio en Cloudflare Pages), con acceso al sitio restringido por Cloudflare Access. Pasos y estado en [`docs/Deployment.md`](docs/Deployment.md). Producción: pendiente.

## Reglas del proyecto

- Todo se hace por *pull request* a `develop`; no hay pushes directos a `develop` ni a `main`.
- Toda clase, método, función, propiedad, interfaz y tipo lleva comentario de documentación (XML docs en C#, JSDoc en TypeScript).
- TypeScript sin `any` (`@typescript-eslint/no-explicit-any` en `error`). Las respuestas de la API se validan en tiempo de ejecución (`src/api/guards.ts`) antes de tiparse.
- Ninguna interfaz ni tipo dentro de componentes, hooks, utils o api: viven en `src/types/`.
- `TreatWarningsAsErrors` en el backend: una advertencia rompe el build.
- La lógica de estados vive solo en el dominio; los clientes muestran las `allowedActions` que devuelve la API.
- Código, nombres y comentarios de código en inglés; textos de UI y documentación en español.
