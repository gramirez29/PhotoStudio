# Portal del cliente (API)

Lo que el cliente hace a través del enlace de su reserva. Este documento cubre el **backend**; el portal web (React) y las
pantallas del fotógrafo para compartir el enlace y verificar comprobantes son la siguiente entrega.

## 1. Flujo

```
Fotógrafo                         API                              Cliente
   │ POST /api/bookings/{id}/link ─▶ crea token (256 bits), guarda su hash
   │◀── { token, url, expiresAt }     (el token solo se ve aquí)
   │ comparte la url por WhatsApp ─────────────────────────────────▶ abre /b/{token}
   │                                  GET /public/bookings/{token} ◀── ver cotización y estado (sin OTP)
   │                                  POST …/code ◀──────────────────── pide código
   │                                  envía el código al CORREO de la reserva (Resend)
   │                                  POST …/code/verify {code} ◀────── código recibido
   │                                  ──▶ { accessToken } (sesión de portal, 30 min)
   │                                  POST …/payments (Bearer) ◀─────── monto + referencia SINPE
   │ ve el pago PendingVerification
   │ POST …/payments/{id}/verify ───▶ cuenta para anticipo/saldo; confirma si hay contrato
   │ POST …/payments/{id}/reject  ──▶ (motivo obligatorio) queda como evidencia
```

## 2. Endpoints

| Método | Ruta | Auth | Respuestas |
|---|---|---|---|
| POST | `/api/bookings/{id}/link` | Fotógrafo | **201** `{ token, url, expiresAt }` · 404 si la reserva no es suya. Crear uno nuevo **revoca** el anterior |
| POST | `/api/bookings/{id}/payments/{paymentId}/verify` | Fotógrafo | 200 reserva · 409 `payment.invalid_status` si no está pendiente · 422 `payment.not_found` |
| POST | `/api/bookings/{id}/payments/{paymentId}/reject` | Fotógrafo | `{ reason }` obligatorio · 422 `booking.reason_required` |
| GET | `/public/bookings/{token}` | Token del enlace (60/min/IP) | 200 vista del cliente · 404 |
| POST | `/public/bookings/{token}/code` | Token (10/min/IP) | **202** `{ maskedEmail, expiresAt }` · 404 · 409 `portal.email_missing` · 429 `portal.otp_rate_limited` (+`Retry-After`) · 503 `email.delivery_failed` |
| POST | `/public/bookings/{token}/code/verify` | Token (10/min/IP) | `{ code }` → 200 `{ accessToken, expiresAt }` · 422 `portal.invalid_code` · 404 |
| POST | `/public/bookings/{token}/payments` | **Sesión de portal** (10/min/IP) | `{ amount, reference?, idempotencyKey }` → 200 vista · 401 sin sesión · 404 si la sesión es de otra reserva · 422 `money.invalid_amount` |

## 3. Seguridad

- **Token del enlace:** 256 bits aleatorios (`IRefreshTokenGenerator`, mismo generador que los refresh tokens), en Mongo solo el **hash** SHA-256 (`booking_links`, índice único). Un token desconocido, vencido o revocado responde **el mismo 404**. Vence 90 días después del fin de la sesión.
- **El código solo va al correo que registró el fotógrafo.** El cliente nunca escribe una dirección, así que tener el enlace no basta para recibir el código en otro lado. Sin correo en la reserva: 409 `portal.email_missing`.
- **Código (OTP):** 6 dígitos, 10 min, **un solo uso**, hash HMAC-SHA256 con la clave de firma y la reserva (un código tiene solo un millón de valores; un hash simple se invertiría al instante). Máximo 5 intentos: cada intento se cuenta con un `$inc` atómico **antes** de comparar, así que intentos en paralelo no evaden el límite (probado con 40 en paralelo). Un código mal formado no gasta intentos.
- **Envío:** 60 s entre códigos de la misma reserva y 5 por hora, además del límite por IP.
- **Sesión de portal:** JWT de 30 min, sin renovación, con audiencia `photosense-portal` y claim `bkg` (la reserva). Esquema de autenticación aparte (`PortalBearer`) y política `client-portal`: un token de portal **no abre** la API del fotógrafo (401) y uno del fotógrafo **no abre** las acciones del portal (401). La acción compara `bkg` con la reserva del enlace (404 si no coincide).
- **La vista del cliente** (`PortalBookingResponse`) no incluye teléfono, correo, ids, política ni historial; solo nombre del fotógrafo y cliente, paquete, fechas, montos, estado de pagos, `canSubmitProof` y `canReceiveCode`.
- **Comprobante:** el monto no puede superar el saldo menos lo que ya espera verificación; `idempotencyKey` obligatoria (doble toque = un solo pago); referencia opcional de hasta 100 caracteres que ve el fotógrafo.
- El limitador va **después** de la autorización (ver `docs/Authentication.md` §19.1).

