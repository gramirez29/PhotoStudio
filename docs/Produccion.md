# Salir a producción: referencia de configuración

Lista de **todo lo que hay que crear y configurar** para pasar de `develop` a producción, plataforma por plataforma, con los valores exactos y el orden. No repite los pasos con detalle de `docs/Deployment.md` (que describe cómo se montó develop): lo usa como base y dice **qué cambia** en producción.

> Estado al escribir esto (2026-11-06): develop está activo y completo; **producción no existe todavía**. Cada cosa que hay que construir dice **(crear)**. Los valores marcados con ⚠ son decisiones tuyas, con mi recomendación. **Ningún secreto va en este documento ni en el repositorio**: se generan en cada plataforma y viven solo en Railway, en el *environment* `production` de GitHub y en EAS.

## 0. Cómo se ve producción

```
 Navegador (cliente y fotógrafo)            App Android del fotógrafo
          │                                          │
  Cloudflare Pages                                    │
  photosensestudio.com                                │
          │ HTTPS                                     │ HTTPS
          ▼                                           ▼
   Railway (ambiente production): photostudio-api ── api.photosensestudio.com
          │        │                  │
          │        │                  ├──► Resend (mail.photosensestudio.com)
          │        │                  └──► Expo Push → FCM (y APNs en iOS)
          ▼        ▼
  MongoDB Atlas   Cloudflare R2 (bucket photostudio-media, privado)
  base `photostudio`        ▲
          ▲                 └── subida y bajada con URL firmada, directo del cliente
   Railway: photostudio-worker (cron cada 15 minutos)
```

| | develop (hoy) | producción |
|---|---|---|
| Sitio | `develop.photosensestudio.com` | `photosensestudio.com` |
| API | `develop-api.photosensestudio.com` | `api.photosensestudio.com` |
| Rama | `develop` | `main` |
| Despliegue | automático si pasa el CI | automático **con aprobación manual** |
| Base de datos | `photostudio_dev` (clúster `photo_studio_dev`) | `photostudio` |
| Bucket R2 | `photostudio-media-develop` | `photostudio-media` |
| Railway | ambiente `develop` | ambiente `production` (mismo proyecto `photosense`) |
| Pages | proyecto `photosense-develop` | proyecto `photosense` |
| Acceso al sitio | detrás de Cloudflare Access, sin indexar | **público** |
| Documentación de la API (`/swagger`) | encendida | **apagada** |
| Registro de cuentas | abierto | cerrado una vez creada tu cuenta |

Los nombres son de **un solo nivel**: el certificado gratuito de Cloudflare no cubre `*.algo.photosensestudio.com`. Sitio y API comparten dominio raíz, y eso **no es opcional**: la web del fotógrafo (`/studio`) usa cookies `__Host-` con `SameSite=Strict`, que solo funcionan entre el sitio y la API del mismo dominio.

## 1. Decisiones antes de empezar ⚠

| # | Decisión | Recomendación | Por qué |
|---|---|---|---|
| 1 | **Atlas: ¿M0 gratis o un clúster de pago?** | Un clúster de pago (M10 o equivalente) **antes de tener clientes reales** | M0 **no tiene respaldos de Atlas**, se pausa tras 30 días sin conexiones, tiene 0,5 GB y 100 operaciones por segundo. Aquí viven contratos y cobros. Si te quedas en M0, el respaldo semanal con `mongodump` deja de ser opcional (ver 8) |
| 2 | **¿Se indexa el sitio en buscadores?** | Sí para la página principal; no para `/b/…` (cliente) ni `/studio` | Hoy **todo** el sitio dice `noindex` (`portal/index.html` y `portal/public/_headers`). Para indexar la principal hay que cambiar eso (ver 5) |
| 3 | **¿Quién puede crear cuenta?** | Cerrar el registro (`AUTH_REGISTRATION_ENABLED=false`) en cuanto exista tu cuenta | El registro abierto permite a cualquiera crear cuentas; solo se defiende con verificación de correo y límites |
| 4 | **Correo: ¿otra cuenta de Resend?** | Mantener la actual mientras sea un solo estudio | Comparte la cuota (3 000 al mes, 100 al día) con la otra aplicación. Si el estudio crece, cuenta propia |
| 5 | **iOS** | Después del primer lanzamiento en Android | Exige Apple Developer ($99/año) y APNs; el push y el build de iOS no están hechos |
| 6 | **Distribución de la app** | APK directo para el primer uso propio; Google Play ($25 una vez) cuando haya más fotógrafos | Play pide un AAB y cuenta de desarrollador |

