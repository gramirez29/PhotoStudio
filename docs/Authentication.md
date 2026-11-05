# Autenticación (cuentas de usuario + JWT propio)

Documento de referencia de cómo se crean las cuentas, cómo inicia sesión el fotógrafo y cómo la API sabe quién es cada petición, para que alguien que no vio el código entienda el diseño, dónde vive cada pieza y cómo se opera.

- **Rama de origen:** `feature/photographer-auth`
- **Alcance:** backend (.NET 10 + MongoDB) y app móvil (Expo). El portal del cliente (token de enlace + OTP) **no** está incluido: es otro mecanismo.
- **Decisiones del producto:** las cuentas se crean desde la propia app (pantalla de login → "Crear cuenta"), se guardan en la colección **`users`** (usuario, correo, contraseña —solo su hash—, nombre y teléfono) y se entra con **usuario y contraseña**. El correo se pide y es único, pero **todavía no se verifica ni se usa para entrar** (ver §18).

---

## Índice

1. [Qué problema resuelve](#1-qué-problema-resuelve)
2. [Vista general](#2-vista-general)
3. [Tokens: access y refresh](#3-tokens-access-y-refresh)
4. [Mapa de archivos](#4-mapa-de-archivos)
5. [Backend: crear una cuenta (registro)](#5-backend-crear-una-cuenta-registro)
6. [Backend: login](#6-backend-login)
7. [Backend: renovación (refresh) y rotación](#7-backend-renovación-refresh-y-rotación)
8. [Backend: logout](#8-backend-logout)
9. [Backend: validar cada petición y aislar usuarios](#9-backend-validar-cada-petición-y-aislar-usuarios)
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

- Cada persona tiene un **usuario** (colección `users`) y se identifica con usuario y contraseña.
- Toda petición a `/api/**` (salvo crear cuenta e iniciar sesión) exige un **token firmado** emitido por la propia API.
- El `PhotographerId` sale **únicamente del token**. Lo que el cliente mande en el cuerpo o en la URL se ignora.
- Cada operación sobre una reserva verifica que la reserva pertenece al usuario del token. Si no, la API responde **404**, exactamente igual que si la reserva no existiera.
- El identificador del usuario (`users._id`) **es** el `PhotographerId`: cada cuenta nueva es su propio tenant.

---

## 2. Vista general

```
   App móvil                                    API (.NET)                         MongoDB
 ┌───────────┐  POST /api/auth/register     ┌──────────────────┐
 │ Crear     │ ───────────────────────────▶ │ RegisterUser     │ ─ guarda el usuario ─▶ users
 │ cuenta    │ ◀─ 201 + sesión (ya entró)   │  valida + hash   │ ─ guarda el hash ────▶ refresh_tokens
 └───────────┘                              └──────────────────┘
 ┌───────────┐  POST /api/auth/login        ┌──────────────────┐
 │ Login     │ ───────────────────────────▶ │ LoginHandler     │ ─ usuario por username ▶ users
 │ (usuario +│ ◀─ accessToken + refreshToken│  verifica hash   │ ─ guarda el hash ─────▶ refresh_tokens
 │ password) │    (JWT 15 min / opaco 30 d) └──────────────────┘
 └─────┬─────┘
       │ guarda el refreshToken en expo-secure-store (Keychain / Keystore)
       │ guarda el accessToken solo en memoria
       ▼
 ┌───────────┐  GET /api/bookings           ┌──────────────────┐
 │ cualquier │  Authorization: Bearer <JWT> │ Middleware JWT   │  valida firma, emisor, audiencia,
 │ petición  │ ───────────────────────────▶ │ + Authorization  │  algoritmo y vencimiento
 └─────┬─────┘                              └────────┬─────────┘
       │                                             │ claim "sub" = PhotographerId
       │ 401 (token vencido)                         ▼
       │                                    ┌──────────────────┐
       │  POST /api/auth/refresh            │ Handler          │  GetOwnedAsync(id, photographerId)
       └──────────────────────────────────▶ │ (con tenant)     │  → 404 si la reserva es de otro
          { refreshToken }                  └──────────────────┘
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
| Si se filtra | Sirve hasta 15 minutos | Se detecta al usarse dos veces (ver §7) |

---

## 3. Tokens: access y refresh

### 3.1 Access token (JWT)

Emitido por `JwtAccessTokenIssuer`. Claims:

| Claim | Valor | Uso |
|---|---|---|
| `sub` | `PhotographerId` (= `users._id`, GUID) | **Es el tenant de toda consulta.** |
| `preferred_username` | el usuario | Informativo. |
| `jti` | GUID único | Identifica el token. |
| `iss` / `aud` | `photostudio-api` / `photostudio-app` (configurables) | La API rechaza tokens de otro emisor o audiencia. |
| `iat` / `nbf` / `exp` | emisión / no antes de / vencimiento | Vida de 15 min (configurable). |

No lleva roles, nombre ni teléfono: solo lo necesario para autorizar.

Validación (en `PhotographerAuthenticationExtensions`): firma con la clave del servidor, `iss`, `aud`, vencimiento (tolerancia de 30 s), `RequireSignedTokens`, y **`ValidAlgorithms = [HS256]`**, de modo que un token con `alg: none` o con otro algoritmo se rechaza. `MapInboundClaims = false` mantiene los nombres de claim tal cual.

### 3.2 Refresh token

Generado por `RefreshTokenGenerator`: 256 bits del generador aleatorio del sistema, en base64url. **Lo que se guarda en Mongo es `SHA-256(secreto)`**, nunca el secreto: si alguien lee la base de datos no obtiene tokens utilizables. Un hash rápido es suficiente aquí (a diferencia de una contraseña) porque el secreto ya tiene entropía completa y no hay nada que adivinar por fuerza bruta.

Cada login o registro crea una **familia** (`familyId`). Cada renovación revoca el token usado y emite uno nuevo **de la misma familia**. La familia es la unidad de revocación: cerrar sesión o detectar un robo revoca solo esa familia, así que cerrar sesión en el teléfono no cierra la de la tablet.

---

## 4. Mapa de archivos

Rutas relativas a `backend/` (API) y `mobile/` (app).

### Backend: Domain

| Archivo | Rol |
|---|---|
| `src/PhotoStudio.Domain/Identity/User.cs` | Agregado del usuario: `username`, hash de la contraseña, `name`, `phone`, contador de fallos y bloqueo. Reglas de validación y normalización (username, nombre, teléfono, largo de contraseña). `MaxFailedLoginAttempts = 5`, `LockoutDuration = 15 min`. |
| `src/PhotoStudio.Domain/Identity/RefreshToken.cs` | Refresh token (solo su hash), familia, vencimiento, revocación. |
| `src/PhotoStudio.Domain/Common/DomainErrorCodes.cs` | Códigos `user.invalid_username`, `user.invalid_name`, `user.invalid_phone`, `user.weak_password`. |

### Backend: Application

| Archivo | Rol |
|---|---|
| `Abstractions/IUserRepository.cs`, `IRefreshTokenRepository.cs` | Puertos de persistencia. |
| `Abstractions/IPasswordHasher.cs`, `IAccessTokenIssuer.cs`, `IRefreshTokenGenerator.cs` | Puertos de criptografía y emisión (los implementa Infrastructure). |
| `Identity/Register/RegisterUserHandler.cs` | Crea el usuario y lo deja con sesión iniciada. |
| `Identity/Login/LoginHandler.cs` | Login. |
| `Identity/RefreshSession/RefreshSessionHandler.cs` | Renovación con rotación y detección de robo. |
| `Identity/Logout/LogoutHandler.cs` | Cierre de sesión. |
| `Identity/SessionIssuer.cs`, `AuthSessionSettings.cs`, `RegistrationSettings.cs`, `AuthSessionResponse.cs` | Arma los tokens; vida del refresh token; interruptor del registro; respuesta. |
| `Exceptions/AuthenticationFailedException.cs`, `AccountLockedException.cs`, `RegistrationDisabledException.cs` | Errores 401, 429 y 403. |
| `Bookings/BookingRepositoryExtensions.cs` | `GetOwnedAsync`: carga una reserva **solo si es del usuario**. |
| `DependencyInjection.cs` | `AddApplicationAuthentication()`. |

### Backend: Infrastructure

| Archivo | Rol |
|---|---|
| `Identity/AspNetPasswordHasher.cs` | Hash de contraseñas (PBKDF2 del hasher de ASP.NET Core). |
| `Identity/RefreshTokenGenerator.cs` | Secreto aleatorio y su hash SHA-256. |
| `Identity/JwtOptions.cs`, `JwtAccessTokenIssuer.cs` | Variables `JWT_*` y emisión del JWT. |
| `Identity/PhotographerAuthenticationExtensions.cs` | Validación JWT, política por defecto (todo protegido) e interruptor `AUTH_REGISTRATION_ENABLED`. |
| `Persistence/MongoUserRepository.cs`, `MongoRefreshTokenRepository.cs` | Repositorios Mongo. |
| `Persistence/Documents/IdentityDocuments.cs`, `IdentityDocumentMappings.cs` | Documentos y mapeos. |
| `Persistence/MongoIndexInitializer.cs` | Índices (username único, hash único, TTL, etc.). |

### Backend: Api

| Archivo | Rol |
|---|---|
| `Endpoints/AuthEndpoints.cs`, `AuthRequests.cs` | `/api/auth/register`, `/login`, `/refresh`, `/logout`. |
| `Auth/ClaimsPrincipalExtensions.cs` | `GetPhotographerId()`: el tenant sale del claim `sub`. |
| `Endpoints/BookingEndpoints.cs`, `BookingRequests.cs` | Todos los endpoints pasan el usuario del token. |
| `RateLimiting/RateLimitingExtensions.cs` | 5 registros por hora y 10 intentos de sesión por minuto, por IP. |
| `Errors/GlobalExceptionHandler.cs` | 401, 403 y 429 con `code` estable. |
| `Program.cs` | Composición: `UseForwardedHeaders`, `UseAuthentication`, `UseAuthorization`. |
| `scripts/migrate-tenant.js` | Script de `mongosh` para pasar las reservas de un tenant antiguo a un usuario (ver §13.3). |

### App móvil (`mobile/src/`)

| Archivo | Rol |
|---|---|
| `session/sessionStore.ts`, `sessionManager.ts` | Estado de la sesión (Zustand) y su ciclo: registrar, entrar, restaurar, renovar (una a la vez), salir. |
| `storage/refreshTokenStorage.ts` | Guarda el refresh token en `expo-secure-store`. |
| `api/authApi.ts`, `api/authClient.ts` | Llamadas a `/api/auth/*` con un cliente **sin** manejo de sesión. |
| `api/httpClient.ts`, `api/client.ts` | Adjunta el token y, ante un 401, renueva y reintenta **una vez**. |
| `app/_layout.tsx`, `app/login.tsx` | Navegación protegida y pantalla de acceso. |
| `components/AuthPanel.tsx`, `LoginForm.tsx`, `RegisterForm.tsx`, `KeyboardAwareScreen.tsx`, `SignOutButton.tsx` | Pantalla de acceso (alterna login/crear cuenta), formularios, contenedor que esquiva el teclado, botón "Salir". |
| `forms/loginForm.ts`, `forms/registerForm.ts`; `hooks/useLoginForm.ts`, `hooks/useRegisterForm.ts` | Validación y estado de los formularios. |

---

## 5. Backend: crear una cuenta (registro)

`POST /api/auth/register` con `{ "username", "password", "name", "phone" }`. Lo ejecuta `RegisterUserHandler`:

```
1. si el registro está cerrado (AUTH_REGISTRATION_ENABLED=false)  → 403 auth.registration_disabled
2. valida y normaliza TODO antes de gastar CPU en el hash:
       contraseña (8–128), username, correo, nombre, teléfono → 422 con el código del campo
3. crea el usuario con un Id nuevo (Guid v7) y el hash de la contraseña
4. lo guarda; los índices únicos deciden si el usuario o el correo ya existían
                                                            → 409 user.username_taken / user.email_taken
5. abre sesión (familia nueva) y guarda el hash del refresh token
6. → 201 con la sesión: la persona ya está dentro
```

### 5.1 Campos del usuario

| Campo | Regla | Se guarda como |
|---|---|---|
| `username` | 3 a 30 caracteres: letras `a-z`, dígitos, `.`, `-` y `_`; empieza y termina con letra o dígito. No distingue mayúsculas. | Minúsculas |
| `email` | Obligatorio, hasta 254 caracteres: una arroba, algo antes, un dominio con punto después y sin espacios. Solo se comprueba la **forma**, no que el buzón exista. No distingue mayúsculas. **Único.** | Minúsculas |
| `password` | 8 a 128 caracteres (solo se valida el largo; una frase larga vale más que una regla de composición). El máximo evita que alguien nos haga gastar CPU con una clave enorme. | **Solo el hash** (PBKDF2 con sal) |
| `name` | Obligatorio, hasta 100 caracteres | Sin espacios sobrantes |
| `phone` | 8 a 15 dígitos, con `+` opcional al inicio. Se quitan espacios, guiones, puntos y paréntesis | Ej.: `+50670189220` |

Respecto a "contraseña encriptada": la contraseña **no se cifra** (el cifrado se puede revertir), se **hashea** (no se puede revertir). Es la práctica correcta: ni el servidor ni quien lea la base de datos puede recuperar la contraseña original; solo comprobar si una candidata coincide.

### 5.2 Concurrencia

Dos registros simultáneos con el mismo usuario (o el mismo correo): gana exactamente uno. No se hace "buscar y luego insertar" (que dejaría pasar a los dos); los **índices únicos** `ix_user_username` e `ix_user_email` rechazan al segundo y el repositorio lo convierte en `409`. El servidor nombra el índice violado en su error, y así el repositorio distingue `user.username_taken` de `user.email_taken`. Hay una prueba con 4 registros paralelos que exige 1 creado y 3 conflictos.

### 5.3 Quién puede registrarse

Cualquiera que llegue a la API, mientras el registro esté abierto (por defecto lo está). Defensas:

- **Límite de 5 registros por hora por IP** (`register`), más estricto que el de login porque crear cuentas es la forma más barata de llenar la base.
- **Interruptor `AUTH_REGISTRATION_ENABLED=false`**: cierra el registro (403) sin tocar el login ni a los usuarios existentes. Pensado para activarlo en producción una vez creada tu cuenta.

---

## 6. Backend: login

`POST /api/auth/login` con `{ "username", "password" }`. Lo ejecuta `LoginHandler`:

```
1. username = TryNormalizeUsername(command.Username)   // recorta y pasa a minúsculas; null si no es válido
2. user = username == null ? null : GetByUsernameAsync(username)
3. si user == null:
       SpendVerificationTime(password)                 // gasta el mismo tiempo que una verificación real
       → 401 auth.invalid_credentials
4. si la cuenta está bloqueada para todas las direcciones, o ESTA dirección está bloqueada para la cuenta:
       → 429 auth.account_locked  (+ Retry-After)      // sin verificar la contraseña
5. si Verify(hash, password) falla:
       desde esta dirección = RegisterFailureAsync(id, dirección)   // $inc atómico por (cuenta, dirección)
       si >= 5: BlockAsync(id, dirección, ahora + 15 min)           // bloquea SOLO esa dirección
       en total = RegisterFailedLoginAsync(id)                       // $inc atómico de la cuenta
       si en total >= 20: LockAsync(id, ahora + 15 min)             // bloquea la cuenta para todos
       → 401 auth.invalid_credentials
6. se olvidan los fallos de esta dirección y, si había fallos o bloqueo de la cuenta, ResetFailedLoginsAsync(id)
7. sesión = nueva familia + access token + refresh token; guarda el hash del refresh token
8. → 200 con la sesión
```

Decisiones de seguridad:

- **No se revela si el usuario existe.** Usuario inexistente, usuario con formato inválido y contraseña incorrecta responden con el mismo estado, el mismo `code` y el mismo `detail`. El caso "usuario desconocido" ejecuta además una verificación de hash señuelo (`SpendVerificationTime`) para que el **tiempo de respuesta** tampoco lo delate. (El *registro* sí revela que un nombre está ocupado: es inherente a poder elegir nombre.)
- **El contador de fallos es atómico.** Si se cargara el usuario, se incrementara y se guardara con concurrencia optimista, un atacante que enviara 100 intentos en paralelo haría que casi todas las escrituras chocaran y el contador apenas avanzara, burlando el bloqueo. Con `$inc` cada intento cuenta (hay una prueba con 25 intentos paralelos que exige que el contador llegue a exactamente 25).
- **Bloqueo temporal, no permanente, y por cuenta Y dirección** (`LoginAttemptGuard`, §18.3): 5 fallos seguidos **desde una misma dirección** contra una cuenta bloquean **esa dirección para esa cuenta** durante 15 minutos; el dueño que entra desde otra dirección no se entera. La cuenta se bloquea **para todas las direcciones** solo con 20 fallos entre varias (una sola dirección nunca llega: la bloquean a los 5, y lo que intenta mientras está bloqueada se rechaza sin contarse). La dirección es la del último salto del proxy (`X-Forwarded-For`), con las IPv6 agrupadas por su red /64, y se guarda solo como **hash con clave** (HMAC ligado a la cuenta), nunca en claro. Las mismas reglas valen para el login de la app, el de la web y la contraseña actual del cambio de contraseña. Recuperar o cambiar la contraseña olvida todos los fallos.
- **Cada login crea su propia familia** de refresh tokens.

Respuesta de registro y de login (`AuthSessionResponse`):

```json
{
  "accessToken": "eyJhbGciOi...",
  "accessTokenExpiresAt": "2026-10-08T21:15:00+00:00",
  "refreshToken": "q3Vf...",
  "refreshTokenExpiresAt": "2026-11-07T21:00:00+00:00",
  "photographerId": "0199a1b2-0000-7000-8000-000000000002",
  "username": "ana.photo",
  "email": "ana@example.com",
  "name": "Ana Pérez"
}
```

---

## 7. Backend: renovación (refresh) y rotación

`POST /api/auth/refresh` con `{ "refreshToken" }`. Lo ejecuta `RefreshSessionHandler`:

```
1. hash = SHA-256(refreshToken); token = GetByHashAsync(hash)
2. si no existe o el secreto está vacío          → 401 auth.invalid_refresh_token
3. si token.IsRevoked:                            // alguien lo usa por SEGUNDA vez
       RevokeFamilyAsync(token.FamilyId)          // se mata toda la familia
       → 401
4. si token vencido                               → 401
5. si el usuario ya no existe: revoca la familia  → 401
6. nueva = access token + refresh token (misma familia)
7. RotateAsync(actual, nueva)                     // atómico, ver abajo
       si devuelve false (otro lo rotó antes): revoca la familia → 401
8. → 200 con la sesión nueva
```

### 7.1 Rotación atómica

`MongoRefreshTokenRepository.RotateAsync` hace, **dentro de una transacción**:

1. `UpdateOne` sobre el token actual con filtro `{ _id, revokedAt: null }` que fija `revokedAt` y `replacedById`.
2. Si no modificó nada (`ModifiedCount == 0`), el token ya estaba usado: devuelve `false` sin insertar nada.
3. Si lo modificó, inserta el token de reemplazo.

Como el filtro exige `revokedAt: null`, de varias renovaciones simultáneas del mismo token **exactamente una** gana (prueba de integración con 8 en paralelo: una gana y se guarda un solo reemplazo). Y como todo va en una transacción, un fallo a mitad no deja al usuario sin token válido.

### 7.2 Por qué reusar un token revoca toda la familia

Un refresh token legítimo se usa una sola vez: la app lo cambia por uno nuevo y descarta el viejo. Si el servidor ve un token ya usado, solo hay dos explicaciones: alguien lo copió (robo) o hay un fallo del cliente. En ambos casos lo seguro es **invalidar toda la familia**: el ladrón y el dueño pierden la sesión y el dueño tiene que iniciar sesión de nuevo. Es el comportamiento recomendado por la guía de seguridad de OAuth para refresh tokens públicos/móviles.

Consecuencia para el cliente: **no puede haber dos renovaciones en paralelo con el mismo token**, porque la segunda parecería un robo. Por eso la app serializa las renovaciones (§11.3).

---

## 8. Backend: logout

`POST /api/auth/logout` con `{ "refreshToken" }`. `LogoutHandler` busca el token por hash y revoca **su familia**. Siempre responde **204**, exista o no el token, para no revelar qué tokens son válidos. Es idempotente.

El access token vigente sigue siendo válido hasta que venza (máximo 15 min): los JWT no se pueden revocar sin consultar la base en cada petición, y el diseño prefiere la vida corta.

La app, antes de llamar a `signOut()`, pide `POST /api/push-devices/unregister` (con el access token que aún vale, máximo 3 s y sin consecuencias si falla) para que el teléfono deje de recibir los pushes de esa cuenta; ver `PushNotifications.md`. Los endpoints `/api/push-devices/**` exigen sesión como todo `/api/**` y se acotan al `PhotographerId` del token.

---

## 9. Backend: validar cada petición y aislar usuarios

### 9.1 Seguro por defecto

`PhotographerAuthenticationExtensions` define una **política de respaldo** (`FallbackPolicy = RequireAuthenticatedUser`). Significa que un endpoint nuevo es **privado por defecto**: para hacerlo público hay que decir `AllowAnonymous()` explícitamente. Hoy son públicos únicamente:

| Endpoint | Por qué |
|---|---|
| `POST /api/auth/register`, `/login`, `/refresh`, `/logout` | Crean y cierran la sesión (con límite por IP). |
| `GET /health/live`, `/health/ready` | Railway los consulta sin credenciales. |
| `GET /openapi/v1.json` | Fuente de los tipos TypeScript. |

Todo lo demás (`/api/bookings/**`, `/api/maintenance/run`) devuelve **401** sin token válido, con la cabecera `WWW-Authenticate: Bearer`.

### 9.2 El tenant sale solo del token

`ClaimsPrincipalExtensions.GetPhotographerId()` lee el claim `sub`. Los endpoints lo pasan a los comandos:

```csharp
private static async Task<Ok<BookingResponse>> CancelAsync(
    Guid id, ClaimsPrincipal user, CancelBookingRequest request, ICommandHandler<CancelBookingCommand, BookingResponse> handler, CancellationToken ct) =>
    TypedResults.Ok(await handler.HandleAsync(request.ToCommand(user.GetPhotographerId(), id), ct));
```

`CreateBookingRequest` no tiene `PhotographerId` y `GET /api/bookings` no recibe `?photographerId=`. Si un cliente malicioso los envía igual, se ignoran (hay pruebas de integración para ambos casos).

### 9.3 Cada operación verifica la propiedad

Todos los comandos y consultas de reservas llevan `PhotographerId` como primer parámetro, y sus handlers cargan la reserva con:

```csharp
var booking = await repository.GetOwnedAsync(command.BookingId, command.PhotographerId, cancellationToken);
```

`GetOwnedAsync` lanza `NotFoundException` si la reserva no existe **o si es de otro usuario**, así que ambos casos responden **404 idéntico**: la API no confirma que un identificador ajeno exista. Cubre los 8 handlers: obtener, cancelar, completar, marcar ausente, revertir ausencia, reprogramar, firmar contrato y registrar pago. Está probado a nivel unitario (`BookingTenantIsolationTests`) y de punta a punta (`TenantIsolationApiTests`, `RegistrationApiTests`).

> **Trabajo global:** `POST /api/maintenance/run` ejecuta trabajo de *todos* los usuarios (expira las reservas vencidas de todos). Por eso exige el **rol de sistema** (ver §19), no solo una sesión: un usuario normal recibe **403**.

---

## 10. Persistencia en MongoDB

### `users`

| Campo | Significado |
|---|---|
| `_id` | **`PhotographerId`** (GUID v7). Es el tenant de todas las demás colecciones. |
| `username` | Normalizado (minúsculas). **Índice único `ix_user_username`**. |
| `email` | Normalizado (minúsculas). **Índice único y parcial `ix_user_email`** (solo para correos no vacíos, así los usuarios creados antes de que existiera el correo no chocan entre sí). |
| `passwordHash` | Hash PBKDF2 con sal y parámetros (formato del hasher de ASP.NET Core). **Nunca la contraseña.** |
| `name`, `phone` | Nombre y teléfono, ya normalizados. |
| `version`, `createdAt` | Versión y alta. |
| `failedLoginAttempts`, `lockedUntil` | Fallos seguidos **de la cuenta entre todas las direcciones** y fin de su bloqueo total (los fallos por dirección están en `login_failures`). |

### `refresh_tokens`

| Campo | Significado |
|---|---|
| `_id`, `photographerId`, `familyId` | Identificador, dueño (= `users._id`) y familia de login. |
| `tokenHash` | SHA-256 del secreto. **Índice único `ix_refresh_hash`**. |
| `createdAt`, `expiresAt` | Emisión y vencimiento. |
| `revokedAt`, `replacedById` | Cuándo se usó/revocó y qué token lo reemplazó. |

Índices: `ix_user_username` (único), `ix_user_email` (único, parcial), `ix_refresh_hash` (único), `ix_refresh_family` y `ix_refresh_photographer` (revocar una familia o las sesiones de un usuario) y `ix_refresh_retention` (TTL: Mongo borra los tokens 7 días después de vencer; se conservan ese tiempo para reconocer un token vencido que se reenvía).

> La colección anterior `photographer_accounts` (la usó una versión previa de esta rama) ya no se usa. Si existe en tu base de desarrollo puedes borrarla: `db.photographer_accounts.drop()`.

---

## 11. App móvil

### 11.1 Pantalla de acceso

`app/login.tsx` muestra `AuthPanel`, que alterna entre dos formularios con un enlace al pie:

| Modo | Campos | Botón |
|---|---|---|
| **Iniciar sesión** (por defecto) | Usuario, Contraseña | Iniciar sesión · "¿No tienes cuenta? **Crear cuenta**" |
| **Crear cuenta** | Nombre, Teléfono (se muestra como `7018-9220`), Correo, Usuario, Contraseña, Repite la contraseña | Crear cuenta · "¿Ya tienes cuenta? **Iniciar sesión**" |

La validación del formulario (`forms/registerForm.ts`) repite las reglas del servidor para avisar junto al campo sin ir a la red: nombre obligatorio, teléfono válido (8 dígitos CR o con código de país), correo con forma válida (la app es un poco más estricta que el servidor: no admite dos puntos seguidos en el dominio), usuario de 3–30 caracteres con el formato permitido, contraseña de 8 a 128 y que las dos coincidan. El servidor vuelve a validar todo: la validación de la app es solo comodidad. El usuario se pasa a minúsculas antes de enviarlo.

Al crear la cuenta la persona **queda dentro** (el registro devuelve una sesión), sin volver a escribir sus datos.

### 11.2 Estados de la sesión

`sessionStore.ts` (Zustand) guarda `status` y, si hay sesión, el access token, su vencimiento, el `photographerId`, el usuario y el nombre:

| Estado | Significado | Qué se ve |
|---|---|---|
| `restoring` | Buscando una sesión guardada al abrir la app | Indicador de carga |
| `offline` | Hay sesión guardada pero no se pudo contactar al servidor para renovarla | "No se pudo conectar…" con **Reintentar** |
| `signedOut` | Sin sesión | Pantalla de acceso |
| `signedIn` | Con sesión | La app |

El access token vive **solo en memoria**. El refresh token se guarda en `expo-secure-store`. Si el almacén falla la app sigue funcionando con la sesión en memoria y solo pide iniciar sesión de nuevo al reiniciar.

### 11.3 Arranque y una sola renovación a la vez

`_layout.tsx` llama `restoreSession()` una vez: sin token guardado → acceso; con uno → lo canjea y guarda el nuevo; si el servidor responde 401 → borra lo guardado; si falla por **red** → `offline` con "Reintentar" (**no cierra la sesión por un problema de conexión**).

El refresh token es de un solo uso (§7.2), así que dos renovaciones simultáneas harían que la segunda pareciera un robo. `renewSessionOnce()` comparte **una sola promesa** entre todos los que necesiten renovar al mismo tiempo. Está probado, y se comprobó con una mutación que la prueba falla si se elimina esa protección (3 llamadas en lugar de 1).

### 11.4 Cada petición

`httpClient.ts` recibe un `AccessTokenProvider`: pide el token (si le quedan menos de 60 s, **renueva primero**), envía `Authorization: Bearer <token>`, y si el servidor responde **401** renueva una vez y **reintenta una sola vez** (un segundo 401 se devuelve: nunca hay bucle). Los endpoints de `/api/auth/*` usan un cliente aparte **sin** manejo de sesión (`authClient.ts`), para que el login o la renovación nunca intenten renovar recursivamente.

### 11.5 Navegación protegida

`_layout.tsx` usa `Stack.Protected`: las pantallas de la app existen solo con `signedIn` y la de acceso solo con `signedOut`. Con la sesión cerrada **no se puede llegar a ninguna pantalla de la app**. Al cerrar sesión se **vacía el caché de TanStack Query**. El botón **Salir** (esquina superior izquierda de la lista) pide confirmación.

### 11.6 Cerrar sesión

El botón "Salir" llama a `signOutOfDevice()` (`push/pushSignOut.ts`), que primero pide al backend olvidar el teléfono del push (mejor esfuerzo, tope de 3 s) y luego a `signOut()`. `signOut()` olvida la sesión en el dispositivo **de inmediato** y después avisa al servidor para revocar la familia, en modo "mejor esfuerzo": sin conexión igual se cierra la sesión en el teléfono, y el refresh token del servidor expira solo a los 30 días.

### 11.7 El teclado ya no tapa los campos

**Síntoma:** al tocar un campo del login, el teclado cubría la mitad de la pantalla y tapaba donde se escribe.

**Causa probable:** la app se dibuja de borde a borde en Android (*edge-to-edge*), por lo que el sistema **ya no reduce la ventana** cuando aparece el teclado, y el formulario anterior solo se ajustaba en iOS (`KeyboardAvoidingView` con `behavior` indefinido en Android).

**Arreglo:** el nuevo componente `KeyboardAwareScreen` (usado por el acceso y, con `align="top"`, por los formularios de nueva reserva y de motivo) envuelve el contenido en un `KeyboardAvoidingView` con `behavior="padding"` **en ambas plataformas**, de modo que el área se encoge al espacio libre sobre el teclado, y dentro un `ScrollView` que permite desplazarse si el formulario no cabe (el de crear cuenta tiene cinco campos), con `keyboardShouldPersistTaps="handled"` (los toques siguen funcionando con el teclado abierto) y `keyboardDismissMode="on-drag"` (arrastrar cierra el teclado). Hay pruebas que fijan esas propiedades para que no se pierdan sin querer.

> Esto no se pudo comprobar en un dispositivo desde el entorno de desarrollo del asistente: las pruebas verifican la configuración del componente, no el comportamiento visual del teclado. Las pantallas de nueva reserva y de motivo tenían el mismo patrón anterior (ajuste solo en iOS o ninguno) y el problema se confirmó en la de reservas, así que ahora usan el mismo contenedor (`align="top"`: el contenido empieza arriba en vez de centrarse). La de reprogramar no lo necesita porque no tiene campos de texto. Si algún formulario sigue quedando tapado, indica el modelo/sistema del teléfono.

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
| `AUTH_EMAIL_VERIFICATION_REQUIRED` | No | `true` | `false` deja usar la app sin verificar el correo y no envía código al registrarse (§20). Mismo criterio de valores que el de arriba. |
| `AUTH_REGISTRATION_ENABLED` | No | `true` | `false` cierra la creación de cuentas (403). Cualquier otro valor que no sea `true`/`false` hace que la API no arranque. |

Un valor fuera de rango o no numérico en las `JWT_*` hace que la API **falle al arrancar** con un mensaje claro.

Ya **no existen** las variables `SEED_PHOTOGRAPHER_*` ni `EXPO_PUBLIC_PHOTOGRAPHER_ID`: el usuario sale del registro y de la sesión. En la app solo queda `EXPO_PUBLIC_API_URL`. Las variables `EXPO_PUBLIC_*` van dentro del paquete de la app: nunca pongas ahí un secreto.

---

## 13. Puesta en marcha en local

### 13.1 Backend

No hace falta definir ninguna variable: el perfil de `launchSettings.json` trae la clave de desarrollo.

```powershell
cd backend
docker compose up -d --wait
dotnet run --project src/PhotoStudio.Api --launch-profile PhotoStudio.Api
```

### 13.2 App

Abre la app y toca **Crear cuenta**. Eliges tu usuario y contraseña ahí mismo; no hay credenciales predefinidas. Con `EXPO_PUBLIC_API_URL` apuntando a la API, quedas dentro al terminar.

### 13.3 Recuperar las reservas de desarrollo que ya tenías

Las reservas creadas antes de este cambio están guardadas bajo un identificador antiguo (`0197a000-0000-7000-8000-000000000001`), y la cuenta nueva tiene un identificador propio, así que no las verías. `backend/scripts/migrate-tenant.js` las pasa a tu usuario (reservas **y** agenda), y es idempotente:

```powershell
cd D:\Repositories\Applications\PhotoStudio
docker exec -i photostudio-mongo mongosh photostudio_dev --quiet --eval "const username='tu.usuario'; $(Get-Content backend/scripts/migrate-tenant.js -Raw)"
```

Imprime algo como `{"username":"tu.usuario","bookingsMoved":8,"calendarMoved":true}`. Si el usuario no existe, falla con un mensaje claro sin tocar nada. El mismo script sirve con otro origen: `const fromId='<GUID anterior>'`.

### 13.4 Probar a mano

```bash
curl -s -X POST http://localhost:8080/api/auth/register -H 'Content-Type: application/json' \
  -d '{"username":"ana.photo","email":"ana@example.com","password":"una clave larga","name":"Ana Perez","phone":"7018-9220"}'
curl -i http://localhost:8080/api/bookings                                # sin token → 401
curl -s http://localhost:8080/api/bookings -H "Authorization: Bearer <accessToken>"   # → 200
```

(Con tildes, envía el JSON en UTF-8 puro, por ejemplo desde un archivo con `-d @archivo.json`; algunas terminales de Windows lo codifican mal y la API responde 400.)

---

## 14. Despliegue en Railway

En el servicio **`photostudio-api`** → *Variables*:

1. **`JWT_SIGNING_KEY`**: genera un secreto aleatorio **solo para producción**:
   - PowerShell: `[Convert]::ToBase64String([Security.Cryptography.RandomNumberGenerator]::GetBytes(48))`
   - Git Bash / Linux: `openssl rand -base64 48`

   Pégalo en Railway. **No lo guardes en el repositorio ni lo reutilices en otro entorno.** Si lo cambias, los access tokens vigentes dejan de valer (la app los renueva sola).
2. Despliega, abre la app apuntando a la API de producción y **crea tu cuenta** con "Crear cuenta".
3. **Cierra el registro**: añade `AUTH_REGISTRATION_ENABLED` = `false` y redespliega. A partir de ahí nadie más puede crear cuentas; tú sigues entrando con las tuyas. Para abrirlo otra vez, bórrala o ponla en `true`.
4. Opcionales: `JWT_ISSUER`, `JWT_AUDIENCE`, `JWT_ACCESS_TOKEN_MINUTES`, `JWT_REFRESH_TOKEN_DAYS`.

Si la API no arranca con `'JWT_SIGNING_KEY' is the development key`, copiaste la clave de desarrollo; genera una nueva. El servicio **`photostudio-worker`** **no necesita** ninguna de estas variables.

**Detrás del proxy de Railway:** la API usa `X-Forwarded-For` para saber la IP real del cliente (necesaria para los límites por IP); ya está configurado.

> **Si olvidas tu contraseña**, usa "¿Olvidaste tu contraseña?" en la app (§22); funciona solo si el correo de la cuenta está verificado. Para una cuenta sin correo verificado no hay recuperación automática. Opciones: crear otra cuenta y pasarle tus reservas con `migrate-tenant.js` (`fromId` = tu `users._id` anterior), o, con acceso a Mongo, borrar tu documento de `users` y volver a registrarte. Es una limitación asumida; ver §18.

---

## 15. Contrato de la API

| Método | Ruta | Auth | Cuerpo | Respuestas |
|---|---|---|---|---|
| POST | `/api/auth/register` | Pública (5/hora/IP) | `{ username, email, password, name, phone }` | **201** sesión · 409 `user.username_taken` / `user.email_taken` · 422 `user.invalid_username` / `user.invalid_email` / `user.invalid_name` / `user.invalid_phone` / `user.weak_password` · 403 `auth.registration_disabled` · 429 `rate_limit.exceeded` |
| POST | `/api/auth/login` | Pública (10/min/IP) | `{ username, password }` | 200 sesión · 401 `auth.invalid_credentials` · 429 `auth.account_locked` (+`Retry-After`) · 429 `rate_limit.exceeded` |
| POST | `/api/auth/refresh` | Pública (10/min/IP) | `{ refreshToken }` | 200 sesión nueva · 401 `auth.invalid_refresh_token` · 429 `rate_limit.exceeded` |
| POST | `/api/auth/logout` | Pública (10/min/IP) | `{ refreshToken }` | 204 siempre |
| POST | `/api/account/password` | **Bearer** (10/min/IP) | `{ currentPassword, newPassword }` | 200 sesión nueva de este dispositivo · 422 `user.invalid_current_password` / `user.weak_password` · 429 `auth.account_locked` / `rate_limit.exceeded` · 401 sin token |
| `*` | `/api/bookings/**` | **Bearer** | (sin `photographerId`) | 401 sin token o token inválido · 404 si la reserva es de otro usuario |
| POST | `/api/maintenance/run` | **Bearer + rol de sistema** | — | 200 · 401 sin token · **403** sin el rol · 429 `rate_limit.exceeded` |

---

## 16. Amenazas consideradas

| Amenaza | Defensa | Verificación |
|---|---|---|
| Crear cuentas en masa (spam, llenar la base) | 5 registros/hora/IP e interruptor `AUTH_REGISTRATION_ENABLED` para cerrarlo | Prueba del límite por IP y de registro cerrado → 403. **Riesgo residual:** sin verificación de correo, un atacante con muchas IPs puede crear cuentas |
| Dos personas toman el mismo usuario o correo a la vez | Índices únicos `ix_user_username` e `ix_user_email` (no "buscar y luego insertar") | Prueba con 4 registros paralelos: 1 creado, 3 conflictos; prueba de correo repetido con otras mayúsculas |
| Descubrir qué correos están registrados | **No se evita:** el registro dice `user.email_taken`, igual que dice `user.username_taken`; es inherente a exigir unicidad. El login sí es opaco (no distingue usuario inexistente de contraseña mala) y no acepta el correo para entrar | — |
| Adivinar contraseñas (fuerza bruta) | Bloqueo por cuenta y dirección (5 fallos → 15 min esa dirección; 20 entre varias → la cuenta) con contadores atómicos + 10 intentos/min por IP | Pruebas de bloqueo, de 25 intentos paralelos y de límite por IP |
| Descubrir qué usuarios existen al iniciar sesión | Misma respuesta y mismo tiempo para usuario desconocido y contraseña mala | Prueba que compara `code` y `detail`; verificación señuelo |
| Robo de la base de datos | Contraseñas solo como hash PBKDF2; refresh tokens solo como hash | Prueba que revisa que la base no contiene la contraseña |
| Robo de un refresh token | Rotación de un solo uso + revocación de toda la familia al reusarlo | Pruebas de reuso |
| Dos renovaciones simultáneas | Rotación atómica y, en la app, una sola renovación a la vez | 8 renovaciones paralelas y 3 peticiones simultáneas |
| Token falsificado o manipulado | Firma HS256 obligatoria; emisor, audiencia y vencimiento | Pruebas: payload alterado, otra clave, vencido, otra audiencia/emisor, `alg: none` |
| Un usuario accede a datos de otro | Tenant solo del token + `GetOwnedAsync` → 404 | Pruebas unitarias y de integración (8 operaciones), más una prueba de mutación |
| Un endpoint nuevo olvidado sin protección | Política de respaldo `RequireAuthenticatedUser` | Pruebas que recorren los endpoints protegidos sin token |
| Contraseña enorme para gastar CPU | Máximo de 128 caracteres, validado antes de hashear | Pruebas de registro |
| La clave de desarrollo llega a producción | La API se niega a arrancar con una clave `dev-only` fuera de Development | Verificado a mano |
| Fuga de datos entre cuentas en el mismo teléfono | Se vacía el caché al cerrar sesión; la clave del caché incluye el usuario | — |
| Token en un lugar inseguro del teléfono | Access token solo en memoria; refresh token en `expo-secure-store` | — |

---

## 17. Pruebas

Backend (`cd backend && dotnet test`; las de integración usan el Mongo de `docker compose` y se saltan solas si no está):

| Proyecto / archivo | Qué cubre |
|---|---|
| `Domain.UnitTests/Identity/UserTests`, `RefreshTokenTests` | Normalización y validación de usuario, correo, nombre, teléfono y contraseña (casos límite), bloqueo, vencimiento y revocación. |
| `Application.UnitTests/Identity/RegisterUserHandlerTests` | Guarda el usuario normalizado con el hash, abre sesión, identificador propio por usuario, usuario ocupado, validaciones **antes** de hashear, registro cerrado. |
| `…/LoginHandlerTests`, `RefreshSessionHandlerTests`, `LogoutHandlerTests` | Éxito, normalización, familia nueva por login, usuario desconocido/inválido (y trabajo de hash), fallos y bloqueo, rotación, reuso, vencido, carrera, logout idempotente. |
| `Application.UnitTests/Bookings/BookingTenantIsolationTests` | Los 8 handlers rechazan la reserva de otro y no guardan nada. |
| `Infrastructure.IntegrationTests/MongoIdentityRepositoriesTests` | Username y correo únicos (con su código propio), usuarios antiguos sin correo que no chocan entre sí y no impiden crear el índice,  contador atómico bajo concurrencia, bloqueo, rotación atómica (8 en paralelo → 1 gana), revocación por familia. |
| `Api.IntegrationTests/RegistrationApiTests` | Registro → 201 con sesión válida; login posterior; **solo se guarda el hash**; usuario o correo ocupado (también con otras mayúsculas) → 409 con su código; 4 registros simultáneos → 1; cada campo inválido → 422 con su código; límite por IP; cada usuario es su propio tenant; registro cerrado → 403 y el login sigue funcionando. |
| `…/AuthenticationApiTests`, `AccessTokenValidationApiTests`, `TenantIsolationApiTests` | Login, bloqueo, límite por IP, rotación, reuso, logout, token alterado/otra clave/vencido/otra audiencia o emisor/`alg: none`, 401 sin token, 404 en las 8 operaciones sobre reservas ajenas. |

App (`cd mobile && npm run verify`): `forms/__tests__/registerForm.test.ts` y `loginForm.test.ts` (reglas y límites), `hooks/__tests__/useRegisterForm.test.tsx` y `useLoginForm.test.tsx`, `components/__tests__/AuthPanel.test.tsx` (alternar login/crear cuenta, mensajes de validación), `KeyboardAwareScreen.test.tsx` (fija `behavior="padding"` y el manejo de toques), `session/__tests__/sessionManager.test.ts` (registrar, restaurar, renovar, una sola renovación, 401 vs. red, cerrar sesión), `api/__tests__/httpClientAuth.test.ts`, `authApi.test.ts` y los mensajes de error.

---

## 18. Límites conocidos y siguientes pasos

1. ~~**Sin recuperación de contraseña.**~~ **Resuelta** (§22), solo para cuentas con el correo verificado. El teléfono se pide y se guarda, pero **no se verifica**.
2. ~~**`/api/maintenance/run` es trabajo global**~~ **Resuelto:** exige el rol de sistema (§19). Queda que el rol es un booleano en el usuario, sin gestión de roles más fina.
3. ~~**Un atacante que conozca tu usuario puede bloquearte**~~ **Mitigado** (PR del bloqueo por dirección). El bloqueo es por (cuenta, dirección): quien solo conoce el usuario bloquea su propia dirección, no a ti. Queda el caso **distribuido**: quien controle al menos 4 direcciones (o un /64 distinto cada una) puede llegar a los 20 fallos y bloquear la cuenta 15 minutos para todos; la salida del dueño es **recuperar la contraseña por correo** (§22), que quita todos los bloqueos. Detrás del NAT de una red móvil, varias personas comparten dirección y por tanto su cuenta de fallos: es el costo de contar por dirección. No se hizo un registro de "direcciones conocidas" del dueño que se salte el bloqueo de la cuenta.
4. **El access token no se puede revocar antes de tiempo** (hasta 15 min tras cerrar sesión). Es el precio de no consultar la base en cada petición.
5. **La IP real depende de `X-Forwarded-For`.** Se confía en el último salto del proxy; si el contenedor fuera alcanzable sin pasar por Railway, alguien podría falsear la IP y evitar los límites *por IP* (no el bloqueo de usuario).
6. ~~**Sin cambio de contraseña desde la app.**~~ **Resuelto** (§19). La *recuperación* si se olvida se resolvió en §22.
7. **Un usuario = un fotógrafo = un tenant.** Para estudios con varios fotógrafos habría que separar "cuenta" de "tenant" y añadir roles.
8. **Cerrar sesión sin conexión** deja vivo el refresh token en el servidor hasta que expire (30 días), aunque el teléfono ya lo olvidó.
9. **Rotar `JWT_SIGNING_KEY`** invalida los access tokens vigentes (la app los renueva sola); no invalida los refresh tokens.
10. ~~**Pendiente (otro mecanismo):** acceso del **cliente** al portal con token de enlace (guardado como hash) y OTP.~~ **Resuelto** en el backend con un mecanismo aparte (token de enlace guardado como hash y código por correo; ver `ClientPortal.md`). La verificación de correo (§20) y la recuperación de contraseña (§22) **del fotógrafo** ya están hechas.

---

## 19. Rol de sistema y cambio de contraseña

### 19.1 Rol de sistema (`isSystemOperator`)

- El usuario tiene un booleano `isSystemOperator` (documento `users`, ausente = `false`). **El registro nunca lo pone**: se otorga a mano con `backend/scripts/grant-system-role.js` (mongosh; `revoke = true` lo quita).
- Si es `true`, el access token lleva `role: "system"` (`JwtAccessTokenIssuer`). La política `system-operator` (`PhotographerAuthenticationExtensions.SystemOperatorPolicy`) exige ese claim y la usa `/api/maintenance/run`. Un usuario normal recibe **403**.
- El cambio del rol llega con el siguiente access token (máx. 15 min o al volver a entrar).
- La respuesta de sesión incluye `isSystemOperator` solo para que la app muestre u oculte el botón "Liberar reservas vencidas"; la API no confía en eso, valida el token.
- **Orden del middleware:** `UseRateLimiter` va **después** de `UseAuthorization`. Antes, una petición rechazada por falta de rol consumía el único cupo por minuto del mantenimiento y cualquier usuario podía dejar al operador sin poder usarlo.
- Para dar el rol en local: `docker exec -i photostudio-mongo mongosh photostudio_dev --eval "const username='tu.usuario'; $(cat backend/scripts/grant-system-role.js)"`.

### 19.2 Cambio de contraseña

`POST /api/account/password` con `{ currentPassword, newPassword }` (Bearer):

1. Carga al usuario del token; si la cuenta está bloqueada, o esta dirección lo está para ella → 429 `auth.account_locked`.
2. Verifica la contraseña actual. Si es incorrecta cuenta como **intento fallido de login** (mismos contadores y bloqueos que el login: por dirección y de la cuenta) y responde **422** `user.invalid_current_password` (no 401: la sesión es válida y un 401 haría que la app intentara renovarla).
3. Valida la nueva (8–128 caracteres, distinta de la actual → `user.weak_password`).
4. Reemplaza el hash **solo si el hash guardado sigue siendo el verificado** (`ChangePasswordAsync`, condicional): dos cambios simultáneos no pisan; el segundo recibe 409 `concurrency.conflict`. También limpia fallos y bloqueo.
5. Revoca **todos** los refresh tokens del usuario (`RevokeAllForUserAsync`) y emite una sesión nueva (familia nueva) para este dispositivo, que la app adopta.

Límites: los access tokens de otros dispositivos siguen siendo válidos hasta 15 min (§18.4). Si la respuesta se pierde después de que el servidor cambió la contraseña, el dispositivo queda con un refresh token revocado y tendrá que entrar con la contraseña nueva.

Pruebas: `ChangePasswordHandlerTests` (unitarias), `AccountApiTests` y `AccessTokenValidationApiTests` (de punta a punta, contra Mongo real).


---

## 20. Verificación del correo

Una cuenta nueva tiene que **demostrar que es dueña del correo** con un código que llega a esa dirección, antes de usar la app. Antes, cualquiera podía registrarse con el correo de otra persona. El mecanismo es el del código del portal del cliente (§ de `ClientPortal.md`), aplicado a la cuenta.

### Flujo

1. `POST /api/auth/register` crea la cuenta (con `emailVerifiedAt` vacío) y **envía el código** de 6 dígitos al correo. Si el correo falla (Resend caído o sin configurar), la cuenta se crea igual y se registra una advertencia: el usuario pide otro código desde la app.
2. La sesión que devuelve el registro (y la de `login` y `refresh`) trae `emailVerified: false`, y el token de acceso lleva el claim **`email_verified: "false"`**.
3. Con ese token la API **solo** acepta dos endpoints, bajo la política `any-user`:
   - `POST /api/account/email/verify` `{ code }` → 200 con una **sesión nueva** (`emailVerified: true`); 422 `auth.invalid_email_code` si el código es malo, venció o se gastó (la respuesta no dice cuál).
   - `POST /api/account/email/resend` → 202 `{ maskedEmail, expiresAt }`; 429 `auth.email_code_rate_limited` (con `Retry-After`); 409 `auth.email_already_verified` o `auth.email_missing`; 503 `email.delivery_failed`.
   - `POST /api/account/email/change` `{ email }` → 202 `{ maskedEmail, expiresAt }`: reemplaza el correo y envía el código a la dirección nueva (ver "Corregir un correo mal escrito" abajo); 409 `auth.email_already_verified` / `user.email_taken`; 422 `user.invalid_email`; 429 `auth.email_code_rate_limited`; 503 `email.delivery_failed`.
4. Todo lo demás responde **403** a ese token: la política por defecto y la de respaldo exigen que el token no diga `email_verified: false`, y `system-operator` también. Los endpoints anónimos (`/health`, `/api/auth/*`, `/public/*`) no cambian.
5. Verificar cambia lo que el token puede hacer, así que la respuesta es una sesión nueva y se **cierran las demás sesiones** de la cuenta (como al cambiar la contraseña). La app la adopta y se abre.

### Reglas del código (`EmailVerification`, colección `email_verifications`)

Las mismas del portal: 6 dígitos, vive 10 minutos, **un solo uso**, hash HMAC con la clave de firma y el usuario (nunca el código), 5 intentos con un `$inc` atómico **antes** de comparar, un minuto entre envíos y 5 por hora. Un código nuevo reemplaza al anterior. Se borra solo una hora después de vencer (índice TTL). Es independiente de los códigos del portal: otra colección, otra excepción de límite (`auth.email_code_rate_limited`).

### Decisiones y límites

- **Interruptor `AUTH_EMAIL_VERIFICATION_REQUIRED`** (solo la API; por defecto `true`). Con `false` nadie queda bloqueado y el registro no envía código: es la salida si el proveedor de correo falla y nadie puede verificar. Un valor que no sea `true`/`false` detiene la API al arrancar, como `AUTH_REGISTRATION_ENABLED`.
- **Cuentas antiguas.** Una cuenta creada antes de esto no tiene `emailVerifiedAt`, así que **se le pide verificar en su próximo inicio de sesión**. Una cuenta sin correo (de antes de que existiera el campo) no puede recibir un código y **no se bloquea**.
- **Tokens ya emitidos.** Un token sin el claim (vive como máximo 15 minutos) se acepta, para que desplegar no corte las sesiones abiertas. Al renovarlo trae el claim.
- **La app** decide sus pantallas con `emailVerified` de la sesión (`app/verify-email.tsx`: código, "Enviar un código nuevo" con espera de 60 s y "Salir"); mientras sea `false` solo existe esa pantalla y el registro del push no corre.
- **Tampoco se resuelve** que alguien ocupe el correo de otra persona sin poder usarlo (el índice único sigue protegiéndolo; la dueña real no puede registrarse con él hasta que el ocupante lo cambie o se borre la cuenta).

### Corregir un correo mal escrito

Quien se equivoca al escribir el correo al registrarse no recibe el código y, sin esto, quedaba atrapado. `POST /api/account/email/change` (política `any-user`, 10 por minuto por IP) con `{ email }`:

1. Normaliza el correo (422 `user.invalid_email`) y carga al usuario. Si **ya verificó** → 409 `auth.email_already_verified`: una vez verificado, el correo es una prueba de identidad y cambiarlo no se hace solo con una sesión (queda pendiente un flujo con contraseña y código a ambas direcciones, si alguna vez hace falta).
2. **Revisa los límites del código antes de tocar nada** (un minuto entre códigos, cinco por hora, los contadores de `email_verifications`). Si no puede enviar, responde 429 y **el correo no cambia**. El cambio no reinicia esos contadores, así que no sirve para mandar correo a muchas direcciones a través del servicio. Costo: justo tras registrarse hay que esperar hasta un minuto para corregir (la app muestra la espera).
3. Si la dirección es la misma que ya tiene, no escribe nada y solo reenvía el código.
4. Reemplaza el correo con una escritura condicionada a `emailVerifiedAt == null` (`ChangeUnverifiedEmailAsync`): un correo que se verifica entre la lectura y la escritura nunca se pisa (409). El índice único decide si otra cuenta usa la dirección (409 `user.email_taken`, el mismo dato que ya revela el registro).
5. Envía el código nuevo **a la dirección nueva**; el registro del código se reemplaza, así que el código enviado a la dirección equivocada deja de servir. No toca sesiones: el token no lleva el correo.

También sirve a una cuenta antigua sin correo: puede ponerse uno y verificarlo. La app lo ofrece en la pantalla de verificación ("¿Te equivocaste de correo? Cámbialo", con la dirección actual para editar). Pruebas: `ChangeUnverifiedEmailHandlerTests` (9) y 4 de punta a punta en `EmailVerificationApiTests`.
- La recuperación de contraseña se apoya en este correo verificado (§22).

## 21. La web del fotógrafo (cookie y segundo factor)

La web del fotógrafo (`/studio` del sitio, PR 3 y 3b de `docs/Gallery.md`) es la **única parte de la API que un navegador usa con una cookie**, y por eso es la única expuesta a que una página maliciosa haga una petición con la cookie del usuario. Está construida aparte de la app y se defiende en capas. Nada de lo de las secciones anteriores cambió: la app sigue con su JWT.

### 21.1 Cómo se entra

1. `POST /api/web/auth/login` con usuario y contraseña. Se comprueban **exactamente igual que en la app** (`CredentialChecker`: mismo tiempo de hash para un usuario que no existe, mismo mensaje, **mismo bloqueo de 15 min tras 5 fallos**; los intentos de un lado cuentan en el otro).
2. La cuenta debe tener el **correo verificado**: el segundo factor va a ese correo. Si no, 403 `auth.web_requires_verified_email` y no se envía nada.
3. Si el navegador es uno **recordado** (cookie de dispositivo válida de ese usuario), entra sin código: 200 y cookie de sesión. La contraseña se pide siempre; la cookie de dispositivo solo salta el código.
4. Si no, se envía un **código de 6 dígitos** al correo y se responde 202 (`CodeRequired`) con una cookie de desafío. El código vive 10 minutos, sirve una vez, admite 5 intentos (contados con `$inc` atómico **antes** de comparar), y no se puede pedir otro antes de 60 s ni más de 5 por hora. Se guarda solo su hash (HMAC con la clave de firma), con un prefijo de propósito (`studio-login:`) para que nunca coincida con un código hecho para otra cosa.
5. `POST /api/web/auth/code/verify` con el código y, si el usuario quiere, `rememberDevice`. El código **solo vale desde el mismo navegador** que dio la contraseña (el secreto del desafío viaja en una cookie): el código correcto enviado desde otro navegador se rechaza. Un código malo, vencido o gastado responde siempre lo mismo (422 `auth.invalid_login_code`).
6. `POST /api/web/auth/code/resend` pide otro código (mismos límites). La contraseña dada en el paso 1 vale solo durante la **hora de la ventana de envíos**: pasada, hay que empezar de nuevo, para que nadie mantenga vivo un desafío viejo pidiendo códigos.
7. `POST /api/web/auth/logout` cierra la sesión **en el servidor** (una copia robada de la cookie deja de servir) y, con `forgetDevice`, olvida el navegador. `GET /api/web/auth/session` dice quién está dentro (401 si nadie), para que la página decida al cargar.

### 21.2 La sesión y las cookies

- La sesión es un **secreto opaco de 256 bits** que se busca en el servidor en cada petición (colección `studio_sessions`, solo su hash SHA-256). No es un JWT: cerrar sesión, cambiar la contraseña o bloquear la cuenta surten efecto **al instante**. Dura **2 horas sin uso** y **12 horas como máximo**, se use o no. Una cuenta bloqueada, sin correo verificado o que ya no existe cierra su sesión en el acto.
- Las cookies (`ps-session`, `ps-device`, `ps-challenge`) son todas **`HttpOnly`** (la página no las lee: un script inyectado no puede robarlas), **`Secure`**, **`SameSite=Strict`** (el navegador solo las manda desde nuestro sitio), de **ruta `/`, sin `Domain`** y con el prefijo **`__Host-`** (el navegador rechaza que otro subdominio o una conexión http las fije). Solo en Development, sobre http en esta computadora, pierden `Secure` y el prefijo (el navegador no las guardaría). En cualquier otro ambiente, una petición insegura nunca recibe una cookie insegura.
- **Funciona porque el sitio y la API comparten el dominio raíz** (`develop.photosensestudio.com` y `develop-api.photosensestudio.com` son el mismo *site*): `SameSite=Strict` no bloquea las llamadas entre ellos. Si el sitio y la API se separaran en dominios distintos, este diseño dejaría de funcionar y habría que repensarlo.

### 21.3 El navegador recordado (30 días)

La cookie de dispositivo (colección `studio_devices`) solo salta el código, nunca la contraseña, y **solo vale para el usuario que la creó**. Su secreto **cambia en cada uso** (rotación atómica: de dos peticiones con la misma cookie solo una rota; la otra encuentra el hash viejo ya cambiado), así que una cookie copiada sirve a lo sumo una vez. Cada uso extiende los 30 días. Se olvida al cerrar sesión con `forgetDevice`, al cambiar la contraseña y a los 30 días sin uso (índice de expiración).

### 21.4 Qué puede hacer la sesión web

Solo lo que está en `/api/web`: iniciar y cerrar sesión, y **leer sus galerías** (`GET /api/web/galleries` y `/{id}`). La política `studio-web` consulta **solo** el esquema de cookie y exige el *claim* `scope=studio`, que ningún token de la app tiene; y el resto de la API consulta solo los esquemas Bearer, así que **la cookie no abre nada fuera de `/api/web` y el token de la app no abre `/api/web`** (hay pruebas de las dos cosas). La web muestra menos que la app (el correo va enmascarado, sin clientes ni pagos).

### 21.5 Amenazas consideradas

| Amenaza | Defensa |
|---|---|
| CSRF (otra página hace una petición con la cookie) | `SameSite=Strict`; CORS con credenciales solo para el sitio; toda petición que cambia algo **debe traer `Origin` igual al del sitio** (si no, 403 `request.forbidden_origin` antes de leer nada); cuerpo JSON (obliga a *preflight*) |
| XSS roba la sesión | Cookies `HttpOnly`; sin tokens en el navegador; el sitio no carga scripts de terceros |
| Contraseña robada | Segundo factor por correo en cada navegador nuevo; la cookie de desafío ata el código al navegador |
| Cookie de sesión copiada | Se busca en el servidor: cerrar sesión, cambiar la contraseña o bloquear la cuenta la matan; 2 h sin uso y 12 h máximo |
| Cookie de dispositivo copiada | Rota en cada uso; solo salta el código, no la contraseña; solo de su usuario |
| Fuerza bruta de la contraseña | El mismo bloqueo que la app (por cuenta y dirección, §18.3) y 10 peticiones por minuto por IP |
| Fuerza bruta del código | 5 intentos por código contados de forma atómica; el código vive 10 min; un millón de valores, con la clave del servidor |
| Enumeración de usuarios | La misma respuesta y el mismo tiempo para usuario inexistente y contraseña mala |
| Código usado desde otro navegador | Se rechaza: va atado a la cookie de desafío |
| Reuso de un código | Se gasta con una eliminación atómica: de dos peticiones solo una gana |
| Otro subdominio de `photosensestudio.com` comprometido (es del mismo *site*, así que `SameSite` no lo frena) | El `Origin` debe ser **exactamente** el del sitio, no solo del mismo dominio raíz; CORS con credenciales solo para ese origen; cookies con prefijo `__Host-` (ningún subdominio puede fijarlas ni pisarlas) |
| Respuestas en caché | Todo lo de `/api/web` responde `Cache-Control: no-store` |
| La base de datos se filtra | Solo hashes de secretos y de códigos; los códigos con HMAC y clave |

### 21.6 Datos y configuración

Colecciones `studio_sessions`, `studio_devices` y `studio_challenges` (esta con el usuario como `_id`, una por usuario), con índices únicos por hash y de expiración automática (TTL). Sin variables nuevas: el origen permitido es `PORTAL_BASE_URL` (y, en Development, `localhost:5173`); las `PORTAL_ORIGINS` extra del portal **no** reciben credenciales. Cambiar la contraseña desde la app **cierra las sesiones web, olvida los navegadores recordados y borra el código pendiente**.

### 21.7 Lo que no se hizo
- Ver o cerrar desde la app las sesiones web abiertas.
- App autenticadora (TOTP): el segundo factor es el correo, que protege contra la contraseña robada pero no contra el correo robado.
- Recuperación de contraseña desde la web: la recuperación existe en la app (§22) y la web no tiene su propia pantalla.
- El correo con el código es de texto simple, sin la imagen de marca; lo unificará el PR de los correos de la galería.

## 22. Recuperar una contraseña olvidada

Quien olvidó su contraseña elige una nueva con un **código de 6 dígitos que llega al correo verificado de la cuenta**. Son dos endpoints públicos (`/api/auth/*`, sin token):

### 22.1 Pedir el código: `POST /api/auth/password-reset/request`

Cuerpo `{ identifier }` (usuario **o** correo; un `@` lo hace correo). Responde **siempre 202 sin cuerpo**, exista o no la cuenta, tenga o no el correo verificado, se haya enviado o no el correo, esté o no en espera. Así no sirve para averiguar qué usuarios o correos están registrados. Solo se envía si:

- la cuenta existe **y su correo está verificado** (una dirección que nadie probó no prueba quién es la persona: quien escribió mal su correo en el registro no puede recuperar por ahí, y por eso §20 permite corregirlo);
- no se envió otro código hace menos de un minuto ni van cinco en la última hora (los mismos límites que el código de verificación, **en silencio**: un 429 aquí diría que la cuenta existe).

Un fallo del proveedor de correo se registra como advertencia y tampoco se le dice a quien llama (un fallo que solo ocurre con cuentas reales las delataría). Además hay un límite **por dirección IP: 8 solicitudes por hora** (`PasswordResetPolicy`, 429 `rate_limit.exceeded`), porque el endpoint envía correo a una dirección que no es la de quien llama.

### 22.2 Elegir la contraseña: `POST /api/auth/password-reset/confirm`

Cuerpo `{ identifier, code, newPassword }` → **204**. El orden importa:

1. Valida la contraseña nueva (8 a 128 caracteres, 422 `user.weak_password`) y que el código sean 6 dígitos, **antes** de contar ningún intento: una contraseña corta no gasta una oportunidad.
2. Busca la cuenta con la misma regla de arriba. Cuenta desconocida, sin correo verificado, sin código, código vencido, gastado o equivocado → **el mismo 422 `auth.invalid_reset_code`**, que no dice cuál fue la causa.
3. Cuenta el intento con un `$inc` atómico **antes** de comparar; el quinto intento fallido borra el código. Un código sirve una sola vez y vive 10 minutos.
4. Cambia el hash con `ChangePasswordAsync` condicionado al hash que se leyó (dos confirmaciones simultáneas no se pisan; la segunda recibe 409 `concurrency.conflict`). Eso también **limpia el contador de fallos y el bloqueo de 15 minutos** del login: recuperar la cuenta quita el bloqueo (y es la salida para quien olvidó su contraseña y se bloqueó probándolas).
5. Borra el código, **revoca todos los refresh tokens**, corta la web del fotógrafo (sesiones, navegadores recordados y código pendiente) y envía un **aviso por correo** de que la contraseña cambió ("si no fuiste tú…"; si falla, se registra y no se deshace nada).

**No inicia sesión**: no devuelve ninguna sesión; la persona entra con la contraseña nueva. Los access tokens ya emitidos siguen valiendo hasta 15 minutos (§18.4).

### 22.3 Decisiones y límites

- **Colección propia `password_resets`** (un documento por usuario, índice TTL de 1 hora tras vencer) y hash HMAC del código con el prefijo `password-reset:`, aparte del código de verificación del correo: un código hecho para un fin no sirve para el otro.
- 1 000 000 de códigos posibles con 5 intentos por código y 5 códigos por hora y cuenta (25 intentos por hora): la probabilidad de acertar a ciegas es del orden de 0,0025 % por hora, y cada pedido de código le llega al dueño real del correo.
- Quien conozca el usuario o el correo de otra persona puede **hacerle llegar hasta 5 correos por hora** con el código (es el costo de no revelar si la cuenta existe); no puede usarlos sin el correo.
- Si alguien controla el correo de la cuenta, controla la cuenta: es el modelo de casi toda recuperación por correo. El aviso de "tu contraseña cambió" existe para que el dueño lo note.
- **Cuentas con el correo sin verificar** no pueden recuperar por correo. Con `AUTH_EMAIL_VERIFICATION_REQUIRED=false` hay cuentas que usan la app sin verificar, y esas tampoco: se sigue necesitando un operador (§14).
- Pruebas: `PasswordResetCodeTests` (dominio), `PasswordRecoveryHandlerTests` (27 casos) y `PasswordRecoveryApiTests` (9, de punta a punta: flujo completo, código de un solo uso, cuenta inexistente que responde igual, cuenta sin verificar, cinco intentos, contraseña débil, espera, desbloqueo y código de otra cuenta).
- **La app:** en el inicio de sesión, "¿Olvidaste tu contraseña?" abre dos pasos (`RecoverPasswordForm`, `useRecoverPasswordForm`): la cuenta, y después el código y la contraseña nueva dos veces, con "Enviar un código nuevo" (espera de 60 s) y "Usar otra cuenta". Al terminar vuelve al inicio de sesión con un aviso. El mensaje tras pedir el código no afirma que la cuenta exista.

## 23. Límite de intentos por cuenta y dirección

Resumen de cómo se limitan las contraseñas adivinadas (la mecánica está en §6 y §18.3):

| Nivel | Se cuenta | Umbral | Efecto | Se borra con |
|---|---|---|---|---|
| Dirección | Fallos seguidos de una cuenta desde una dirección (`login_failures`, un documento por cuenta y dirección) | 5 | Esa dirección queda bloqueada **para esa cuenta** 15 min (429 `auth.account_locked` con `Retry-After`) | Un login correcto desde esa dirección; cambiar o recuperar la contraseña; vence solo un día después (índice TTL) |
| Cuenta | Fallos seguidos de la cuenta entre todas las direcciones (`users.failedLoginAttempts`) | 20 | La cuenta se bloquea para todas las direcciones 15 min | Un login correcto; cambiar o recuperar la contraseña |
| Dirección (red) | Todas las peticiones de login | 10 por minuto | 429 `rate_limit.exceeded` | Pasa el minuto |

- **La dirección** viene de `X-Forwarded-For` (último salto, como el resto del límite por IP), las IPv6 se agrupan por /64 y se guarda como HMAC con la clave de firma ligado a la cuenta (`login-address:` + dirección): la base de datos no guarda direcciones IP. Una petición sin dirección conocida cae en una cubeta común.
- **Un intento de una dirección bloqueada** se rechaza antes de mirar la contraseña y **no se cuenta**: por eso una sola dirección nunca llega a los 20 de la cuenta.
- **Los access tokens siguen durando 15 minutos y no se revocan antes** (decisión del 2026-11-05: revocarlos exigiría consultar la base en cada petición; el riesgo ya lo acota la vida corta y la rotación del refresh token; ver §18.4).
- Pruebas: `LoginHandlerTests` y `ChangePasswordHandlerTests` (unitarias), `MongoLoginFailureRepositoryTests` (conteo paralelo, bloqueo, aislamiento por cuenta y dirección, índices), `ClientAddressTests` y `LoginLockoutApiTests` (de punta a punta: un extraño no bloquea al dueño, varias direcciones sí bloquean la cuenta, insistir bloqueado no la bloquea, IPv6 de la misma red, cambio de contraseña).
