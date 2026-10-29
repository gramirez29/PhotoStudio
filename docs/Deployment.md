# Despliegue

Cómo se publica PhotoSense Studio. Hoy existe un ambiente, **develop**; producción se agrega después con el mismo patrón.

## 1. Panorama

```
push a develop ─▶ GitHub Actions (deploy-develop.yml)
                    ├─ CI: backend + imagen Docker + mobile + portal      (ci.yml)
                    └─ si pasa, en paralelo:
                         ├─ sitio    ─▶ Cloudflare Pages  (wrangler pages deploy)
                         ├─ API      ─▶ Railway           (railway up --service photostudio-api)    + espera /health/ready
                         └─ worker   ─▶ Railway           (railway up --service photostudio-worker)
```

Es el mismo patrón que `barber.flow.app`: GitHub Actions es quien despliega, solo después de pasar las pruebas, y la rama decide el ambiente. Railway **no** está conectado a GitHub: sus servicios son vacíos y reciben el código que sube el workflow.

| Pieza | Dónde corre | Cómo se despliega |
|---|---|---|
| Sitio (página principal en `/` + portal del cliente en `/b/{token}`) | Cloudflare Pages, proyecto `photosense-develop` | `wrangler pages deploy` desde Actions |
| API | Railway, ambiente `develop`, servicio `photostudio-api` | `railway up` desde Actions |
| Worker (expiración de reservas, recordatorios, outbox) | Railway, mismo ambiente, servicio `photostudio-worker` (cron) | `railway up` desde Actions |
| Base de datos | MongoDB Atlas M0, clúster `photo_studio_dev`, base `photostudio_dev` | — |
| Correo | Resend, dominio `mail.photosensestudio.com` | — |

### Nombres (ambiente develop)

| Qué | Dirección |
|---|---|
| Sitio | `https://develop.photosensestudio.com` |
| API | `https://develop-api.photosensestudio.com` |

Son nombres **de un solo nivel** a propósito: el certificado gratuito de Cloudflare cubre `*.photosensestudio.com` pero no `*.develop.photosensestudio.com`; uno de dos niveles exigiría un certificado de pago.

El sitio de develop se marca con una franja **"Ambiente de pruebas"** (`VITE_ENVIRONMENT=develop`), no se indexa (`robots.txt`, `X-Robots-Tag`) y su contenido principal es provisional.

> Producción (pendiente): `photosensestudio.com` + `api.photosensestudio.com`. `PORTAL_BASE_URL` se define por ambiente, así que no hay que tocar código.

## 2. Qué hay en el repositorio

| Archivo | Para qué |
|---|---|
| `.github/workflows/ci.yml` | Compila y prueba backend (con MongoDB en réplica), construye la imagen de producción, y verifica mobile y portal. Corre en cada pull request y lo reutiliza el despliegue |
| `.github/workflows/deploy-develop.yml` | En cada push a `develop`: CI y, solo si pasa, publica sitio, API y worker |
| `backend/scripts/configure-railway.mjs` | Fija los ajustes de construcción y arranque de los dos servicios de Railway (Dockerfile; API con health check `/health/ready`; worker con su comando y su cron `*/15 * * * *`). Es repetible: compara y solo escribe si algo difiere |
| `mobile/eas.json`, `mobile/app.config.ts` | Perfil de build de desarrollo de EAS (APK interno) y lectura del `google-services.json` de Firebase desde una variable de EAS; ver la sección 7 y `docs/PushNotifications.md` |
| `backend/Dockerfile` | Usa `CMD` (no `ENTRYPOINT`): el worker usa la misma imagen con otro comando |
| `portal/public/_headers`, `_redirects`, `robots.txt` | Cabeceras de seguridad, rutas de la SPA y no indexar |

Las acciones de GitHub están fijadas por commit (`@<sha> # vX.Y.Z`) y el CLI de Railway por versión exacta (`RAILWAY_CLI_VERSION`), porque el repositorio es público y esos pasos corren con credenciales que pueden desplegar.

## 3. Puesta en marcha (en este orden)

### 3.1 MongoDB Atlas
1. Crea la cuenta y un proyecto (`PhotoSense`).
2. **Create cluster → M0 (Free)** con nombre `photo_studio_dev`, proveedor AWS, región **N. Virginia (us-east-1)**, para estar cerca de Railway (US East). El nombre del clúster es solo una etiqueta: la base de datos se llama `photostudio_dev` y se crea con la primera escritura.
3. **Database Access → Add user** `photostudio-develop`, contraseña generada. Rol **personalizado** con `readWrite` solo sobre la base `photostudio_dev` (no `Atlas admin`).
4. **Network Access → Add IP address → `0.0.0.0/0`**. Railway Hobby no tiene IP de salida fija. Es el costo de M0: lo compensan la contraseña larga, TLS (siempre en Atlas) y un usuario limitado a una sola base.
5. **Connect → Drivers** y copia la cadena `mongodb+srv://…`. Pon la contraseña real y **no la guardes en ningún archivo**: va solo en Railway.