## 2. Dominio y DNS (Cloudflare)

El dominio `photosensestudio.com` ya es tuyo y su DNS está en Cloudflare. Registros de producción **(crear)**:

| Tipo | Nombre | Destino | Proxy | Para qué |
|---|---|---|---|---|
| CNAME | `@` (raíz) | `photosense.pages.dev` | sí | El sitio (Pages crea el registro al activar el dominio) |
| CNAME | `api` | el destino que da Railway (`xxxx.up.railway.app`) | sí al principio; ver la nota | La API |
| varios | `mail` y los de Resend | los que da Resend | **no** | SPF, DKIM y DMARC (ya existen para `mail.photosensestudio.com`; se comparten) |

- **SSL/TLS:** modo **Full (strict)**. Con la API detrás del proxy de Cloudflare, el límite por dirección del inicio de sesión (que toma el último salto de `X-Forwarded-For`) vería la IP de Cloudflare y no la del cliente. Mientras no se haya probado, deja el registro `api` **sin proxy** (nube gris) o revisa con una prueba real quién llega en `X-Forwarded-For` antes de poner el proxy.
- No hace falta `media.photosensestudio.com`: el bucket es privado y se usa solo con URLs firmadas. Solo se crearía si algún día se sirven imágenes públicas.
- **Cloudflare Access: no se activa** en producción (el sitio es público).

## 3. MongoDB Atlas

Pasos base: `docs/Deployment.md` §3.1. En producción:

1. **Clúster:** el de la decisión 1, en **AWS · N. Virginia (us-east-1)**, la misma región que Railway. Nómbralo `photo_studio_prod`.
2. **Base:** `photostudio` (se crea con la primera escritura).
3. **Usuario de base de datos:** `photostudio-production`, contraseña larga generada, rol **personalizado `readWrite` solo sobre `photostudio`** (no `Atlas admin`). Distinto del de develop.
4. **Acceso de red:** Railway Hobby no tiene IP de salida fija, así que hoy hay que permitir `0.0.0.0/0`. Con un plan de pago se puede cerrar con **peering o IP fija** (Railway Pro ofrece IP estática); anótalo como mejora.
5. **Respaldos:** con clúster de pago, activa **Cloud Backup** (punto en el tiempo si el plan lo permite) y prueba **una restauración** a un clúster temporal antes de abrir al público.
6. Los índices los crea la API al arrancar (`MongoIndexInitializer`): no se crean a mano.
7. Cadena de conexión → `MONGODB_CONNECTION_STRING` de **producción** en Railway. Sin `<` ni `>`.

## 4. Cloudflare R2

Pasos base: `docs/Deployment.md` §3.2.1. En producción:

1. **Bucket** `photostudio-media`, privado, región cercana a Railway. **Sin** dominio público ni `r2.dev`.
2. **Token de API de R2** nuevo, **solo** para ese bucket, permiso *Object Read & Write*, sin permisos de administración. Guarda el *Access Key ID* y el *Secret* (se ve una vez).
3. **CORS del bucket** (el navegador sube directo a R2):
   ```json
   [
     {
       "AllowedOrigins": ["https://photosensestudio.com"],
       "AllowedMethods": ["PUT", "GET", "HEAD"],
       "AllowedHeaders": ["Content-Type"],
       "ExposeHeaders": ["ETag"],
       "MaxAgeSeconds": 3600
     }
   ]
   ```
   Sin `localhost` ni los orígenes de develop.
