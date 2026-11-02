# A5 — Seguridad y autenticación

"Seguridad primero" es regla permanente del proyecto. Este documento resume cómo entra cada persona, qué se guarda y qué amenazas se consideraron. El detalle está en `docs/Authentication.md` y `docs/ClientPortal.md`.

## 1. Las cuatro formas de entrar

| Quién | Cómo | Credencial | Alcance |
|---|---|---|---|
| **Fotógrafo en la app** | Usuario + contraseña | JWT de 15 min + token de renovación rotativo | Toda la API del fotógrafo (`/api/**`) |
| **Fotógrafo en la web** | Usuario + contraseña + código por correo | Cookie de sesión `HttpOnly; Secure; SameSite=Strict` | Solo galerías (`/api/web/**`) |
| **Cliente** | Enlace secreto + código por correo | JWT de portal (30 días por defecto) | Solo la reserva de su enlace (`/public/bookings/{token}/**`) |
| **Operador de sistema** | Cuenta con rol de sistema | Claim `role=system` | `POST /api/maintenance/run` |

Un endpoint nuevo es **protegido por defecto** (`FallbackPolicy = RequireAuthenticatedUser`); solo es público si lo declara `AllowAnonymous` (hoy: `/health/*`, `/openapi/*`, `/api/auth/*` y el portal con su propia política). Las credenciales de una superficie **no abren** las otras: el token de la app no abre `/api/web`, la cookie web no abre nada fuera de `/api/web`, y el JWT de portal no abre la API del fotógrafo.

## 2. Fotógrafo en la app

- **Contraseña:** 8 a 128 caracteres; se guarda solo el hash (PBKDF2 del hasher de ASP.NET Core); no se puede recuperar.
- **Usuario:** 3–30 caracteres `a-z 0-9 . - _`, único sin distinguir mayúsculas; el correo también es único (índices, no "buscar y luego insertar").
- **Bloqueo:** 5 fallos seguidos bloquean al usuario 15 minutos. El contador es atómico (`$inc`), así que no se evade con intentos en paralelo. Un usuario desconocido tarda lo mismo y responde igual que una contraseña mala (no se puede averiguar qué usuarios existen).
- **JWT:** HS256 con emisor, audiencia y firma verificados; solo se acepta ese algoritmo. Vida de 15 minutos.
- **Renovación:** token de 256 bits aleatorios, de **un solo uso**; en Mongo solo su hash, agrupado por *familia* de login. Si alguien reusa un token ya usado (señal de robo), se revoca toda la familia.
- **Verificación del correo:** al registrarse llega un código; hasta verificarlo, el token lleva `email_verified: false` y la API responde 403 a todo salvo verificar o reenviar.
- **Aislamiento entre fotógrafos:** el `PhotographerId` sale **solo** del claim `sub`; toda operación sobre una reserva o galería verifica que sea suya, y la ajena responde **404** (igual que una inexistente).
- **Almacenamiento en el teléfono:** el access token solo en memoria; el refresh token en `expo-secure-store` (Keychain/Keystore).
- **Registro abierto:** `AUTH_REGISTRATION_ENABLED=false` lo cierra. Con el registro abierto, cualquiera puede crear cuentas (de ahí la verificación del correo).

## 3. Fotógrafo en la web

- Misma cuenta, **correo verificado obligatorio**.
- Contraseña, y en un navegador no recordado, un **código de 6 dígitos por correo** atado a ese navegador con una cookie; "recordar" salta el código 30 días con una cookie que rota en cada uso (nunca salta la contraseña).
- Sesión = secreto opaco de 256 bits buscado en el servidor (`studio_sessions`, solo el hash): 2 horas sin uso, 12 horas máximo.
- Cambiar la contraseña cierra todas las sesiones web.
- Defensas contra CSRF: `SameSite=Strict` (el sitio y la API comparten dominio raíz), toda petición que cambia algo exige `Origin` igual al del sitio (403 `request.forbidden_origin`), respuestas `no-store`, y CORS con credenciales solo para el sitio.
- La política de contenido (CSP) del sitio solo permite scripts propios y conexiones a la API y a R2.

## 4. Cliente (portal)

- **El enlace** (`/b/{token}`) es un token opaco de 256 bits; en Mongo solo su hash. Uno activo por reserva: crear otro revoca el anterior. Token inválido, vencido o revocado = el mismo 404 (no revela cuál).
- **El código** (6 dígitos, 10 minutos, un uso, 5 intentos contados con `$inc` atómico *antes* de comparar, hash HMAC) va **solo al correo que registró el fotógrafo**: el cliente nunca escribe la dirección, así nadie puede mandar el código a un correo propio. Límites: 60 segundos entre envíos y 5 por hora.
- **Doble factor real:** el enlace llega por WhatsApp y los códigos por correo; los correos de la galería no llevan enlace a propósito, para que quien lea un buzón no tenga las dos mitades.
- **La sesión** (JWT de portal) se guarda en el navegador del cliente (`localStorage`), por reserva. Sirve para contrato, comprobante y galería. Hay un botón "Cerrar sesión en este dispositivo". **No se puede revocar antes de vencer** (salvo cambiar la clave de firma), por eso la duración es configurable (`PORTAL_SESSION_DAYS`). La CSP `script-src 'self'` mitiga el robo por XSS.
- Límites por IP: lectura 60/min, código 10/min, acciones 10/min.

## 5. Fotos

- Bucket privado; todo acceso por URL firmada; las claves las construye el backend con identificadores aleatorios (el nombre original es una etiqueta, nunca parte de la clave).
- La API **no emite ninguna URL de descarga** con saldo distinto de cero; las URLs de descarga duran 5 minutos y pedirlas se cuenta como descarga. Una URL ya emitida no se puede revocar.
- Al registrar una subida, la API comprueba el objeto real y borra lo inválido (una URL firmada no puede limitar el tamaño).
- Lo que no se puede impedir: una captura de pantalla. Lo que protege es la baja calidad de las pruebas, la marca de agua grande al centro y que el original nunca se sube.
- La app solo borra archivos que estén en su propia caché; nunca la fototeca del fotógrafo.

## 6. Límites de uso (rate limiting)

Registro 5/hora por IP; login, renovación y cierre 10/min; verificación de correo 10/min; mantenimiento 1/min; subidas y acciones de galería 60/min; portal según arriba. Una petición rechazada responde 429 con `Retry-After` y código `rate_limit.exceeded`. `UseRateLimiter` va **después** de `UseAuthorization` a propósito: una petición rechazada por falta de rol no gasta el cupo compartido.

## 7. Dinero

Todo registro de pago exige una **clave de idempotencia**: un doble toque o un reintento registra el pago una sola vez. Un `Payment` nunca se edita ni se borra; se compensa con un reembolso.

## 8. Reglas para quien opera

- Los secretos solo en Railway y en el entorno `develop` de GitHub. Nunca en el repositorio (es público), en chats ni en capturas.
- Los tokens viejos se revocan: pendiente revocar el token `cfat_…` anterior en Cloudflare.
- Una clave `JWT_SIGNING_KEY` con `dev-only` se rechaza fuera de Development.
- No definir `API_DOCS_ENABLED` en producción.

## 9. Pendiente

Recuperación de contraseña y corrección de un correo mal escrito; verificación del teléfono; revocación de sesiones de portal antes de vencer (si se requiere).