> M0: 0,5 GB, sin respaldos de Atlas (solo `mongodump` manual). Para producción habrá otra base (`photostudio`) con otro usuario.

### 3.2 Cloudflare
1. **Account ID:** Workers & Pages → Overview → columna derecha.
2. **Token de API:** Mi perfil → API Tokens → *Create Custom Token* → permiso **Account · Cloudflare Pages · Edit**, limitado a tu cuenta. Cópialo una sola vez.
3. **Proyecto de Pages** (una sola vez, desde tu terminal):
   ```powershell
   cd D:\Repositories\Applications\PhotoStudio\portal
   npx wrangler login
   npx wrangler pages project create photosense-develop --production-branch=develop
   ```
4. **Dominio del sitio:** Workers & Pages → `photosense-develop` → Custom domains → `develop.photosensestudio.com` → *Activate domain*. Como el dominio está en la misma cuenta, Cloudflare crea el CNAME (con proxy).
5. **Dominio de la API:** se crea en 3.3 (necesita el destino que da Railway).

### 3.3 Railway
1. **Proyecto** `photosense`. Crea el ambiente `develop` (Project → Environments → *New Environment*). Todo lo siguiente va en ese ambiente.
2. **Servicio API:** *New → Empty Service* → nómbralo exactamente `photostudio-api`. **No lo conectes a GitHub**: el código lo sube el workflow.
3. **Variables de la API:**

   | Variable | Valor |
   |---|---|
   | `MONGODB_CONNECTION_STRING` | la de Atlas (3.1) |
   | `MONGODB_DATABASE_NAME` | `photostudio_dev` |
   | `JWT_SIGNING_KEY` | una **nueva**, no la de desarrollo: `openssl rand -base64 48` |
   | `RESEND_API_KEY` | clave de Resend (*Sending access*, solo el dominio de correo) |
   | `EMAIL_FROM` | `PhotoSense Studio <no-reply@mail.photosensestudio.com>` |
   | `PORTAL_BASE_URL` | `https://develop.photosensestudio.com` |
   | `EXPO_ACCESS_TOKEN` | **opcional**: solo si el proyecto de Expo activa la seguridad mejorada para push; sin ella el envío funciona |
   | `AUTH_REGISTRATION_ENABLED` | `true` hasta crear tu cuenta; luego `false` |

   No definas `ASPNETCORE_ENVIRONMENT`: por defecto es *Production*, que rechaza la clave de desarrollo y no abre CORS al servidor local de Vite. `PORT` lo pone Railway.

   > **Los símbolos `<` y `>` de los ejemplos son marcadores, no parte del valor.** Si los dejas pegados, la API no arranca (`The connection string '<mongodb+srv://…>' is not valid`) y la clave de Resend o la de JWT quedan mal. El primer despliegue falló por esto.
4. **Dominio de la API:** Settings → Networking → *Custom Domain* → `develop-api.photosensestudio.com`, puerto **8080**. Railway muestra un destino (`xxxx.up.railway.app`): crea en Cloudflare un registro **CNAME** `develop-api` → ese destino, con el proxy **desactivado (nube gris, "DNS only")** para que Railway valide y emita su certificado.
5. **Servicio worker:** *New → Empty Service* → `photostudio-worker`. Sin dominio público y sin health check.
6. **Variables del worker:**

   | Variable | Valor |
   |---|---|
   | `MONGODB_CONNECTION_STRING` | `${{photostudio-api.MONGODB_CONNECTION_STRING}}` |
   | `MONGODB_DATABASE_NAME` | `${{photostudio-api.MONGODB_DATABASE_NAME}}` |
   | `WORKER_RUN_ONCE` | `true` |
   | `EXPO_ACCESS_TOKEN` | **opcional**, la misma que la API: el worker también entrega avisos y sus pushes |