4. **Variables en Railway, en la API y en el worker** (las cuatro o ninguna): `R2_ACCOUNT_ID`, `R2_BUCKET=photostudio-media`, `R2_ACCESS_KEY_ID`, `R2_SECRET_ACCESS_KEY`. `R2_ENDPOINT` **no** se define (es del servidor local).
5. **Respaldos de la base: otro bucket**, `photostudio-backups`, con su propio token. El de las fotos no es para respaldos. Se usará para el `mongodump` semanal si sigues en M0.
6. **Regla de ciclo de vida** (opcional): eliminar de `photostudio-backups` lo que tenga más de 90 días.

## 5. Cloudflare Pages (el sitio)

Pasos base: `docs/Deployment.md` §3.2.

1. **Proyecto** `photosense` **(crear)**: `npx wrangler pages project create photosense --production-branch=main`.
2. **Dominio personalizado:** `photosensestudio.com` (y, si quieres, `www` con redirección al apex). Sin Access.
3. **Variables del build del sitio** (las lee el workflow al compilar): `VITE_API_URL=https://api.photosensestudio.com`, **y `VITE_ENVIRONMENT` sin definir** (sin ella no sale la franja «Ambiente de pruebas»).
4. **Indexación** ⚠ (decisión 2): hoy `portal/index.html` tiene `<meta name="robots" content="noindex, nofollow">` y `portal/public/_headers` pone `X-Robots-Tag: noindex, nofollow` en `/*`. Para producción: quitar ambos para la página principal y dejar `noindex` en `/b/*` y `/studio*` (en `_headers` hay que listar esas rutas aparte), y poner un `robots.txt` y un `sitemap.xml` con la principal. Esto exige un cambio en el repositorio **(crear)**; mientras no se haga, producción sale sin indexar, que es lo seguro.
5. **Cabeceras de seguridad:** ya van en `_headers` (CSP, `X-Frame-Options: DENY`, `Referrer-Policy: no-referrer`, etc.). La CSP permite `connect-src https://*.photosensestudio.com` y `https://*.r2.cloudflarestorage.com`, así que cubre `api.photosensestudio.com` sin cambios.
6. El `_redirects` del repositorio ya enruta el sitio de una sola página; no cambia.

## 6. Railway

Pasos base: `docs/Deployment.md` §3.3 y `docs/manual/A4-Infraestructura.md` §4. En producción:

1. En el proyecto `photosense`, **ambiente nuevo `production`** (Project → Environments → *New Environment*). Trabaja siempre con el ambiente elegido: los servicios y variables de develop no se tocan.
2. **Servicios vacíos y sin conexión a GitHub:** `photostudio-api` y `photostudio-worker` (los mismos nombres que en develop; el ambiente los separa).
3. **Ajustes de los servicios** (Railway **no aplica** la sección `deploy` de un `railway.json` subido con `railway up`): ejecutar `node backend/scripts/configure-railway.mjs --project <id> --environment production`. Deja: API con Dockerfile y health check `/health/ready`; worker con comando `dotnet worker/PhotoStudio.Worker.dll`, cron `*/15 * * * *` y sin reinicio.
4. **Variables de la API** (todas con valores **propios de producción**; ninguna se copia de develop):

   | Variable | Valor |
   |---|---|
   | `MONGODB_CONNECTION_STRING` | la de Atlas producción (secreto) |
   | `MONGODB_DATABASE_NAME` | `photostudio` |
   | `JWT_SIGNING_KEY` | **nueva**, `openssl rand -base64 48`; nunca una con `dev-only` (se rechaza) |
   | `PORTAL_BASE_URL` | `https://photosensestudio.com` (**obligatoria**: sin ella la API no arranca; es la dirección de los enlaces a clientes y el origen de CORS y de la web del fotógrafo) |
   | `RESEND_API_KEY` | clave de Resend con permiso de envío solo para el dominio de correo (secreto) |
   | `EMAIL_FROM` | `PhotoSense Studio <no-reply@mail.photosensestudio.com>` |
   | `R2_ACCOUNT_ID`, `R2_BUCKET`, `R2_ACCESS_KEY_ID`, `R2_SECRET_ACCESS_KEY` | las de la sección 4 |
   | `EXPO_ACCESS_TOKEN` | token de Expo (secreto, recomendado; ver `docs/PushNotifications.md` §10) |
   | `AUTH_REGISTRATION_ENABLED` | `true` solo hasta crear tu cuenta; luego `false` |
   | `AUTH_EMAIL_VERIFICATION_REQUIRED` | no definir (por defecto `true`) |
   | `API_DOCS_ENABLED` | **no definir**. Con ella ausente `/swagger` y `/openapi/v1.json` no existen |
   | `ASPNETCORE_ENVIRONMENT` | **no definir** (por defecto *Production*) |
   | `PORT` | la pone Railway |

   Opcionales: `PORTAL_ORIGINS` (orígenes extra de CORS; en producción normalmente vacío), `PORTAL_SESSION_DAYS`, `JWT_ISSUER`, `JWT_AUDIENCE`, `JWT_ACCESS_TOKEN_MINUTES` (15), `JWT_REFRESH_TOKEN_DAYS` (30) y las políticas `BOOKING_*` y `GALLERY_*` (`docs/manual/A4-Infraestructura.md` §5). Un valor inválido detiene el arranque.
