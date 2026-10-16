# Despliegue

Cómo se publica PhotoSense Studio. Hoy existe un ambiente, **develop**; producción se agrega después con el mismo patrón.

## 1. Panorama

```
push a develop ─▶ GitHub Actions (deploy-develop.yml)
                    ├─ CI: backend + imagen Docker + mobile + portal        (ci.yml)
                    └─ si pasa ─▶ construye el sitio y lo publica en Cloudflare Pages
                  Railway (misma rama, "Wait for CI") ─▶ espera los checks de ese run
                    ├─ photostudio-api     (health check /health/ready)
                    └─ photostudio-worker  (cron cada 15 min)
```

| Pieza | Dónde corre | Quién la despliega |
|---|---|---|
| Sitio (página principal en `/` + portal del cliente en `/b/{token}`) | Cloudflare Pages, proyecto `photosense-develop` | GitHub Actions (`wrangler pages deploy`) |
| API | Railway, ambiente `develop`, servicio `photostudio-api` | Railway, desde la rama `develop`, **después** de que pasa el CI |
| Worker (expiración de reservas, recordatorios, outbox) | Railway, mismo ambiente, servicio `photostudio-worker` (cron) | Railway, igual que la API |
| Base de datos | MongoDB Atlas M0, base `photostudio_dev` | — |
| Correo | Resend, dominio `mail.photosensestudio.com` | — |

### Nombres (ambiente develop)

| Qué | Dirección |
|---|---|
| Sitio | `https://develop.photosensestudio.com` |
| API | `https://develop-api.photosensestudio.com` |

Son nombres **de un solo nivel** a propósito: el certificado gratuito de Cloudflare cubre `*.photosensestudio.com` pero no `*.develop.photosensestudio.com`; uno de dos niveles exigiría un certificado de pago.

El sitio de develop se marca con una franja **"Ambiente de pruebas"** (`VITE_ENVIRONMENT=develop`), no se indexa (`robots.txt`, `X-Robots-Tag`) y su contenido principal es provisional.

> Producción (pendiente): `photosensestudio.com` + `api.photosensestudio.com`. Antes se había pensado en `book.photosensestudio.com` para el portal; con un solo sitio ya no hace falta. `PORTAL_BASE_URL` se define por ambiente, así que no hay que tocar código.

## 2. Qué hay en el repositorio

| Archivo | Para qué |
|---|---|
| `.github/workflows/ci.yml` | Compila y prueba backend (con MongoDB en réplica), construye la imagen de producción, y verifica mobile y portal. Corre en cada pull request y lo reutiliza el despliegue |
| `.github/workflows/deploy-develop.yml` | En cada push a `develop`: CI y, solo si pasa, publica el sitio en Pages |
| `backend/railway.json` | Servicio API: construye el `Dockerfile`, health check `/health/ready`, reinicia ante fallos |
| `backend/railway.worker.json` | Servicio worker: comando `dotnet worker/PhotoStudio.Worker.dll`, cron `*/15 * * * *`, sin reinicio |
| `backend/Dockerfile` | Usa `CMD` (no `ENTRYPOINT`): el worker usa la misma imagen con otro comando |
| `portal/public/_headers`, `_redirects`, `robots.txt` | Cabeceras de seguridad, rutas de la SPA y no indexar |

Las acciones de GitHub están fijadas por commit (`@<sha> # vX.Y.Z`) porque el repositorio es público.

## 3. Puesta en marcha (en este orden)

### 3.1 MongoDB Atlas
1. Crea la cuenta y un proyecto (`PhotoSense`).
2. **Create cluster → M0 (Free)**, proveedor AWS, región **N. Virginia (us-east-1)**, para estar cerca de Railway (US East).
3. **Database Access → Add user** `photostudio-develop`, contraseña generada. Rol **personalizado** con `readWrite` solo sobre la base `photostudio_dev` (no `Atlas admin`).
4. **Network Access → Add IP address → `0.0.0.0/0`**. Railway Hobby no tiene IP de salida fija. Es el costo de M0: lo compensan la contraseña larga, TLS (siempre en Atlas) y un usuario limitado a una sola base.
5. **Connect → Drivers** y copia la cadena `mongodb+srv://…`. Pon la contraseña real y **no la guardes en ningún archivo**: va solo en Railway.