7. **Token del ambiente:** Project settings → **Tokens** → ambiente `develop` → crear → copiarlo. Es un **token de proyecto**: solo sirve para ese ambiente. Cuando exista producción tendrá el suyo, guardado en su propio *environment* de GitHub.
8. **Ajustes de los servicios** (una vez, y de nuevo si cambian). Railway **no aplica** la sección `deploy` de un `railway.json` subido con `railway up` (el servicio del worker arrancó la API), así que esos ajustes se fijan en el servicio con un script. Necesita haber iniciado sesión (`npx @railway/cli@5.69.0 login`) y el ID del proyecto (Project settings → General):
   ```powershell
   node backend/scripts/configure-railway.mjs --project <id-del-proyecto> --environment develop
   ```
   Con `--dry-run` muestra lo que aplicaría sin escribir. Si ya está aplicado responde `Already up to date`.

### 3.4 GitHub
1. **Environment:** Settings → Environments → *New environment* → `develop`.
2. **Secretos del environment** (`gh` pide el valor en la terminal; no lo pegues en un chat ni en un archivo):
   ```bash
   gh secret set CLOUDFLARE_API_TOKEN  --env develop
   gh secret set CLOUDFLARE_ACCOUNT_ID --env develop
   gh secret set RAILWAY_TOKEN         --env develop
   ```
3. **Variables del environment** (son públicas):
   ```bash
   gh variable set API_URL       --env develop --body "https://develop-api.photosensestudio.com"
   gh variable set PAGES_PROJECT --env develop --body "photosense-develop"
   gh variable set SITE_URL      --env develop --body "https://develop.photosensestudio.com"
   ```
4. **Protección de ramas** (Settings → Branches), para `develop` y `main`: exigir pull request y los checks **Backend (build and tests)**, **Backend (production image builds)**, **Mobile (types, lint, tests)** y **Portal (types, lint, tests, build)**.
5. Settings → Actions → General → *Workflow permissions*: **Read repository contents**.

### 3.5 Primer despliegue y verificación
Haz un push (o *Run workflow* en Actions → *Deploy develop*) y comprueba:

- [ ] Actions: CI verde y los tres jobs (**Site**, **API**, **Worker**) terminan bien. El de la API incluye la espera de `/health/ready`.
- [ ] `curl https://develop-api.photosensestudio.com/health/ready` responde `200`.
- [ ] `https://develop.photosensestudio.com` muestra la franja "Ambiente de pruebas" y la página principal.
- [ ] En la app (con `EXPO_PUBLIC_API_URL=https://develop-api.photosensestudio.com` en `mobile/.env`): crear cuenta, crear una reserva con tu correo y "Enviar enlace por WhatsApp".
- [ ] Abre el enlace desde el teléfono **con datos móviles**, pide el código (llega por Resend), sube un comprobante y verifícalo en la app.
- [ ] Railway → worker: el log muestra `Maintenance pass completed` tras la primera hora del cron.
- [ ] Pon `AUTH_REGISTRATION_ENABLED=false` en la API.

## 4. Operación

- **Volver atrás:** Railway → servicio → Deployments → *Redeploy* sobre uno anterior. Pages → Deployments → *Rollback*. O vuelve a ejecutar el workflow desde un commit anterior.
- **Respaldo:** Atlas M0 no tiene. Antes de cambios riesgosos: `mongodump --uri "<cadena>" --db photostudio_dev`.
- **Secretos:** solo en Railway y en el environment de GitHub. Las variables `EXPO_PUBLIC_*` y `VITE_*` terminan dentro del paquete: nunca llevan secretos.
- **Costo:** Atlas M0, Pages y Actions (repositorio público) son gratis. Railway Hobby son 5 USD con 5 USD de consumo incluidos; la API y el worker de develop cuentan contra eso.
- **Si la construcción de Railway falla,** el job falla (`--ci`) y la versión anterior sigue sirviendo. Si construye pero la nueva versión no pasa el health check, Railway tampoco cambia el tráfico; y como la espera de `/health/ready` responde con la versión anterior, **el job puede salir verde aunque el despliegue haya fallado**. Tras cada despliegue conviene mirar Railway → Deployments hasta tener un mecanismo que distinga versiones.

## 5. Lo que se comprobó y lo que no

**Comprobado en el primer despliegue real:**

- `railway up ./backend --path-as-root --service … --environment develop --ci` con un **token de proyecto** (`RAILWAY_TOKEN`) despliega en un servicio vacío: sube, construye con el Dockerfile y termina.
- **Railway no aplica la sección `deploy` de un `railway.json` subido con el CLI.** El worker arrancó el programa de la API (por eso falló con `JWT_SIGNING_KEY is not set`) porque su comando de inicio y su cron no se aplicaron; el health check de la API tampoco. La parte de construcción del archivo sí se aplicó. Por eso los ajustes viven en el servicio (`configure-railway.mjs`) y no hay `railway.json` en el repositorio.
- `railway environment edit --service-config <servicio> <ruta> <valor>` respondió `No changes to apply` y no aplicó nada con la versión 5.69.0; el parche JSON por la entrada estándar sí funciona, y es lo que usa el script.
- Con los ajustes y las variables correctos, la API respondió `200` en `/health/ready` por el dominio de Railway y por `develop-api.photosensestudio.com`.

