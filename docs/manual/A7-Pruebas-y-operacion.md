# A7 — Pruebas y operación

## 1. Correr todo en local

Requisitos: .NET SDK 10.0.1xx o superior, Node 20+, Docker.

### Backend
```
cd backend
docker compose up -d --wait          # MongoDB (réplica de 1 nodo) en 127.0.0.1:27019 y almacenamiento S3 local
dotnet restore && dotnet build && dotnet test
dotnet run --project src/PhotoStudio.Api --launch-profile PhotoStudio.Api   # http://localhost:8080
```
- Mongo local usa el puerto **27019** (27017 y 27018 suelen estar ocupados). Cadena: `mongodb://localhost:27019/?directConnection=true`; `directConnection=true` es obligatorio porque la réplica se anuncia como `localhost:27017`. Sin autenticación: solo desarrollo.
- `launchSettings.json` trae una `JWT_SIGNING_KEY` `dev-only`. En local no hace falta definir nada: la cuenta se crea desde la app.
- Alternativa: `docker compose --profile api up -d --build` levanta la API y Mongo en contenedores.
- Documentación interactiva: `http://localhost:8080/swagger` (solo en Development).

### App
```
cd mobile
npm install
cp .env.example .env     # EXPO_PUBLIC_API_URL
npm run verify           # tsc + eslint + jest
npx expo start --clear
```

### Sitio
```
cd portal
npm install
npm run dev              # http://localhost:5173
npm test                 # vitest
```

## 2. Qué cubren las pruebas

| Área | Dónde | Ejemplos |
|---|---|---|
| Reglas del dominio | `backend/tests/PhotoStudio.Domain.UnitTests` | Cada transición B1–B13 y G1–G8, guardas, políticas, `allowedActions` |
| Casos de uso | `...Application.UnitTests` | Manejadores con puertos sustituidos; archivado; mantenimiento |
| Persistencia | `...Infrastructure.IntegrationTests` | Doble reserva imposible, concurrencia optimista, outbox en la misma transacción, índices |
| API completa | `...Api.IntegrationTests` | Login, tokens inválidos, aislamiento entre fotógrafos, portal, galería contra R2 real que **verifica las firmas** |
| App | `mobile/src/**/__tests__` | Validación, formatos, hooks, componentes, tema claro y oscuro |
| Sitio | `portal/src/**/*.test.*` | Renderizado de fotos, subidas, portal, web del fotógrafo |

El CI (`ci.yml`) corre todo en cada PR. Una advertencia de compilación rompe el CI.

### Correr la suite en contenedor (Windows)
Smart App Control puede bloquear el DLL recién compilado. Se copia el backend sin `bin/obj` a una carpeta temporal y se corre en el SDK oficial:

```
docker run --rm --network container:photostudio-storage -v <copia>:/src -w /src/backend \
  -e STORAGE_TEST_ENDPOINT=http://127.0.0.1:8333 \
  -e "MONGODB_TEST_CONNECTION_STRING=mongodb://host.docker.internal:27019/?directConnection=true" \
  mcr.microsoft.com/dotnet/sdk:10.0 dotnet test PhotoStudio.slnx
```
(En Git Bash anteponer `MSYS_NO_PATHCONV=1`.)

### Notas de pruebas del sitio
Node puede exponer un `localStorage` experimental sin métodos que tapa el de jsdom; `src/test/setup.ts` instala uno en memoria si falta. Algunas pruebas de flujo largo pueden agotar los 5 s si la máquina está cargada; al correrlas solas pasan.

## 3. Operación diaria

### Qué mirar
- **`/health/ready`** de la API debe dar 200 (incluye MongoDB).
- **Outbox:** en Mongo, `outbox_messages` con `status` `Failed` son eventos que se rindieron tras 8 intentos y necesitan revisión manual.
- **Worker:** el cron de Railway debe correr cada 15 minutos y terminar con código 0 (1 si Mongo no responde).
- **Correo:** si falta la clave de Resend, los códigos no llegan (503 `email.delivery_failed`).
- **Espacio:** R2 y Atlas M0 (0,5 GB). Archivar galerías libera R2.

### Acciones de mantenimiento
- **Pasada a demanda:** en la app, Ajustes → "Liberar reservas vencidas" (solo operador de sistema), o `POST /api/maintenance/run`.
- **Dar el rol de sistema:** `node backend/scripts/grant-system-role.js` (nunca por registro).
- **Migrar reservas de un tenant a un usuario:** `node backend/scripts/migrate-tenant.js` (se usó con las reservas de desarrollo).
- **Reconfigurar Railway:** `node backend/scripts/configure-railway.mjs --project <id> --environment develop`.

### Respaldo
Atlas M0 **no tiene backups**. Pendiente: job semanal en el worker que haga `mongodump` hacia R2. Mientras tanto, hacerlo a mano antes de cambios grandes.

## 4. Problemas comunes

| Síntoma | Causa probable | Qué hacer |
|---|---|---|
| La API no arranca fuera de Development | Falta `PORTAL_BASE_URL` o `JWT_SIGNING_KEY` (o tiene `dev-only`) | Definirlas |
| Arranque falla con `PORTAL_SESSION_DAYS` | Valor fuera de 1–90 o no entero | Corregirlo |
| Arranque falla con `R2_*` | Las definiste en parte | Todas o ninguna |
| Todo lo de fotos responde 503 | Faltan las `R2_*` | Definirlas en API y worker |
| No llegan códigos ni correos | Faltan `RESEND_API_KEY`/`EMAIL_FROM`, o el dominio no está verificado | Revisar Railway y Resend |
| Los avisos tardan | El cron del worker no corre, o el outbox tiene mensajes `Failed` | Revisar Railway y `outbox_messages` |
| El archivado automático no corre | El worker no tiene las `R2_*` | Copiarlas al worker |
| El push no llega | App en Expo Go o emulador, permiso denegado, o falta la clave FCM | Usar el build de desarrollo en un teléfono físico; revisar credenciales en expo.dev |
| El teléfono no conecta con la API local | La IP de la computadora cambió | `ipconfig` y actualizar `EXPO_PUBLIC_API_URL`; revisar el firewall |
| Subir fotos desde el navegador falla con error de red | CORS del bucket de R2 | Permitir `PUT`/`GET`/`HEAD` y `Content-Type` desde el origen del sitio |
| La suite de API falla en Windows sin error de código | Smart App Control | Correr en contenedor |
| Un cambio de JavaScript no aparece en el teléfono | Metro con caché | `npx expo start --clear` (antes `npx tsc --noEmit`) |
| Falla un build de EAS tras añadir una dependencia | Falta rebuild o versión incompatible | Usar `npx expo install` y rehacer el build |

## 5. Reglas de trabajo en el repositorio
- Todo cambio entra por *pull request* hacia `develop`; nadie empuja directo a `develop` ni a `main`.
- No se fusiona sin revisión del dueño.
- No se instalan SDKs ni se modifica el sistema sin permiso.
- El repositorio es público: ni secretos ni el `CLAUDE.md` (privado, ignorado por git).
- Commits y PR llevan la atribución del asistente cuando lo escribe él.