> M0: 0,5 GB, sin respaldos de Atlas (solo `mongodump` manual). Para producción habrá otra base (`photostudio`) con otro usuario, en este mismo clúster o en otro.

### 3.2 Cloudflare
1. **Token de API:** Mi perfil → API Tokens → *Create Token* → personalizado → permiso **Account · Cloudflare Pages · Edit**, limitado a tu cuenta. Cópialo una sola vez.
2. **Account ID:** panel de Cloudflare → página principal de la cuenta (columna derecha) o la URL del panel.
3. **Crea el proyecto de Pages** (una sola vez, desde tu terminal):
   ```powershell
   cd D:\Repositories\Applications\PhotoStudio\portal
   $env:CLOUDFLARE_API_TOKEN = "pega-el-token"
   $env:CLOUDFLARE_ACCOUNT_ID = "pega-el-id"
   npx wrangler pages project create photosense-develop --production-branch=develop
   ```
4. **Dominio del sitio:** Workers & Pages → `photosense-develop` → Custom domains → `develop.photosensestudio.com`. Como el dominio está en la misma cuenta, Cloudflare crea el registro DNS por ti.
5. **Dominio de la API:** se crea en 3.3 (necesita el destino que da Railway).

### 3.3 Railway
1. **Proyecto** `photosense` con el repositorio conectado (instala la app de GitHub de Railway en el repo).
2. **Ambiente `develop`:** Project → Environments → *New Environment*. Todo lo siguiente va en ese ambiente.
3. **Servicio API** → *New → GitHub Repo* → este repositorio → nómbralo `photostudio-api`. En Settings:
   - **Source → Branch:** `develop`.
   - **Source → Root Directory:** `backend`.
   - **Config as Code → path:** `/backend/railway.json`.
   - **Wait for CI:** activado.
4. **Variables de la API** (Variables):

   | Variable | Valor |
   |---|---|
   | `MONGODB_CONNECTION_STRING` | la de Atlas (3.1) |
   | `MONGODB_DATABASE_NAME` | `photostudio_dev` |
   | `JWT_SIGNING_KEY` | una **nueva**, no la de desarrollo: `openssl rand -base64 48` |
   | `RESEND_API_KEY` | clave de Resend (*Sending access*, solo el dominio de correo) |
   | `EMAIL_FROM` | `PhotoSense Studio <no-reply@mail.photosensestudio.com>` |
   | `PORTAL_BASE_URL` | `https://develop.photosensestudio.com` |
   | `AUTH_REGISTRATION_ENABLED` | `true` hasta crear tu cuenta; luego `false` |

   No definas `ASPNETCORE_ENVIRONMENT`: por defecto es *Production*, que rechaza la clave de desarrollo y no abre CORS al servidor local de Vite. `PORT` lo pone Railway.
5. **Dominio de la API:** Settings → Networking → *Custom Domain* → `develop-api.photosensestudio.com`, puerto **8080**. Railway muestra un destino (`xxxx.up.railway.app`): crea en Cloudflare un registro **CNAME** `develop-api` → ese destino, con el proxy **desactivado (nube gris, "DNS only")** para que Railway pueda validar y emitir su certificado.
6. **Servicio worker** → *New → GitHub Repo* → mismo repositorio → `photostudio-worker`. Igual que la API (rama `develop`, Root Directory `backend`, **Wait for CI**) pero **Config as Code → path:** `/backend/railway.worker.json`. Sin dominio público ni health check.
7. **Variables del worker:**

   | Variable | Valor |
   |---|---|
   | `MONGODB_CONNECTION_STRING` | `${{photostudio-api.MONGODB_CONNECTION_STRING}}` |
   | `MONGODB_DATABASE_NAME` | `${{photostudio-api.MONGODB_DATABASE_NAME}}` |
   | `WORKER_RUN_ONCE` | `true` |

