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
4. si el usuario está bloqueado ahora:
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

- **No se revela si el usuario existe.** Usuario inexistente, usuario con formato inválido y contraseña incorrecta responden con el mismo estado, el mismo `code` y el mismo `detail`. El caso "usuario desconocido" ejecuta además una verificación de hash señuelo (`SpendVerificationTime`) para que el **tiempo de respuesta** tampoco lo delate. (El *registro* sí revela que un nombre está ocupado: es inherente a poder elegir nombre.)
- **El contador de fallos es atómico.** Si se cargara el usuario, se incrementara y se guardara con concurrencia optimista, un atacante que enviara 100 intentos en paralelo haría que casi todas las escrituras chocaran y el contador apenas avanzara, burlando el bloqueo. Con `$inc` cada intento cuenta (hay una prueba con 25 intentos paralelos que exige que el contador llegue a exactamente 25).
- **Bloqueo temporal, no permanente:** 5 fallos seguidos bloquean 15 minutos.
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

> **Importante con el registro abierto:** `POST /api/maintenance/run` exige sesión pero ejecuta trabajo **global** (expira las reservas vencidas de *todos* los usuarios). Como ahora cualquiera puede crear una cuenta, cualquiera puede dispararlo. Es idempotente, de duración acotada y limitado a 1 llamada por minuto, así que el daño posible es una carga leve, pero conviene restringirlo a un rol de sistema (ver §18).

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
| `failedLoginAttempts`, `lockedUntil` | Fallos seguidos y fin del bloqueo. |

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

`signOut()` olvida la sesión en el dispositivo **de inmediato** y después avisa al servidor para revocar la familia, en modo "mejor esfuerzo": sin conexión igual se cierra la sesión en el teléfono, y el refresh token del servidor expira solo a los 30 días.

### 11.7 El teclado ya no tapa los campos

**Síntoma:** al tocar un campo del login, el teclado cubría la mitad de la pantalla y tapaba donde se escribe.

**Causa probable:** la app se dibuja de borde a borde en Android (*edge-to-edge*), por lo que el sistema **ya no reduce la ventana** cuando aparece el teclado, y el formulario anterior solo se ajustaba en iOS (`KeyboardAvoidingView` con `behavior` indefinido en Android).

**Arreglo:** el nuevo componente `KeyboardAwareScreen` (usado por el acceso) envuelve el contenido en un `KeyboardAvoidingView` con `behavior="padding"` **en ambas plataformas**, de modo que el área se encoge al espacio libre sobre el teclado, y dentro un `ScrollView` que permite desplazarse si el formulario no cabe (el de crear cuenta tiene cinco campos), con `keyboardShouldPersistTaps="handled"` (los toques siguen funcionando con el teclado abierto) y `keyboardDismissMode="on-drag"` (arrastrar cierra el teclado). Hay pruebas que fijan esas propiedades para que no se pierdan sin querer.

> Esto no se pudo comprobar en un dispositivo desde el entorno de desarrollo del asistente: las pruebas verifican la configuración del componente, no el comportamiento visual del teclado. Si algún formulario sigue quedando tapado, indica el modelo/sistema del teléfono. Los formularios de nueva reserva y de motivo tienen el mismo patrón anterior (ajuste solo en iOS) y podrían adoptar `KeyboardAwareScreen`.

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

> **Si olvidas tu contraseña no hay recuperación automática** (no hay correo configurado). Opciones: crear otra cuenta y pasarle tus reservas con `migrate-tenant.js` (`fromId` = tu `users._id` anterior), o, con acceso a Mongo, borrar tu documento de `users` y volver a registrarte. Es una limitación asumida; ver §18.

---

## 15. Contrato de la API

