# A4 — Infraestructura y despliegue

Esta página dice **qué se configuró en cada plataforma** y cómo montar todo desde cero. El detalle de pasos con capturas de pantalla está en `docs/Deployment.md`.

## 1. Mapa

```
 Cliente / fotógrafo (navegador)           Fotógrafo (app Android)
            │                                       │
   Cloudflare Pages  ── develop.photosensestudio.com│
   (sitio estático)                                 │
            │ HTTPS                                 │ HTTPS
            ▼                                       ▼
        Railway: photostudio-api  ── develop-api.photosensestudio.com
            │         │              │
            │         │              └──► Resend (correo)   Expo Push → FCM
            ▼         ▼
     MongoDB Atlas   Cloudflare R2  ◄── subida y bajada de fotos con URL firmada
            ▲
        Railway: photostudio-worker (cron cada 15 minutos)
```

| Servicio | Para qué | Plan y costo |
|---|---|---|
| **Railway** | API y worker (misma imagen) | Hobby, $5/mes con $5 de consumo incluido; estimado $5–8 |
| **MongoDB Atlas** | Datos | M0 gratis (sin backups propios) |
| **Cloudflare R2** | Fotos | 10 GB gratis, luego $0,015/GB-mes |
| **Cloudflare Pages** | Sitio web | Gratis |
| **Cloudflare Access** | Protege el sitio de develop | Gratis |
| **Resend** | Correo | Gratis: 3 000/mes y 100/día (se comparte con otra app) |
| **Expo EAS** | Compilar la app y actualizaciones | Gratis: 15 builds de cada sistema al mes |
| **Firebase (FCM)** | Transporte del push en Android | Gratis |
| **GitHub Actions** | CI/CD | Gratis |
| **Apple Developer** | iOS y APNs | $99/año — **pendiente** |
| **Google Play** | Publicar | ~$25 una vez — opcional |
| **Dominio** | `photosensestudio.com` | ~$10–12/año |

Total estimado: ~$6–9/mes sin iOS.

## 2. Ambientes

| Ambiente | Estado | Sitio | API | Base |
|---|---|---|---|---|
| **develop** | Activo | `develop.photosensestudio.com` | `develop-api.photosensestudio.com` | `photostudio_dev` (clúster `photo_studio_dev`) |
| **production** | **Pendiente** | `photosensestudio.com` | `api.photosensestudio.com` | `photostudio` |

Los nombres son de **un solo nivel** (no `*.develop.…`) porque el certificado gratuito de Cloudflare no cubre subdominios de segundo nivel. Además `media.photosensestudio.com` para R2. El sitio de develop está detrás de Cloudflare Access y no se indexa.

El flujo de ramas es solo por *pull request*: `feature/*` → `develop` → (futuro) `main` para producción con aprobación manual. Nadie empuja directo a `develop` ni a `main`.

## 3. CI/CD

**`.github/workflows/ci.yml`** corre en cada PR y en cada push: backend (levanta Mongo en réplica y el almacenamiento S3 local con `docker compose`, restaura, compila con advertencias como errores y corre todas las pruebas), imagen Docker, app móvil (tsc, ESLint, Jest) y sitio (tsc, ESLint, Vitest, build).

**`.github/workflows/deploy-develop.yml`** corre en cada push a `develop`: primero repite el CI y, **solo si pasa**, despliega en paralelo:
1. El sitio a Cloudflare Pages (`wrangler pages deploy`, proyecto `photosense-develop`).
2. La API a Railway: `railway up ./backend --path-as-root --service photostudio-api --environment develop --ci`, y espera a que responda.
3. El worker a Railway con el mismo comando para `photostudio-worker`.

Las acciones de GitHub están fijadas por SHA (el repositorio es público) y la versión del CLI de Railway está fijada. Los servicios de Railway son **vacíos** (sin conexión a GitHub): el despliegue lo hace Actions.

**Secretos de GitHub** (entorno `develop`): `CLOUDFLARE_API_TOKEN`, `CLOUDFLARE_ACCOUNT_ID`, `RAILWAY_TOKEN` (token de proyecto del ambiente). **Variables:** `API_URL`, `PAGES_PROJECT`, `SITE_URL`.