5. **Variables del worker:** `MONGODB_CONNECTION_STRING`, `MONGODB_DATABASE_NAME`, `RESEND_API_KEY`, `EMAIL_FROM`, las cuatro `R2_*`, `EXPO_ACCESS_TOKEN`, `WORKER_RUN_ONCE=true`, `PORTAL_BASE_URL` (los correos del worker la usan para los enlaces) y las mismas políticas `BOOKING_*`/`GALLERY_*` que la API si las cambias: **tienen que coincidir** entre los dos servicios. **No** lleva `JWT_SIGNING_KEY` salvo que el arranque la pida; revisa el log del primer arranque.
6. **Dominio de la API:** Settings → Networking → *Custom Domain* `api.photosensestudio.com`, puerto **8080**; crear en Cloudflare el CNAME que indica Railway (ver 2).
7. **Token de proyecto** del ambiente `production` (Settings → Tokens): es el `RAILWAY_TOKEN` del environment de GitHub (sección 7). No es el mismo de develop.
8. **Recursos:** la API más el worker de develop caben en Hobby ($5 con $5 de consumo). Producción suma otro par de servicios: espera **$5–8 adicionales** o pasa a Pro si hace falta IP fija o más memoria.

## 7. GitHub (CI/CD)

Hoy existe `.github/workflows/deploy-develop.yml`. Para producción **(crear)**:

1. **`.github/workflows/deploy-production.yml`**, copia de `deploy-develop.yml` con: disparador `push` a `main` (y `workflow_dispatch`), `concurrency.group: deploy-production`, `environment: production` en los tres jobs, `--environment production` en `railway up` y el proyecto de Pages `photosense`. El CI (`ci.yml`) se reutiliza tal cual.
2. **Environment `production`** (Settings → Environments → New): 
   - **Required reviewers:** tú. Cada despliegue espera tu aprobación manual.
   - **Deployment branches:** solo `main`.
   - Secretos: `CLOUDFLARE_API_TOKEN` (permiso *Cloudflare Pages · Edit*), `CLOUDFLARE_ACCOUNT_ID`, `RAILWAY_TOKEN` (el de producción).
   - Variables: `API_URL=https://api.photosensestudio.com`, `PAGES_PROJECT=photosense`, `SITE_URL=https://photosensestudio.com`.
3. **Protección de ramas** en `main` y en `develop`: solo por *pull request*, con el CI obligatorio y una aprobación.
4. **Flujo de ramas:** `feature/*` → `develop` (despliega a develop solo) → *pull request* `develop` → `main` (despliega a producción tras tu aprobación). Nadie empuja directo.
5. Las acciones se mantienen fijadas por SHA (el repositorio es público) y la versión del CLI de Railway fijada (hoy 5.69.0).