### 3.4 GitHub
1. **Environment:** Settings → Environments → *New environment* → `develop`.
2. **Secretos del environment** (los valores no los pega nadie en un chat ni en un archivo; `gh` los pide en la terminal):
   ```bash
   gh secret set CLOUDFLARE_API_TOKEN  --env develop
   gh secret set CLOUDFLARE_ACCOUNT_ID --env develop
   ```
3. **Variables del environment** (son públicas):
   ```bash
   gh variable set API_URL       --env develop --body "https://develop-api.photosensestudio.com"
   gh variable set PAGES_PROJECT --env develop --body "photosense-develop"
   gh variable set SITE_URL      --env develop --body "https://develop.photosensestudio.com"
   ```
4. **Protección de ramas** (Settings → Branches), para `develop` y `main`: exigir pull request y los checks **Backend (build and tests)**, **Backend (production image builds)**, **Mobile (types, lint, tests)** y **Portal (types, lint, tests, build)**.
5. Settings → Actions → General → *Workflow permissions*: **Read repository contents** (los workflows ya piden solo lo que usan).

### 3.5 Primer despliegue y verificación
Haz un push (o *Run workflow* en la pestaña Actions → *Deploy develop*) y comprueba:

- [ ] Actions: CI verde y el job **Site to Cloudflare Pages** publicado.
- [ ] Railway: la API y el worker se despliegan **después** del CI, y la API pasa el health check.
- [ ] `curl https://develop-api.photosensestudio.com/health/ready` responde `200`.
- [ ] `https://develop.photosensestudio.com` muestra la franja "Ambiente de pruebas" y la página principal.
- [ ] En la app (con `EXPO_PUBLIC_API_URL=https://develop-api.photosensestudio.com` en `mobile/.env`): crear cuenta, crear una reserva con tu correo y "Enviar enlace por WhatsApp".
- [ ] Abre el enlace desde el teléfono **con datos móviles**, pide el código (llega por Resend), sube un comprobante y verifícalo en la app.
- [ ] Railway → worker: el log muestra `Maintenance pass completed` tras la primera hora del cron.
- [ ] Pon `AUTH_REGISTRATION_ENABLED=false` en la API.

## 4. Operación

- **Volver atrás:** Railway → servicio → Deployments → *Redeploy* sobre uno anterior. Pages → Deployments → *Rollback* (o vuelve a ejecutar el workflow desde un commit anterior).
- **Respaldo:** Atlas M0 no tiene. Antes de cambios riesgosos: `mongodump --uri "<cadena>" --db photostudio_dev`.
- **Secretos:** solo en Railway y en el environment de GitHub. Las variables `EXPO_PUBLIC_*` y `VITE_*` terminan dentro del paquete: nunca llevan secretos.
- **Costo:** Atlas M0, Pages y Actions (repositorio público) son gratis. Railway Hobby son 5 USD con 5 USD de consumo incluidos; la API y el worker de develop cuentan contra eso.

## 5. Lo que no pude verificar desde aquí
Los paneles de Railway y Cloudflare no son accesibles para mí, así que estos puntos están escritos de la documentación y de memoria, y conviene confirmarlos al hacerlos:

- Que `watchPatterns` (`/backend/**`) se interprete desde la raíz del repositorio con *Root Directory* `backend`. Si ves que se despliega al cambiar solo `portal/`, ajusta el patrón en los dos `railway*.json`.
- Que el campo se llame **Config as Code** y que **Wait for CI** exista en tu plan; los nombres de los campos cambian con las versiones del panel.
- Que Pages ofrezca el dominio personalizado sin pasos extra cuando el proyecto se crea con `wrangler`.

Lo que sí verifiqué: la imagen de Docker compila y arranca la API por defecto; los workflows pasan `actionlint`; el sitio compila con `npm run build`.

## 6. Pendiente

- **Producción:** segundo ambiente de Railway, proyecto de Pages `photosense`, `deploy-production.yml` con un *environment* `production` que exija aprobación manual, y la decisión de los dominios finales.
- **Contenido de la página principal** (hoy provisional) y si en producción se indexa o no.
- **Reply-To** de los correos y un remitente que no sea `no-reply`.
- **Respaldo semanal** con `mongodump` hacia R2 (CLAUDE.md §12).