## 4. Railway

Proyecto con ambiente `develop` y dos servicios:

| Servicio | Comando | Particularidades |
|---|---|---|
| `photostudio-api` | el `CMD` de la imagen | Health check `/health/ready`; puerto `PORT` lo pone Railway |
| `photostudio-worker` | `dotnet worker/PhotoStudio.Worker.dll` | `WORKER_RUN_ONCE=true`, cron `*/15 * * * *`, sin reinicio |

**Importante:** Railway **no aplica** la sección `deploy` de un `railway.json` subido con `railway up` (el worker llegó a arrancar la API). Por eso no hay `railway*.json` y los ajustes se fijan **en el servicio** con `node backend/scripts/configure-railway.mjs --project <id> --environment develop` (repetible). En las variables, los `<>` de los ejemplos son marcadores, no parte del valor.

## 5. Variables de entorno

Nunca hay secretos en el código ni en `appsettings`. Los secretos solo viven en Railway y en el entorno `develop` de GitHub. **No pegues claves en chats ni en commits.**

### Obligatorias
| Variable | Dónde | Qué es |
|---|---|---|
| `MONGODB_CONNECTION_STRING` | API y worker | Cadena de Atlas (secreto) |
| `MONGODB_DATABASE_NAME` | API y worker | `photostudio_dev` / `photostudio` |
| `JWT_SIGNING_KEY` | API | ≥ 32 caracteres (secreto); una con `dev-only` se rechaza fuera de Development |
| `PORTAL_BASE_URL` | API | Dirección de los enlaces enviados a clientes y origen de CORS |

### Correo y fotos
| Variable | Dónde | Qué es |
|---|---|---|
| `RESEND_API_KEY` | API y worker | Clave de Resend (secreto) |
| `EMAIL_FROM` | API y worker | Remitente, p. ej. `no-reply@mail.photosensestudio.com` |
| `R2_ACCOUNT_ID`, `R2_BUCKET`, `R2_ACCESS_KEY_ID`, `R2_SECRET_ACCESS_KEY` | API **y worker** | Las cuatro o ninguna. El worker las necesita desde el archivado (PR 10) |
| `R2_ENDPOINT` | solo local | Para el servidor S3 de desarrollo; no la pongas en Railway |

Sin `RESEND_API_KEY`/`EMAIL_FROM` en producción el correo responde 503 `email.delivery_failed` (la API arranca igual). Sin `R2_*` todo lo de fotos responde 503 `storage.unavailable`.

### Opcionales
`JWT_ISSUER`, `JWT_AUDIENCE`, `JWT_ACCESS_TOKEN_MINUTES` (15), `JWT_REFRESH_TOKEN_DAYS` (30), `AUTH_REGISTRATION_ENABLED` (`true`; ponla en `false` en producción cuando tu cuenta ya exista), `AUTH_EMAIL_VERIFICATION_REQUIRED` (`true`), `PORTAL_SESSION_DAYS` (30; 1–90; inválido = no arranca), `PORTAL_ORIGINS`, `API_DOCS_ENABLED` (solo develop), `EXPO_ACCESS_TOKEN` (secreto, API y worker), `WORKER_RUN_ONCE`.

### Políticas de negocio (valores por defecto)
`BOOKING_TENTATIVE_HOLD_HOURS` 48 · `BOOKING_DEPOSIT_PERCENTAGE` 0.5 · `BOOKING_FREE_CANCELLATION_WINDOW_HOURS` 72 · `BOOKING_RESCHEDULE_MIN_NOTICE_HOURS` 48 · `BOOKING_MAX_RESCHEDULES` 1 · `BOOKING_CLIENT_ABSENT_TOLERANCE_MINUTES` 30 · `BOOKING_CLIENT_ABSENT_REVERT_WINDOW_DAYS` 7 · `BOOKING_LATE_CANCELLATION_RETENTION` 1 · `BOOKING_CLIENT_ABSENT_RETENTION` 1 · `BOOKING_PHOTOGRAPHER_CANCELLATION_REFUND` 1 · `BOOKING_DELIVERY_DEADLINE_DAYS` 21 · `GALLERY_INCLUDED_PHOTOS_BASIC/STANDARD/PREMIUM` 15/30/50 · `GALLERY_EXTRA_PHOTO_PRICE` 3000. Un valor inválido detiene el arranque.