## 8. Respaldos y recuperación

| Qué | Cómo | Estado |
|---|---|---|
| Base de datos | Cloud Backup de Atlas (clúster de pago) **o** `mongodump` semanal del worker hacia `photostudio-backups` en R2 | El `mongodump` semanal está **pendiente de construir**; con clúster de pago no es indispensable |
| Fotos | R2 guarda las fotos; no hay copia aparte. Las finales ya entregadas se pueden bajar con ZIP; los RAW **nunca** se suben | Sin cambios |
| Prueba de restauración | Antes de abrir: restaurar un respaldo a un clúster temporal y arrancar la API contra él | **Hacer** una vez |
| Volver atrás un despliegue | Railway → servicio → Deployments → *Redeploy* sobre uno anterior; Pages → Deployments → *Rollback* | Probado en develop |
| Antes de un cambio riesgoso | `mongodump --uri "<cadena>" --db photostudio` | Manual |

## 9. Servicios externos

- **Resend:** el dominio `mail.photosensestudio.com` y sus registros ya existen. Crea una **clave de API nueva solo para producción** con permiso de envío limitado a ese dominio. Pendiente opcional: `Reply-To` y un remitente que no sea `no-reply`.
- **Expo / EAS:** la cuenta es `g.raba29-dev`, el proyecto `@g.raba29-dev/photostudio`. Hace falta el **entorno `production` de EAS** con la variable de archivo `GOOGLE_SERVICES_JSON` **(crear)**: `eas env:create production --name GOOGLE_SERVICES_JSON --type file --value ./google-services.json --visibility sensitive`.
- **Firebase (FCM):** el proyecto `photosense-studio` y la app Android `com.photosensestudio.app` ya existen. Para producción conviene un **proyecto de Firebase aparte** (`photosense-studio-prod`) con su `google-services.json` y su clave de cuenta de servicio FCM V1 subida en expo.dev → Credentials. Es una decisión: con un solo proyecto, develop y producción comparten el transporte del push.
- **Apple (iOS)**, ⚠ decisión 5: cuenta de Apple Developer, APNs y credenciales de iOS en EAS. El `bundleIdentifier` es el mismo y **no se puede cambiar después de publicar**.
- **Google Play**, ⚠ decisión 6: cuenta de desarrollador, ficha de la app, política de privacidad (la app guarda datos de clientes) y un AAB firmado.

## 10. La app móvil

1. **Perfil `production` en `mobile/eas.json` (crear):** `distribution: store` (o `internal` para el APK directo), `android.buildType: "app-bundle"` para Play o `"apk"` para instalar a mano, `environment: "production"`, `autoIncrement: true` y
   ```json
   "env": {
     "EXPO_PUBLIC_API_URL": "https://api.photosensestudio.com",
     "EXPO_PUBLIC_STUDIO_URL": "https://photosensestudio.com/studio"
   }
   ```
   Todo `EXPO_PUBLIC_*` queda dentro del paquete: nunca secretos.
2. **Firma:** EAS genera y guarda el *keystore* de Android la primera vez. **Haz una copia** de las credenciales (`eas credentials`) y guárdala fuera del repositorio: sin el keystore no se pueden publicar actualizaciones de la misma app.
3. **Build:** `cd mobile && npx eas-cli build --platform android --profile production`.
4. **Actualizaciones por aire (OTA):** si las quieres, EAS Update (1 000 usuarios activos al mes gratis). Hoy no está configurado; un cambio de JavaScript exige un build nuevo.
5. **Versión:** `appVersionSource: remote` y `autoIncrement` suben el `versionCode`. La versión visible (`version` de `app.json`) se sube a mano.

## 11. Seguridad: lista de comprobación antes de abrir