**Sin comprobar todavía:**

- Que `--ci` haga fallar el job cuando la construcción falla (solo lo vimos con construcciones que terminaron bien).
- Que Pages ofrezca el dominio personalizado sin pasos extra cuando el proyecto se crea con `wrangler`.
- La primera pasada del cron del worker en Railway.
- ~~Un push real a un teléfono (Expo → FCM → Android)~~ Probado el 2026-10-28 en Android. Pendiente iOS (ver `docs/PushNotifications.md` §14).
- Que el despertador de la API (`OutboxWakeUpService`) corra bien en Railway tras el primer despliegue que lo incluya.

Lo que sí está verificado fuera de Railway: la imagen de Docker compila y arranca la API por defecto; los workflows pasan `actionlint`; el sitio compila con `npm run build`.

## 6. Pendiente

- **Producción:** segundo ambiente de Railway con su token, proyecto de Pages `photosense`, `deploy-production.yml` con un *environment* `production` que exija aprobación manual, y los dominios finales.
- **Distinguir versiones en el health check** (por ejemplo, exponer el commit desplegado) para que el job de la API no dé por bueno un despliegue que Railway rechazó.
- **Volver el job de backend a `ubuntu-latest`.** Hoy corre en `ubuntu-22.04` porque MongoDB se niega a arrancar en kernels Linux 6.19 hasta 7.0.13 (SERVER-121912, incompatibilidad con `tcmalloc`; afecta a todas las imágenes de `mongo`, Docker incluido) y `ubuntu-latest` pasó a uno de esos kernels entre las imágenes de runner `20261012` y `20261025`. Síntoma: el paso `Start MongoDB` falla con `container photostudio-mongo is unhealthy` a los 0,5 s y el log de Mongo dice `MongoDB cannot start: Linux kernel versions 6.19 and newer...`. Revisar cuando el runner traiga un kernel 7.0.14 o más nuevo; el paso `MongoDB diagnostics` del workflow imprime esos logs. Lo mismo puede ocurrir en local si WSL2 se actualiza a un kernel de ese rango (hoy: 6.6.114).
- **Contenido de la página principal** (hoy provisional) y si en producción se indexa o no.
- **Reply-To** de los correos y un remitente que no sea `no-reply`.
- **Respaldo semanal** con `mongodump` hacia R2.
- **Producción de la app móvil:** perfil `production` de EAS, keystore y Firebase de producción, Google Play, y las credenciales de iOS (APNs). Detalle en `docs/PushNotifications.md` §14.

## 7. App móvil: build de desarrollo y push (EAS)

El push no funciona en Expo Go: hace falta un *development build* con `expo-dev-client` y `expo-notifications`. La guía completa del push (herramientas, plataformas, decisiones, pruebas y TODO) está en [`PushNotifications.md`](PushNotifications.md); aquí solo el resumen de despliegue.

- **Identificador:** `com.photosensestudio.app` (Android e iOS). No se puede cambiar después de publicar.
- **Proyecto de EAS:** `@g.raba29-dev/photostudio` (`extra.eas.projectId` en `mobile/app.json`). `mobile/eas.json` tiene el perfil `development` (APK interno, `EXPO_PUBLIC_API_URL` apuntando a develop).
- **Android (FCM):** (1) proyecto de Firebase con una app Android de ese paquete; su `google-services.json` **no se versiona** (el repositorio es público): se sube a EAS como variable de archivo `GOOGLE_SERVICES_JSON` (`eas env:create development --name GOOGLE_SERVICES_JSON --type file --value ./google-services.json --visibility sensitive`) y `mobile/app.config.ts` la lee; en local se lee de `mobile/google-services.json` (ignorado por git). (2) La clave de cuenta de servicio de Firebase (FCM V1) se sube a EAS desde expo.dev → Credentials → Android; **nunca** al repositorio ni a un chat.
- **Build:** `cd mobile && npx eas-cli build --platform android --profile development`. Un build de desarrollo no trae el JavaScript: al abrirlo pide un servidor (`npx expo start --dev-client`, misma red Wi-Fi o `--tunnel`); `mobile/.env` define `EXPO_PUBLIC_API_URL`.
- **iOS:** pendiente (exige Apple Developer y credenciales APNs).