### En otras plataformas
- **Cloudflare Pages:** `VITE_ENVIRONMENT=develop` (muestra la franja "Ambiente de pruebas").
- **EAS:** variable de archivo `GOOGLE_SERVICES_JSON` (el `google-services.json` de Firebase, que **no** está en git).
- **App móvil:** `EXPO_PUBLIC_API_URL` en `mobile/.env` y, opcional, `EXPO_PUBLIC_STUDIO_URL` (la dirección de la web del fotógrafo, por ejemplo `https://develop.photosensestudio.com/studio`; sin ella no aparece el enlace «Abrir la web del fotógrafo» de Inicio). Todo `EXPO_PUBLIC_*` va dentro del paquete: nunca secretos, y cambiarlo exige reiniciar Metro con `npx expo start --clear`.

## 6. Montar todo desde cero

1. **Dominio y DNS:** comprar `photosensestudio.com`; DNS en Cloudflare.
2. **MongoDB Atlas:** clúster M0 en la misma región que Railway; usuario de base; permitir las IP de Railway; copiar la cadena a `MONGODB_CONNECTION_STRING`.
3. **Cloudflare R2:** crear el bucket `photostudio-media` privado; crear un token de API con permiso de objetos en ese bucket (esas son las `R2_*`); configurar CORS (orígenes del sitio, métodos `PUT`, `GET`, `HEAD`, encabezado `Content-Type`).
4. **Resend:** añadir el dominio `mail.photosensestudio.com`; poner los registros SPF, DKIM y DMARC en Cloudflare; crear la clave de API.
5. **Railway:** crear el proyecto, el ambiente `develop` y los servicios `photostudio-api` y `photostudio-worker` vacíos; ejecutar `configure-railway.mjs`; cargar las variables de arriba; crear el token de proyecto para GitHub.
6. **Cloudflare Pages:** crear el proyecto `photosense-develop`; dominio `develop.photosensestudio.com`; proteger con Access si es de pruebas.
7. **GitHub:** crear el entorno `develop` con los secretos y variables de la sección 3; activar protección de ramas.
8. **Primer despliegue:** empujar a `develop`; el flujo corre CI y despliega.
9. **Firebase y EAS:** proyecto Firebase `photosense-studio`, app Android `com.photosensestudio.app`; subir la clave de cuenta de servicio FCM V1 en expo.dev → Credentials; `eas env:create` para `GOOGLE_SERVICES_JSON`; primer build (ver [A6](A6-Clientes.md)).
10. **Cuenta del fotógrafo:** crearla desde la app; si hace falta un operador de sistema: `node backend/scripts/grant-system-role.js` (nunca por registro).

Después de crear tu cuenta en producción: `AUTH_REGISTRATION_ENABLED=false`.

## 7. Producción (pendiente)

Falta: rama `main`, base `photostudio`, dominio y certificados, aprobación manual del despliegue, `AUTH_REGISTRATION_ENABLED=false`, no definir `API_DOCS_ENABLED`, CORS de R2 con el origen de producción, perfil `production` de EAS y Google Play, y el **respaldo semanal** (`mongodump` → R2) con su servicio en el worker, porque Atlas M0 no tiene copias de seguridad.

## 8. Windows y Smart App Control

En Windows, Smart App Control puede bloquear el `PhotoStudio.Api.dll` recién compilado y hacer fallar todas las pruebas de API en local sin que sea un error del código. Alternativa: correr la suite en un contenedor (`mcr.microsoft.com/dotnet/sdk:10.0`) sobre una copia sin `bin/obj`; es lo mismo que hace el CI. Ver [A7](A7-Pruebas-y-operacion.md).
