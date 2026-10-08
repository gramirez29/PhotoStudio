# Autenticación del fotógrafo (JWT propio)

Documento de referencia de cómo el fotógrafo inicia sesión y cómo la API sabe quién es, para que alguien que no vio el código entienda el diseño, dónde vive cada pieza y cómo se opera.

- **Rama de origen:** `feature/photographer-auth`
- **Alcance:** backend (.NET 10 + MongoDB) y app móvil (Expo). El portal del cliente (token de enlace + OTP) **no** está incluido: es otro mecanismo.
- **Decisiones del producto:** cuenta sembrada desde variables de entorno (sin registro público) y login con email + contraseña.

---

## Índice

1. [Qué problema resuelve](#1-qué-problema-resuelve)
2. [Vista general](#2-vista-general)
3. [Tokens: access y refresh](#3-tokens-access-y-refresh)
4. [Mapa de archivos](#4-mapa-de-archivos)
5. [Backend: login](#5-backend-login)
6. [Backend: renovación (refresh) y rotación](#6-backend-renovación-refresh-y-rotación)
7. [Backend: logout](#7-backend-logout)
8. [Backend: validar cada petición y aislar fotógrafos](#8-backend-validar-cada-petición-y-aislar-fotógrafos)
9. [Backend: la cuenta sembrada](#9-backend-la-cuenta-sembrada)
10. [Persistencia en MongoDB](#10-persistencia-en-mongodb)
11. [App móvil](#11-app-móvil)
12. [Configuración y variables de entorno](#12-configuración-y-variables-de-entorno)
13. [Puesta en marcha en local](#13-puesta-en-marcha-en-local)
14. [Despliegue en Railway](#14-despliegue-en-railway)
15. [Contrato de la API](#15-contrato-de-la-api)
16. [Amenazas consideradas](#16-amenazas-consideradas)
17. [Pruebas](#17-pruebas)
18. [Límites conocidos y siguientes pasos](#18-límites-conocidos-y-siguientes-pasos)

---

## 1. Qué problema resuelve

Antes de este cambio la API estaba abierta: cualquiera que conociera la URL podía leer o modificar reservas, y el `PhotographerId` (el "tenant" que separa los datos de cada fotógrafo) lo **decía el cliente** en el cuerpo de la petición o en la URL. Dos consecuencias graves:

1. No había forma de saber quién hace una petición.
2. Aunque se quisiera aislar a los fotógrafos, bastaba cambiar el `photographerId` para ver o tocar datos de otro, y cualquier reserva era accesible con solo conocer su identificador.

Ahora:

- Toda petición a `/api/**` (salvo el inicio de sesión) exige un **token firmado** emitido por la propia API.
- El `PhotographerId` sale **únicamente del token**. Lo que el cliente mande en el cuerpo o en la URL se ignora.
- Cada operación sobre una reserva verifica que la reserva pertenece al fotógrafo del token. Si no, la API responde **404**, exactamente igual que si la reserva no existiera.

---

## 2. Vista general

```
   App móvil                                    API (.NET)                         MongoDB
 ┌───────────┐  POST /api/auth/login        ┌─────────────────┐
 │  Login    │ ───────────────────────────▶ │ LoginHandler    │ ─ cuenta por email ─▶ photographer_accounts
 │ (email +  │ ◀─ accessToken + refreshToken│  verifica hash  │ ─ guarda el hash ───▶ refresh_tokens
 │ password) │    (JWT 15 min / opaco 30 d) └─────────────────┘
 └─────┬─────┘
       │ guarda el refreshToken en expo-secure-store (Keychain / Keystore)
       │ guarda el accessToken solo en memoria
       ▼
 ┌───────────┐  GET /api/bookings           ┌─────────────────┐
 │ cualquier │  Authorization: Bearer <JWT> │ Middleware JWT  │  valida firma, emisor, audiencia,
 │ petición  │ ───────────────────────────▶ │ + Authorization │  algoritmo y vencimiento
 └─────┬─────┘                              └────────┬────────┘
       │                                             │ claim "sub" = PhotographerId
       │ 401 (token vencido)                         ▼
       │                                    ┌─────────────────┐
       │  POST /api/auth/refresh            │ Handler         │  GetOwnedAsync(id, photographerId)
       └──────────────────────────────────▶ │ (con tenant)    │  → 404 si la reserva es de otro
          { refreshToken }                  └─────────────────┘
          ◀─ nuevo accessToken + NUEVO refreshToken (el anterior queda revocado)
```

Dos tokens con dos funciones:

| | Access token | Refresh token |
|---|---|---|
| Formato | JWT firmado (HS256) | 32 bytes aleatorios (base64url), opaco |
| Vida | **15 minutos** | **30 días**, pero cada uso lo reemplaza por uno nuevo |
| Para qué sirve | Autorizar cada petición | Obtener un access token nuevo sin pedir la contraseña |
| Dónde vive en el servidor | En ningún lado (se valida con la firma) | En Mongo, **solo su hash** SHA-256 |
| Dónde vive en la app | **Solo en memoria** | `expo-secure-store` (Keychain en iOS, Keystore en Android) |
| Si se filtra | Sirve hasta 15 minutos | Se detecta al usarse dos veces (ver §6) |

---

## 3. Tokens: access y refresh

### 3.1 Access token (JWT)

Emitido por `JwtAccessTokenIssuer`. Claims:

| Claim | Valor | Uso |
|---|---|---|
| `sub` | `PhotographerId` (GUID) | **Es el tenant de toda consulta.** |
| `email` | email del fotógrafo | Informativo. |
| `jti` | GUID único | Identifica el token. |
| `iss` / `aud` | `photostudio-api` / `photostudio-app` (configurables) | La API rechaza tokens de otro emisor o audiencia. |
| `iat` / `nbf` / `exp` | emisión / no antes de / vencimiento | Vida de 15 min (configurable). |

No lleva roles ni permisos: hoy un fotógrafo solo puede hacer cosas de fotógrafo sobre sus propios datos.

Validación (en `PhotographerAuthenticationExtensions`): firma con la clave del servidor, `iss`, `aud`, vencimiento (tolerancia de 30 s), `RequireSignedTokens`, y **`ValidAlgorithms = [HS256]`**, de modo que un token con `alg: none` o con otro algoritmo se rechaza. `MapInboundClaims = false` mantiene los nombres de claim tal cual (`sub`, no el URI largo de WS-Federation).

### 3.2 Refresh token

Generado por `RefreshTokenGenerator`: 256 bits del generador aleatorio del sistema, en base64url. **Lo que se guarda en Mongo es `SHA-256(secreto)`**, nunca el secreto: si alguien lee la base de datos no obtiene tokens utilizables. Un hash rápido es suficiente aquí (a diferencia de una contraseña) porque el secreto ya tiene entropía completa y no hay nada que adivinar por fuerza bruta.

Cada login crea una **familia** (`familyId`). Cada renovación revoca el token usado y emite uno nuevo **de la misma familia**. La familia es la unidad de revocación: cerrar sesión o detectar un robo revoca solo esa familia, así que cerrar sesión en el teléfono no cierra la de la tablet.

---

## 4. Mapa de archivos

Rutas relativas a `backend/` (API) y `mobile/` (app).

### Backend: Domain

| Archivo | Rol |
|---|---|
| `src/PhotoStudio.Domain/Identity/PhotographerAccount.cs` | Agregado de la cuenta: email normalizado, hash, contador de fallos y bloqueo. Constantes `MaxFailedLoginAttempts = 5` y `LockoutDuration = 15 min`. |
| `src/PhotoStudio.Domain/Identity/RefreshToken.cs` | Refresh token (solo su hash), familia, vencimiento, revocación. |
| `src/PhotoStudio.Domain/Common/DomainErrorCodes.cs` | Códigos nuevos `account.invalid_email` y `account.weak_password`. |

### Backend: Application

| Archivo | Rol |
|---|---|
| `Abstractions/IPhotographerAccountRepository.cs`, `IRefreshTokenRepository.cs` | Puertos de persistencia. |
| `Abstractions/IPasswordHasher.cs`, `IAccessTokenIssuer.cs`, `IRefreshTokenGenerator.cs` | Puertos de criptografía y emisión (los implementa Infrastructure). |
| `Identity/Login/LoginHandler.cs` | Caso de uso de login. |
| `Identity/RefreshSession/RefreshSessionHandler.cs` | Renovación con rotación y detección de robo. |
| `Identity/Logout/LogoutHandler.cs` | Cierre de sesión. |
| `Identity/EnsureAccount/EnsurePhotographerAccountHandler.cs` | Crea o actualiza la cuenta sembrada. |
| `Identity/SessionIssuer.cs`, `AuthSessionSettings.cs`, `AuthSessionResponse.cs` | Arma los tokens de una sesión; vida del refresh token; respuesta. |
| `Exceptions/AuthenticationFailedException.cs`, `AccountLockedException.cs` | Errores de autenticación (401 y 429). |
| `Bookings/BookingRepositoryExtensions.cs` | `GetOwnedAsync`: carga una reserva **solo si es del fotógrafo**. |
| `DependencyInjection.cs` | `AddApplicationAuthentication()`. |

### Backend: Infrastructure

| Archivo | Rol |
|---|---|
| `Identity/AspNetPasswordHasher.cs` | Hash de contraseñas (PBKDF2 del hasher de ASP.NET Core). |
| `Identity/RefreshTokenGenerator.cs` | Secreto aleatorio y su hash SHA-256. |
| `Identity/JwtOptions.cs` | Lee y valida las variables `JWT_*`. |
| `Identity/JwtAccessTokenIssuer.cs` | Emite el JWT. |
| `Identity/PhotographerAuthenticationExtensions.cs` | Configura la validación JWT, la política por defecto (todo protegido) y el sembrado. |
| `Identity/PhotographerAccountSeeder.cs` | Servicio de arranque que crea la cuenta desde las variables `SEED_*`. |
| `Persistence/MongoPhotographerAccountRepository.cs`, `MongoRefreshTokenRepository.cs` | Repositorios Mongo. |
| `Persistence/Documents/IdentityDocuments.cs`, `IdentityDocumentMappings.cs` | Documentos y mapeos. |
| `Persistence/MongoIndexInitializer.cs` | Índices (email único, hash único, TTL, etc.). |

### Backend: Api

| Archivo | Rol |
|---|---|
| `Endpoints/AuthEndpoints.cs`, `AuthRequests.cs` | `/api/auth/login`, `/refresh`, `/logout`. |
| `Auth/ClaimsPrincipalExtensions.cs` | `GetPhotographerId()`: el tenant sale del claim `sub`. |
| `Endpoints/BookingEndpoints.cs`, `BookingRequests.cs` | Todos los endpoints pasan el fotógrafo del token. |
| `RateLimiting/RateLimitingExtensions.cs` | Límite de 10 por minuto por IP para `/api/auth/*`. |
| `Errors/GlobalExceptionHandler.cs` | 401 y 429 con `code` estable. |
| `Program.cs` | Composición: `UseForwardedHeaders`, `UseAuthentication`, `UseAuthorization`. |

### App móvil (`mobile/src/`)

| Archivo | Rol |
|---|---|
| `session/sessionStore.ts` | Estado de la sesión (Zustand): `restoring`, `offline`, `signedOut`, `signedIn`. |
| `session/sessionManager.ts` | Login, restaurar, renovar (una sola a la vez), cerrar sesión. |
| `storage/refreshTokenStorage.ts` | Guarda el refresh token en `expo-secure-store`. |
| `api/authApi.ts`, `api/authClient.ts` | Llamadas a `/api/auth/*` con un cliente **sin** manejo de sesión. |
| `api/httpClient.ts` | Adjunta el token y, ante un 401, renueva y reintenta **una vez**. |
| `api/client.ts` | Cliente con sesión para el resto de la API. |
| `app/_layout.tsx`, `app/login.tsx` | Navegación protegida y pantalla de login. |
| `components/LoginForm.tsx`, `SignOutButton.tsx`; `hooks/useLoginForm.ts`; `forms/loginForm.ts` | Formulario, botón "Salir", estado y validación. |

---

## 5. Backend: login

`POST /api/auth/login` con `{ "email", "password" }`. Lo ejecuta `LoginHandler`:

```
1. email = TryNormalizeEmail(command.Email)            // recorta y pasa a minúsculas; null si no parece un email
2. account = email == null ? null : GetByEmailAsync(email)
3. si account == null:
       SpendVerificationTime(password)                 // gasta el mismo tiempo que una verificación real
       → 401 auth.invalid_credentials
4. si account está bloqueada ahora:
       → 429 auth.account_locked  (+ Retry-After)      // sin verificar la contraseña
5. si Verify(hash, password) falla:
       intentos = RegisterFailedLoginAsync(id)         // $inc atómico en Mongo
       si intentos >= 5: LockAsync(id, ahora + 15 min) // además reinicia el contador
       → 401 auth.invalid_credentials
6. si había fallos o bloqueo previos: ResetFailedLoginsAsync(id)
7. sesión = nueva familia + access token + refresh token; guarda el hash del refresh token
8. → 200 con la sesión
```

Decisiones de seguridad:

- **No se revela si el email existe.** Email inexistente, email con formato inválido y contraseña incorrecta responden con el mismo estado, el mismo `code` y el mismo `detail`. Además, el caso "email desconocido" ejecuta una verificación de hash señuelo (`SpendVerificationTime`) para que el **tiempo de respuesta** tampoco lo delate.
- **El contador de fallos es atómico.** Si se cargara la cuenta, se incrementara y se guardara con concurrencia optimista, un atacante que enviara 100 intentos en paralelo haría que casi todas las escrituras chocaran y el contador apenas avanzara, burlando el bloqueo. Con `$inc` cada intento cuenta (hay una prueba de integración con 25 intentos paralelos que exige que el contador llegue a exactamente 25).
- **Bloqueo temporal, no permanente:** 5 fallos seguidos bloquean 15 minutos. Contrapartida conocida: un atacante que sepa el email puede mantener bloqueado al dueño (ver §18).
- **Cada login crea su propia familia** de refresh tokens.

Respuesta (`AuthSessionResponse`):

```json
{
  "accessToken": "eyJhbGciOi...",
  "accessTokenExpiresAt": "2026-10-08T21:15:00+00:00",
  "refreshToken": "q3Vf...",
  "refreshTokenExpiresAt": "2026-11-07T21:00:00+00:00",
  "photographerId": "0197a000-0000-7000-8000-000000000001",
  "email": "ana@example.com"
}
```

---

## 6. Backend: renovación (refresh) y rotación

`POST /api/auth/refresh` con `{ "refreshToken" }`. Lo ejecuta `RefreshSessionHandler`:

```
1. hash = SHA-256(refreshToken); token = GetByHashAsync(hash)
2. si no existe o el secreto está vacío          → 401 auth.invalid_refresh_token
3. si token.IsRevoked:                            // alguien lo usa por SEGUNDA vez
       RevokeFamilyAsync(token.FamilyId)          // se mata toda la familia
       → 401
4. si token vencido                               → 401
5. si la cuenta ya no existe: revoca la familia   → 401
6. nueva = access token + refresh token (misma familia)
7. RotateAsync(actual, nueva)                     // atómico, ver abajo
       si devuelve false (otro lo rotó antes): revoca la familia → 401
8. → 200 con la sesión nueva
```

### 6.1 Rotación atómica

`MongoRefreshTokenRepository.RotateAsync` hace, **dentro de una transacción**:

1. `UpdateOne` sobre el token actual con filtro `{ _id, revokedAt: null }` que fija `revokedAt` y `replacedById`.
2. Si no modificó nada (`ModifiedCount == 0`), el token ya estaba usado: devuelve `false` sin insertar nada.
3. Si lo modificó, inserta el token de reemplazo.

Como el filtro exige `revokedAt: null`, de varias renovaciones simultáneas del mismo token **exactamente una** gana (prueba de integración con 8 en paralelo: una gana y se guarda un solo reemplazo). Y como todo va en una transacción, un fallo a mitad no deja al usuario sin token válido.

### 6.2 Por qué reusar un token revoca toda la familia

Un refresh token legítimo se usa una sola vez: la app lo cambia por uno nuevo y descarta el viejo. Si el servidor ve un token ya usado, solo hay dos explicaciones: alguien lo copió (robo) o hay un fallo del cliente. En ambos casos lo seguro es **invalidar toda la familia**: el ladrón y el dueño pierden la sesión y el dueño tiene que iniciar sesión de nuevo. Es el comportamiento recomendado por la guía de seguridad de OAuth para refresh tokens públicos/móviles.

Consecuencia para el cliente: **no puede haber dos renovaciones en paralelo con el mismo token**, porque la segunda parecería un robo. Por eso la app serializa las renovaciones (§11.3).

---

## 7. Backend: logout

`POST /api/auth/logout` con `{ "refreshToken" }`. `LogoutHandler` busca el token por hash y revoca **su familia**. Siempre responde **204**, exista o no el token, para no revelar qué tokens son válidos. Es idempotente.

El access token vigente sigue siendo válido hasta que venza (máximo 15 min): los JWT no se pueden revocar sin consultar la base en cada petición, y el diseño prefiere la vida corta.

---

## 8. Backend: validar cada petición y aislar fotógrafos

### 8.1 Seguro por defecto

`PhotographerAuthenticationExtensions` define una **política de respaldo** (`FallbackPolicy = RequireAuthenticatedUser`). Significa que un endpoint nuevo es **privado por defecto**: para hacerlo público hay que decir `AllowAnonymous()` explícitamente. Hoy son públicos únicamente:

| Endpoint | Por qué |
|---|---|
| `POST /api/auth/login`, `/refresh`, `/logout` | Son los que crean y cierran la sesión (con límite de 10 por minuto por IP). |
| `GET /health/live`, `/health/ready` | Railway los consulta sin credenciales. |
| `GET /openapi/v1.json` | Fuente de los tipos TypeScript. |

Todo lo demás (`/api/bookings/**`, `/api/maintenance/run`) devuelve **401** sin token válido, con la cabecera `WWW-Authenticate: Bearer`.

### 8.2 El tenant sale solo del token

`ClaimsPrincipalExtensions.GetPhotographerId()` lee el claim `sub`. Los endpoints lo pasan a los comandos:

```csharp
private static async Task<Ok<BookingResponse>> CancelAsync(
    Guid id, ClaimsPrincipal user, CancelBookingRequest request, ICommandHandler<CancelBookingCommand, BookingResponse> handler, CancellationToken ct) =>
    TypedResults.Ok(await handler.HandleAsync(request.ToCommand(user.GetPhotographerId(), id), ct));
```

`CreateBookingRequest` ya **no tiene** `PhotographerId`, y `GET /api/bookings` ya no recibe `?photographerId=`. Si un cliente malicioso los envía igual, se ignoran (hay pruebas de integración para ambos casos).

### 8.3 Cada operación verifica la propiedad

Todos los comandos y consultas de reservas llevan ahora `PhotographerId` como primer parámetro, y sus handlers cargan la reserva con:

```csharp
var booking = await repository.GetOwnedAsync(command.BookingId, command.PhotographerId, cancellationToken);
```

`GetOwnedAsync` lanza `NotFoundException` si la reserva no existe **o si es de otro fotógrafo**, así que ambos casos responden **404 idéntico**: la API no confirma que un identificador ajeno exista. Cubre los 8 handlers: obtener, cancelar, completar, marcar ausente, revertir ausencia, reprogramar, firmar contrato y registrar pago. Está probado a nivel unitario (`BookingTenantIsolationTests`) y de punta a punta (`TenantIsolationApiTests`).

> El endpoint `POST /api/maintenance/run` exige sesión pero ejecuta trabajo **global** (expira reservas de todos los fotógrafos). Con un solo fotógrafo es irrelevante; cuando haya varios, restringirlo a un rol de sistema.

---

## 9. Backend: la cuenta sembrada

No hay registro público. La cuenta se crea al arrancar la API con tres variables de entorno; `PhotographerAccountSeeder` (un `BackgroundService`) ejecuta `EnsurePhotographerAccountHandler`:

| Variable | Obligatoria | Descripción |
|---|---|---|
| `SEED_PHOTOGRAPHER_EMAIL` | Sí (para sembrar) | Email de acceso. |
| `SEED_PHOTOGRAPHER_PASSWORD` | Sí (para sembrar) | Contraseña, **mínimo 10 caracteres**. |
| `SEED_PHOTOGRAPHER_ID` | No | GUID del fotógrafo. **Conserva el tenant de los datos existentes**: las reservas se guardan con ese `PhotographerId`. Si falta, se genera uno nuevo. |

Comportamiento:

| Situación | Resultado |
|---|---|
| No existe cuenta con ese email | **Se crea** con el `SEED_PHOTOGRAPHER_ID` dado. |
| Existe y la contraseña coincide | No hace nada (`Unchanged`). |
| Existe y la contraseña es distinta | **Reemplaza la contraseña** y **revoca todas las sesiones** (`PasswordUpdated`). |
| Existe con otro `PhotographerId` que el configurado | Conserva el existente y escribe un aviso en el log (las reservas guardadas bajo el id configurado no serían alcanzables). |
| Faltan las variables | Registra un aviso y no hace nada (la API arranca igual). |
| Mongo no responde al arrancar | Reintenta 5 veces cada 5 s; nunca impide que la API arranque. |
| Contraseña muy corta o email inválido | Error en el log; no crea la cuenta. |

**Mientras las variables estén definidas, el entorno manda sobre la contraseña.** Esa es también la vía de **recuperación** si olvidas la contraseña: cambias `SEED_PHOTOGRAPHER_PASSWORD`, reinicias, y todas las sesiones anteriores quedan revocadas. Cuando exista cambio de contraseña desde la app, habrá que quitar estas variables para que no la pisen.

---

## 10. Persistencia en MongoDB

### `photographer_accounts`

| Campo | Significado |
|---|---|
| `_id` | `PhotographerId` (GUID). Es el tenant de todas las demás colecciones. |
| `version` | Versión (se incrementa al cambiar la contraseña). |
| `email` | Normalizado (minúsculas, sin espacios). **Índice único `ix_account_email`**. |
| `passwordHash` | Formato del hasher de ASP.NET Core (incluye sal y parámetros). |
| `createdAt`, `failedLoginAttempts`, `lockedUntil` | Alta, fallos seguidos y fin del bloqueo. |

### `refresh_tokens`

| Campo | Significado |
|---|---|
| `_id`, `photographerId`, `familyId` | Identificador, dueño y familia de login. |
| `tokenHash` | SHA-256 del secreto. **Índice único `ix_refresh_hash`**. |
| `createdAt`, `expiresAt` | Emisión y vencimiento. |
| `revokedAt`, `replacedById` | Cuándo se usó/revocó y qué token lo reemplazó. |

Índices: `ix_refresh_hash` (único), `ix_refresh_family` y `ix_refresh_photographer` (revocar una familia o todas las sesiones de un fotógrafo), y `ix_refresh_retention` (TTL: Mongo borra los tokens 7 días después de vencer; se conservan ese tiempo para reconocer un token vencido que se reenvía).

---

## 11. App móvil

### 11.1 Estados de la sesión

`sessionStore.ts` (Zustand) guarda `status` y, si hay sesión, el access token, su vencimiento, el `photographerId` y el email:

| Estado | Significado | Qué se ve |
|---|---|---|
| `restoring` | Buscando una sesión guardada al abrir la app | Indicador de carga |
| `offline` | Hay sesión guardada pero no se pudo contactar al servidor para renovarla | "No se pudo conectar…" con **Reintentar** |
| `signedOut` | Sin sesión | Pantalla de login |
| `signedIn` | Con sesión | La app |

El access token vive **solo en memoria**. El refresh token se guarda en `expo-secure-store` (almacén cifrado del sistema). Si el almacén falla (por ejemplo, dispositivo bloqueado) la app sigue funcionando con la sesión en memoria y solo pide iniciar sesión de nuevo al reiniciar.

### 11.2 Arranque

`_layout.tsx` llama `restoreSession()` una vez:

1. Sin refresh token guardado → `signedOut` (login).
2. Con uno guardado → lo canjea en `/api/auth/refresh`; si funciona → `signedIn` y guarda el refresh token **nuevo**.
3. Si el servidor responde 401 (token revocado o vencido) → borra lo guardado y va al login.
4. Si falla por **red** → `offline` con "Reintentar". **No cierra la sesión por un problema de conexión.**

### 11.3 Una sola renovación a la vez

El refresh token es de un solo uso (§6.2), así que dos renovaciones simultáneas harían que la segunda pareciera un robo y el servidor revocaría toda la sesión. `renewSessionOnce()` comparte **una sola promesa** entre todos los que necesiten renovar al mismo tiempo (por ejemplo, tres peticiones que reciben 401 a la vez). Está probado, y se comprobó con una mutación que la prueba falla si se elimina esa protección (3 llamadas en lugar de 1).

### 11.4 Cada petición

`httpClient.ts` recibe un `AccessTokenProvider`:

1. Antes de enviar pide el token (`getValidAccessToken`). Si le quedan menos de 60 s, **renueva primero**, así casi nunca se envía una petición con un token vencido.
2. Envía `Authorization: Bearer <token>`.
3. Si el servidor responde **401**, pide `renewAfterRejection(tokenRechazado)`: si otra petición ya renovó, usa ese token; si no, renueva. Luego **reintenta una sola vez**. Un segundo 401 se devuelve al llamador: nunca hay un bucle de reintentos.
4. Si la renovación falla porque el servidor rechazó el refresh token → sesión terminada → la app vuelve al login. Si falla por red → el error de red llega al llamador y la sesión se conserva.

Los endpoints de `/api/auth/*` usan un cliente aparte **sin** manejo de sesión (`authClient.ts`), para que el propio login o la renovación nunca intenten renovar la sesión recursivamente.

### 11.5 Navegación protegida

`_layout.tsx` usa `Stack.Protected`: las pantallas de la app existen solo con `guard = signedIn` y el login solo con `signedOut`. Con la sesión cerrada **no se puede llegar a ninguna pantalla de la app** (ni por enlace profundo): Expo Router redirige. Al cerrar sesión se **vacía el caché de TanStack Query** para que los datos de una cuenta nunca se muestren a quien inicie sesión después en el mismo dispositivo. El botón **Salir** (esquina superior izquierda de la lista) pide confirmación.

### 11.6 Cerrar sesión

`signOut()` olvida la sesión en el dispositivo **de inmediato** (memoria, almacén seguro, store) y después avisa al servidor para revocar la familia, en modo "mejor esfuerzo": sin conexión igual se cierra la sesión en el teléfono, y el refresh token del servidor expira solo a los 30 días.

---

## 12. Configuración y variables de entorno

Solo la **API** las necesita; el worker no (no sirve HTTP).

| Variable | Obligatoria | Default | Descripción |
|---|---|---|---|
| `JWT_SIGNING_KEY` | **Sí** | — | Secreto de firma. **Mínimo 32 caracteres.** Generar una por entorno (§14). Una clave que contenga `dev-only` se **rechaza fuera de Development** y la API no arranca. |
| `JWT_ISSUER` | No | `photostudio-api` | Claim `iss`. |
| `JWT_AUDIENCE` | No | `photostudio-app` | Claim `aud`. |
| `JWT_ACCESS_TOKEN_MINUTES` | No | `15` | Vida del access token (1–60). |
| `JWT_REFRESH_TOKEN_DAYS` | No | `30` | Vida del refresh token (1–365). |
| `SEED_PHOTOGRAPHER_EMAIL` / `_PASSWORD` / `_ID` | Para crear la cuenta | — | Ver §9. |

Un valor fuera de rango o no numérico en `JWT_*_MINUTES/DAYS` hace que la API **falle al arrancar** con un mensaje claro, en vez de usar un valor inesperado.

En la app ya **no existe** `EXPO_PUBLIC_PHOTOGRAPHER_ID`: el fotógrafo sale de la sesión. Solo queda `EXPO_PUBLIC_API_URL`. Recuerda que las variables `EXPO_PUBLIC_*` van dentro del paquete de la app: nunca pongas ahí un secreto.

---

## 13. Puesta en marcha en local

### 13.1 Backend

El perfil de `launchSettings.json` ya trae una clave `dev-only` (solo válida en Development). Falta crear **tu cuenta**. Las credenciales **no se guardan en el repositorio**: expórtalas en la terminal antes de arrancar.

PowerShell:

```powershell
cd backend
docker compose up -d --wait
$env:SEED_PHOTOGRAPHER_EMAIL    = "tu@correo.com"
$env:SEED_PHOTOGRAPHER_PASSWORD = "una contraseña larga de verdad"
$env:SEED_PHOTOGRAPHER_ID       = "0197a000-0000-7000-8000-000000000001"
dotnet run --project src/PhotoStudio.Api --launch-profile PhotoStudio.Api
```

El `SEED_PHOTOGRAPHER_ID` anterior es el que usan las reservas de desarrollo existentes (el valor que tenía `EXPO_PUBLIC_PHOTOGRAPHER_ID`); así conservas esos datos. En el log debe aparecer `Photographer account Created (id …)`. En los siguientes arranques aparece `Unchanged`.

### 13.2 App

Quita `EXPO_PUBLIC_PHOTOGRAPHER_ID` de `mobile/.env` (ya no se usa; no hace daño dejarla). Con la API corriendo y `EXPO_PUBLIC_API_URL` apuntando a ella, abre la app: verás el login.

### 13.3 Probar a mano con curl

```bash
# login
curl -s -X POST http://localhost:8080/api/auth/login -H 'Content-Type: application/json' \
  -d '{"email":"tu@correo.com","password":"tu contraseña"}'

# sin token → 401
curl -i http://localhost:8080/api/bookings

# con token → 200
curl -s http://localhost:8080/api/bookings -H "Authorization: Bearer <accessToken>"
```

---

## 14. Despliegue en Railway

En el servicio **`photostudio-api`** → *Variables*:

1. **`JWT_SIGNING_KEY`**: genera un secreto aleatorio **solo para producción**, por ejemplo:
   - PowerShell: `[Convert]::ToBase64String([Security.Cryptography.RandomNumberGenerator]::GetBytes(48))`
   - Git Bash / Linux: `openssl rand -base64 48`
   
   Pégalo en Railway. **No lo guardes en el repositorio ni lo reutilices en otro entorno.** Si lo cambias, todos los access tokens vigentes dejan de valer (la app los renueva sola con el refresh token).
2. **`SEED_PHOTOGRAPHER_EMAIL`**, **`SEED_PHOTOGRAPHER_PASSWORD`** (contraseña larga y única) y **`SEED_PHOTOGRAPHER_ID`** (el GUID de tu fotógrafo en la base de producción; si es una base nueva, puedes omitirlo).
3. `JWT_ISSUER`, `JWT_AUDIENCE`, `JWT_ACCESS_TOKEN_MINUTES` y `JWT_REFRESH_TOKEN_DAYS` son opcionales.

Despliega. En los logs de la API debe aparecer `Photographer account Created (id …)`. Si la API no arranca con `'JWT_SIGNING_KEY' is the development key`, copiaste la clave de desarrollo; genera una nueva.

El servicio **`photostudio-worker`** **no necesita** ninguna de estas variables.

**Detrás del proxy de Railway:** la API usa `X-Forwarded-For` para saber la IP real del cliente (necesaria para el límite de intentos de login por IP); ya está configurado.

---

## 15. Contrato de la API

| Método | Ruta | Auth | Cuerpo | Respuestas |
|---|---|---|---|---|
| POST | `/api/auth/login` | Pública (10/min/IP) | `{ email, password }` | 200 sesión · 401 `auth.invalid_credentials` · 429 `auth.account_locked` (+`Retry-After`) · 429 `rate_limit.exceeded` |
| POST | `/api/auth/refresh` | Pública (10/min/IP) | `{ refreshToken }` | 200 sesión nueva · 401 `auth.invalid_refresh_token` · 429 `rate_limit.exceeded` |
| POST | `/api/auth/logout` | Pública (10/min/IP) | `{ refreshToken }` | 204 siempre |
| `*` | `/api/bookings/**`, `/api/maintenance/run` | **Bearer** | (sin `photographerId`) | 401 sin token o token inválido · 404 si la reserva es de otro fotógrafo |

Cambios incompatibles para cualquier cliente anterior: `CreateBookingRequest` ya no acepta `photographerId` y `GET /api/bookings` ya no lleva `?photographerId=`.

---

## 16. Amenazas consideradas

| Amenaza | Defensa | Verificación |
|---|---|---|
| Adivinar contraseñas (fuerza bruta) | Bloqueo de cuenta (5 fallos → 15 min) con contador atómico + 10 intentos/min por IP | Pruebas de integración: bloqueo, contador con 25 intentos paralelos, límite por IP |
| Descubrir qué emails existen | Misma respuesta y mismo tiempo para email desconocido y contraseña mala | Prueba que compara `code` y `detail`; verificación señuelo |
| Robo de la base de datos | Contraseñas con PBKDF2; refresh tokens guardados solo como hash | — |
| Robo de un refresh token | Rotación de un solo uso + revocación de toda la familia al reusarlo | Pruebas de `Refresh_WithAnAlreadyUsedToken_RevokesTheWholeLogin` |
| Dos renovaciones simultáneas | Rotación atómica (gana exactamente una) y, en la app, una sola renovación a la vez | Pruebas con 8 renovaciones paralelas y con 3 peticiones simultáneas |
| Token falsificado o manipulado | Firma HS256 obligatoria; solo ese algoritmo; emisor, audiencia y vencimiento | Pruebas: payload alterado, otra clave, vencido, otra audiencia/emisor, `alg: none` |
| Un fotógrafo accede a datos de otro | Tenant solo del token + `GetOwnedAsync` → 404 | Pruebas unitarias y de integración (8 operaciones), más una prueba de mutación que confirma que fallan sin la verificación |
| Un endpoint nuevo olvidado sin protección | Política de respaldo `RequireAuthenticatedUser` | Pruebas que recorren los endpoints protegidos sin token |
| La clave de desarrollo llega a producción | La API se niega a arrancar con una clave `dev-only` fuera de Development | Verificado a mano |
| Fuga de datos entre cuentas en el mismo teléfono | Se vacía el caché al cerrar sesión; la clave del caché incluye el fotógrafo | — |
| Token en un lugar inseguro del teléfono | Access token solo en memoria; refresh token en `expo-secure-store` | — |

---

## 17. Pruebas

Backend (`cd backend && dotnet test`), las de integración usan el Mongo de `docker compose` y se saltan solas si no está:

| Proyecto / archivo | Qué cubre |
|---|---|
| `Domain.UnitTests/Identity/PhotographerAccountTests`, `RefreshTokenTests` | Normalización y validación del email, bloqueo, vencimiento y revocación. |
| `Application.UnitTests/Identity/LoginHandlerTests` | Éxito, normalización, familia nueva por login, reinicio del contador, email desconocido/inválido (y trabajo de hash), fallo, bloqueo al llegar al límite, cuenta bloqueada, fin del bloqueo. |
| `…/RefreshSessionHandlerTests` | Rotación, token desconocido/vacío, reuso, vencido, cuenta inexistente, carrera. |
| `…/LogoutHandlerTests`, `EnsurePhotographerAccountHandlerTests` | Cierre idempotente; creación, sin cambios, cambio de contraseña con revocación, validaciones. |
| `Application.UnitTests/Bookings/BookingTenantIsolationTests` | Los 8 handlers rechazan la reserva de otro y no guardan nada. |
| `Infrastructure.IntegrationTests/MongoIdentityRepositoriesTests` | Email único, contador atómico bajo concurrencia, bloqueo, rotación atómica (8 en paralelo → 1 gana), revocación por familia y por fotógrafo. |
| `Api.IntegrationTests/AuthenticationApiTests` | Login, email sin distinguir mayúsculas, misma respuesta para email/contraseña malos, bloqueo, límite por IP, rotación, reuso, aislamiento de dispositivos, logout. |
| `…/AccessTokenValidationApiTests` | 401 sin token en cada endpoint, endpoints públicos, token alterado/otra clave/vencido/otra audiencia o emisor/`alg: none`, y el caso de control (token bien formado aceptado). |
| `…/TenantIsolationApiTests` | El tenant sale del token (ignora el cuerpo y el query), lista solo lo propio, 404 en las 8 operaciones sobre reservas ajenas, el dueño sí puede. |

App (`cd mobile && npm run verify`): `session/__tests__/sessionManager.test.ts` (restaurar, renovar, una sola renovación, 401 vs. red, cerrar sesión), `api/__tests__/httpClientAuth.test.ts` (token, un reintento, sin bucles), `authApi.test.ts`, `forms/__tests__/loginForm.test.ts`, `hooks/__tests__/useLoginForm.test.tsx`, `components/__tests__/SignOutButton.test.tsx` y los mensajes de error.

---

## 18. Límites conocidos y siguientes pasos

1. **Un atacante que conozca tu email puede bloquearte** el login 15 minutos enviando contraseñas malas (el bloqueo es por cuenta). Es la contrapartida habitual del bloqueo de cuentas. Mitigación futura: bloquear por (cuenta, IP) o añadir retardos progresivos.
2. **El access token no se puede revocar antes de tiempo** (hasta 15 min tras cerrar sesión o cambiar la contraseña). Es el precio de no consultar la base en cada petición.
3. **La IP real depende de `X-Forwarded-For`.** Se confía en el último salto del proxy; si el contenedor fuera alcanzable sin pasar por Railway, alguien podría falsear la IP y evitar el límite *por IP* (no el bloqueo de cuenta).
4. **Sin cambio de contraseña desde la app ni "olvidé mi contraseña" por correo.** Hoy la recuperación es cambiar `SEED_PHOTOGRAPHER_PASSWORD` y reiniciar (requiere acceso a Railway). Al añadirlo, quitar las variables `SEED_*` para que no pisen la contraseña.
5. **Una sola cuenta.** Para vender como SaaS: registro con verificación de email, recuperación de contraseña por correo (Resend), y un rol de sistema para `/api/maintenance/run`.
6. **Cerrar sesión sin conexión** deja vivo el refresh token en el servidor hasta que expire (30 días), aunque el teléfono ya lo olvidó.
7. **Pendiente (otro mecanismo):** acceso del **cliente** al portal con token de enlace (guardado como hash) y OTP.
8. **Rotar `JWT_SIGNING_KEY`** invalida los access tokens vigentes (la app los renueva sola); no invalida los refresh tokens.