- [ ] `JWT_SIGNING_KEY` nueva, de ≥ 32 caracteres, distinta de develop y de desarrollo.
- [ ] `API_DOCS_ENABLED` **no existe** en Railway producción (`/swagger` y `/openapi/v1.json` dan 404).
- [ ] `ASPNETCORE_ENVIRONMENT` no definido.
- [ ] `AUTH_REGISTRATION_ENABLED=false` una vez creada tu cuenta (y `grant-system-role.js` si necesitas operador de sistema; **nunca** por registro).
- [ ] Usuario de Atlas limitado a la base `photostudio`; contraseña única.
- [ ] Token de R2 limitado al bucket; bucket privado, sin dominio público.
- [ ] CORS de R2 solo con `https://photosensestudio.com`.
- [ ] Sitio sin Cloudflare Access (público) pero con las cabeceras de `_headers`.
- [ ] El entorno `production` de GitHub exige aprobación y solo despliega desde `main`.
- [ ] Ningún secreto en el repositorio, en `appsettings` ni en `EXPO_PUBLIC_*`/`VITE_*`.
- [ ] Límites de intentos de inicio de sesión comprobados con la IP real detrás de Cloudflare/Railway (sección 2).
- [ ] Política de privacidad y términos publicados en el sitio (los clientes dejan nombre, teléfono, correo y firma).

## 12. Orden de salida

1. Decisiones de la sección 1.
2. Atlas (3) → R2 (4) → Resend (9) → Railway (6) con **todas las variables**.
3. Primer despliegue **manual** de la API y el worker a producción (`railway up … --environment production`) para ver los logs de arranque; comprobar `https://api.photosensestudio.com/health/ready` = 200.
4. Pages (5) y el workflow de producción (7).
5. Crear tu cuenta desde la app contra la API de producción; verificar el correo; `AUTH_REGISTRATION_ENABLED=false`.
6. **Humo completo de punta a punta**, como en `docs/Deployment.md` §3.5: crear una reserva, abrir el enlace del cliente, recibir el código por correo, firmar el contrato, subir un comprobante, verificarlo, completar la sesión, subir pruebas y finales a R2, que el cliente elija y descargue, el push al teléfono y el cron del worker (la expiración y los avisos).
7. Prueba de restauración del respaldo (8).
8. Build de producción de la app (10) e instalación en tu teléfono.
9. Abrir a los primeros clientes.

## 13. Después de salir

- Revisar los logs de Railway los primeros días (`MongoHealthCheck`, errores 5xx, `email.delivery_failed`, `storage.unavailable`).
- Vigilar la cuota de Resend (100 al día compartida) y el consumo de Railway.
- Ver en Atlas el uso de almacenamiento y de operaciones; subir de plan si se acerca al límite.
- Anotar la fecha del primer respaldo restaurado y la de la última rotación de claves (JWT, Resend, R2).

## 14. Lo que todavía no existe (a construir para producción)

| Pieza | Dónde |
|---|---|
| `deploy-production.yml` y el *environment* `production` con aprobación | GitHub |
| Proyecto de Pages `photosense` y su dominio | Cloudflare |
| Ambiente `production` de Railway y sus dos servicios | Railway |
| Perfil `production` en `mobile/eas.json` y entorno `production` de EAS | Expo |
| Cambios de indexación del sitio (`index.html`, `_headers`, `robots.txt`, `sitemap.xml`) | Repositorio |
| `mongodump` semanal hacia R2 (si no hay Cloud Backup) | Worker |
| Versión visible del commit en `/health/ready` (para que el despliegue distinga una versión rechazada) | API |
| Política de privacidad y términos | Sitio |
| iOS: Apple Developer, APNs y credenciales | Apple / EAS |

Para el detalle de cada servicio: `docs/Deployment.md` (pasos de develop), `docs/manual/A4-Infraestructura.md` (qué se configuró y variables), `docs/PushNotifications.md` §14 (pendientes de push e iOS) y `docs/Authentication.md` (cookies de la web del fotógrafo).