## 4. Datos nuevos

| Dónde | Qué |
|---|---|
| `bookings.clientEmail` | Opcional. Se envía en `POST /api/bookings` (`clientEmail`); se devuelve al fotógrafo. Reservas anteriores no lo tienen: el cliente no puede recibir códigos hasta que se agregue |
| `bookings.payments[].reference` | Referencia SINPE del cliente |
| `booking_links` | `tokenHash` (único), `bookingId`, `photographerId`, `expiresAt` (TTL +30 días), `revokedAt` |
| `portal_otps` | Un documento por reserva (`_id` = reserva): `codeHash`, `expiresAt` (TTL +1 h), `failedAttempts`, `sendCount`, `windowStartedAt` |

## 5. Configuración (solo la API)

| Variable | Para qué |
|---|---|
| `RESEND_API_KEY` | Clave de Resend (*Sending access*, restringida al dominio). Sin ella: en Development el correo se **escribe en el log**; en producción el envío falla con 503 y la API sigue arrancando |
| `EMAIL_FROM` | Remitente, p. ej. `PhotoSense Studio <no-reply@mail.photosensestudio.com>`. Obligatoria si hay clave |
| `PORTAL_BASE_URL` | Dirección pública del **sitio** (p. ej. `https://develop.photosensestudio.com`); con ella se arma `…/b/{token}` y es el origen que CORS permite. **Obligatoria fuera de Development** (la API no arranca sin ella); en Development vale `http://localhost:5173` |
| `PORTAL_ORIGINS` | Opcional. Orígenes extra permitidos por CORS, separados por comas (p. ej. una vista previa de Pages: `https://preview.pages.dev`). Solo orígenes `http(s)` sin ruta; un valor inválido (`*`, con ruta) detiene la API al arrancar |

En local no hace falta nada: el código de verificación aparece en el log de la API, y en Development CORS ya permite `http://localhost:5173` y `http://127.0.0.1:5173` (el servidor de Vite).

## 5.1 CORS

El portal es una página web en otro origen que el API, así que el navegador exige permiso. La política `portal` (`Cors/PortalCorsExtensions.cs`):

- Se aplica **solo a `/public/bookings/{token}/**`**. La API del fotógrafo no se abre a navegadores: la usa la app, que no tiene origen.
- Orígenes: el de `PORTAL_BASE_URL`, los de `PORTAL_ORIGINS` y, en Development, los de Vite. Sin comodín.
- Métodos `GET` y `POST`; cabeceras `Authorization` y `Content-Type`; **sin credenciales** (el token va en cabecera, no en cookies); preflight en caché 1 hora.
- Expone `Retry-After`, que el portal usa para la cuenta regresiva de reenvío del código.
- `UseCors` va **antes** del manejador de excepciones: los errores del portal son excepciones convertidas en `ProblemDetails`, y sin la cabecera el navegador los escondería detrás de un fallo de red genérico (probado en `PortalCorsTests`).

## 6. Pendiente

- ~~**App del fotógrafo**~~ Hecho: campo de correo (opcional) al crear la reserva, tarjeta "Enlace del cliente" (crea el enlace y abre WhatsApp con el mensaje) y tarjeta "Comprobantes por verificar" (verificar con confirmación, rechazar con motivo). Queda sin pantalla para **agregar el correo a una reserva que ya existe** (no hay endpoint para editarlo).
- ~~**Portal web**~~ Hecho en `portal/` (React 19 + Vite, en el mismo sitio que la página principal; ver `docs/ClientPortalWeb.md` y `docs/Deployment.md`). Falta su entrega 4 (pulido y pruebas de punta a punta) y la firma del contrato.
- ~~**Aviso al fotógrafo** cuando llega un comprobante~~ Hecho: `PaymentSubmitted` genera un aviso en la bandeja y un push (`PushNotifications.md`); la tarjeta "Revisar comprobante" abre la reserva.
- **Firma del contrato por el cliente** (B2, canal Portal) y la autorización de imagen.
- **Comprobante como imagen** (necesita R2 y URLs prefirmadas).
- Recibo por correo al verificar el pago.