| Método | Ruta | Auth | Cuerpo | Respuestas |
|---|---|---|---|---|
| POST | `/api/auth/register` | Pública (5/hora/IP) | `{ username, email, password, name, phone }` | **201** sesión · 409 `user.username_taken` / `user.email_taken` · 422 `user.invalid_username` / `user.invalid_email` / `user.invalid_name` / `user.invalid_phone` / `user.weak_password` · 403 `auth.registration_disabled` · 429 `rate_limit.exceeded` |
| POST | `/api/auth/login` | Pública (10/min/IP) | `{ username, password }` | 200 sesión · 401 `auth.invalid_credentials` · 429 `auth.account_locked` (+`Retry-After`) · 429 `rate_limit.exceeded` |
| POST | `/api/auth/refresh` | Pública (10/min/IP) | `{ refreshToken }` | 200 sesión nueva · 401 `auth.invalid_refresh_token` · 429 `rate_limit.exceeded` |
| POST | `/api/auth/logout` | Pública (10/min/IP) | `{ refreshToken }` | 204 siempre |
| `*` | `/api/bookings/**`, `/api/maintenance/run` | **Bearer** | (sin `photographerId`) | 401 sin token o token inválido · 404 si la reserva es de otro usuario |

---

## 16. Amenazas consideradas

| Amenaza | Defensa | Verificación |
|---|---|---|
| Crear cuentas en masa (spam, llenar la base) | 5 registros/hora/IP e interruptor `AUTH_REGISTRATION_ENABLED` para cerrarlo | Prueba del límite por IP y de registro cerrado → 403. **Riesgo residual:** sin verificación de correo, un atacante con muchas IPs puede crear cuentas |
| Dos personas toman el mismo usuario o correo a la vez | Índices únicos `ix_user_username` e `ix_user_email` (no "buscar y luego insertar") | Prueba con 4 registros paralelos: 1 creado, 3 conflictos; prueba de correo repetido con otras mayúsculas |
| Descubrir qué correos están registrados | **No se evita:** el registro dice `user.email_taken`, igual que dice `user.username_taken`; es inherente a exigir unicidad. El login sí es opaco (no distingue usuario inexistente de contraseña mala) y no acepta el correo para entrar | — |
| Adivinar contraseñas (fuerza bruta) | Bloqueo (5 fallos → 15 min) con contador atómico + 10 intentos/min por IP | Pruebas de bloqueo, de 25 intentos paralelos y de límite por IP |
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

1. **Sin recuperación de contraseña ni verificación del correo.** El correo y el teléfono se piden y se guardan, pero **no se verifican**: nada envía mensajes todavía (Resend no está configurado y no hay dominio). Así que alguien puede registrarse con el correo de otra persona y **ocupar esa dirección**. El correo está listo para ser la vía de recuperación de contraseña, pero solo será fiable cuando se verifique con un código enviado a esa dirección. Hasta entonces, cerrar el registro en producción (`AUTH_REGISTRATION_ENABLED=false`) una vez creada tu cuenta es la mitigación.
2. **`/api/maintenance/run` es trabajo global** y ahora cualquier usuario registrado puede dispararlo. Restringirlo a un rol de sistema (o moverlo al worker) antes de abrir el producto a más gente.
3. **Un atacante que conozca tu usuario puede bloquearte** el login 15 minutos enviando contraseñas malas (el bloqueo es por usuario). Mitigación futura: bloquear por (usuario, IP) o retardos progresivos.
4. **El access token no se puede revocar antes de tiempo** (hasta 15 min tras cerrar sesión). Es el precio de no consultar la base en cada petición.
5. **La IP real depende de `X-Forwarded-For`.** Se confía en el último salto del proxy; si el contenedor fuera alcanzable sin pasar por Railway, alguien podría falsear la IP y evitar los límites *por IP* (no el bloqueo de usuario).
6. **Sin cambio de contraseña desde la app.**
7. **Un usuario = un fotógrafo = un tenant.** Para estudios con varios fotógrafos habría que separar "cuenta" de "tenant" y añadir roles.
8. **Cerrar sesión sin conexión** deja vivo el refresh token en el servidor hasta que expire (30 días), aunque el teléfono ya lo olvidó.
9. **Rotar `JWT_SIGNING_KEY`** invalida los access tokens vigentes (la app los renueva sola); no invalida los refresh tokens.
10. **Pendiente (otro mecanismo):** acceso del **cliente** al portal con token de enlace (guardado como hash) y OTP.
