# A2 — Backend por módulos

Cada módulo tiene su parte en Domain, sus casos de uso en Application (una carpeta por caso, con su comando/consulta y su manejador) y sus endpoints en Api. Para el detalle fino de cada uno hay un documento en `docs/`.

## 1. Bookings (reservas) — `docs/` general, `CLAUDE.md` §8

**Agregado `Booking`** (`Domain/Bookings`): máquina de estados `Tentative → Confirmed → Completed`, y finales `Cancelled`, `Expired`, `ClientAbsent` (revertible). Lo importante:

- **Contrato firmado y anticipo verificado no son estados**, son guardas para llegar a `Confirmed`. Cuando ambos se cumplen, la reserva pasa sola a `Confirmed` (transición B6, privada).
- El dinero no es un estado: el **saldo** se calcula (`Precio − pagos verificados`).
- La **política se congela** en la reserva al crearla: cambiar la política después no afecta reservas existentes.
- Cada transición se guarda en el historial con actor (`Client`, `Photographer`, `System`), canal, motivo y fecha.
- `allowedActions`: el dominio calcula qué puede hacer el usuario ahora; los clientes (app y web) solo muestran esas acciones y no implementan reglas de estado.

**Casos de uso** (`Application/Bookings`): `CreateBooking`, `GetBooking`, `ListBookings` (paginado por cursor), `EditBookingDetails`, `GetContract`, `SignContractInPerson`, `RecordInPersonPayment`, `VerifyPayment`, `RejectPayment`, `RescheduleBooking`, `CancelBooking`, `CompleteBooking`, `MarkClientAbsent`, `RevertClientAbsent`, `ExpireTentativeBookings` (job).

**Endpoints** (todos exigen sesión del fotógrafo, y una reserva ajena responde 404):

| Método y ruta | Qué hace |
|---|---|
| `POST /api/bookings` | Crea una reserva tentativa (B1). Acepta correo del cliente, extras y descuento |
| `GET /api/bookings`, `GET /api/bookings/{id}` | Lista paginada (la más reciente primero) y detalle con `allowedActions` |
| `GET /api/bookings/calendar?from&to&includeEnded` | Las reservas que tocan un rango de fechas, sin páginas y por hora de inicio, para la agenda de la app: máx. 62 días y 500 reservas (`truncated: true` si hay más); por defecto solo tentativas y confirmadas |
| `POST /api/bookings/{id}/details` | Corrige datos del cliente; paquete, precio, extras y descuento solo antes de firmar y pagar |
| `GET /api/bookings/{id}/contract` | El contrato con los datos de la reserva |
| `POST .../contract/in-person` | Firma presencial (en el teléfono) o en papel |
| `POST .../payments/in-person` | Pago en efectivo o SINPE registrado por el fotógrafo (entra verificado) |
| `POST .../payments/{paymentId}/verify` · `/reject` | Verifica o rechaza (con motivo) un comprobante del cliente |
| `POST .../reschedule` | Reprograma una reserva confirmada |
| `POST .../cancel`, `.../complete`, `.../client-absent`, `.../client-absent/revert` | Cancelar, completar, ausente y revertir |
| `POST .../link` | Crea (o reemplaza) el enlace del cliente |

## 2. Billing (reembolsos y retenciones) — `docs/Billing.md`

Cuando una reserva con dinero pagado termina cancelada, vencida o con cliente ausente, el consumidor `BookingSettlementHandler` (que escucha `BookingCancelled`, `BookingExpired`, `ClientMarkedAbsent` y `ClientAbsenceReverted`) crea una **liquidación** (`BookingSettlement`) con lo que se retiene y lo que se reembolsa, según la política congelada. El fotógrafo ve los reembolsos pendientes y los marca como devueltos (efectivo o SINPE, con nota; definitivo).

Endpoints: `GET /api/billing/refunds`, `GET /api/billing/settlements/{bookingId}`, `POST .../refund/complete`.

El **cargo automático de fotos extra no existe**: se acuerda aparte con el cliente (decisión 2026-11-02) y el fotógrafo "entrega las adicionales" a mano cuando el cliente ya pagó.

## 3. Contracts (contrato) — `Application/Contracts`

Plantilla versionada (`contract-v1.md`) que `ContractRenderer` rellena con los datos de la reserva y la política congelada. Se guarda el **texto exacto firmado**. Al firmar el cliente en el portal, `ContractCopyHandler` (consumidor de `ContractSigned`) envía una copia por correo, de forma idempotente. Si cambia la plantilla vigente entre que el fotógrafo la lee y la firma, responde 409 `contract.template_changed`.

## 4. Identity (cuentas y sesiones) — `docs/Authentication.md`

- Registro y login con usuario y contraseña; JWT propio de 15 minutos más token de renovación rotativo de un solo uso.
- Verificación del correo con código de 6 dígitos (`EmailVerificationSender`); hasta verificarlo la API responde 403 a casi todo.
- Cambio de contraseña (cierra las demás sesiones).
- **Web del fotógrafo:** sesión en cookie y segundo factor por correo (`StudioLoginChallenge`, `StudioSession`, `StudioTrustedDevice`).
- Rol de sistema (`isSystemOperator`) para `POST /api/maintenance/run`.
- Corregir un correo mal escrito: `POST /api/account/email/change`, solo antes de verificarlo y respetando los límites de envío del código (`docs/Authentication.md` §20).
- Recuperación de contraseña por código al correo verificado (`/api/auth/password-reset/request` y `/confirm`; `docs/Authentication.md` §22).
- **Pendiente:** verificación del teléfono.

## 5. Portal del cliente — `docs/ClientPortal.md`

- Enlace único por reserva (`POST .../link`): token opaco de 256 bits; en Mongo solo su hash; crear otro revoca el anterior.
- Ver la reserva solo necesita el enlace. Las acciones que cambian algo (subir comprobante, firmar, ver la galería) exigen una **sesión de portal**, que se obtiene con un código de 6 dígitos enviado **solo al correo que registró el fotógrafo**.
- La sesión de portal es un JWT (audiencia `photosense-portal`, claim `bkg` con la reserva) que dura **30 días por defecto** (`PORTAL_SESSION_DAYS`, de 1 a 90) y se guarda en el navegador del cliente: el primer código sirve para todo.

Endpoints: `GET /public/bookings/{token}`, `POST .../code`, `.../code/verify`, `.../payments`, `.../contract` (leer y firmar), y los de galería (abajo).

## 6. Notifications (avisos) — `docs/Notifications.md`, `docs/PushNotifications.md`

Bandeja de avisos del fotógrafo. Tipos: recordatorio de sesión (24 h antes), saldo pendiente (24 h después de completar), comprobante por verificar, selección recibida y galería lista para liberar. Un aviso se **programa** (`Scheduled`), y al vencer se **reevalúa**: si ya no es relevante (la reserva se canceló, se movió, se pagó...) se descarta (`Cancelled`); si no, se entrega (`Delivered`) a la bandeja y se envía el push. La clave de deduplicación única evita avisos repetidos aunque el outbox entregue un evento dos veces.

Push: `POST /api/push-devices` registra el teléfono; `ExpoPushSender` envía por el servicio de Expo (que usa FCM en Android). Texto fijo en `NotificationPushText`.

## 7. Galleries — `docs/Gallery.md`

Máquina de estados: `Pending → InSelection → InEditing → Ready → Delivered → Archived`.

| Paso | Qué ocurre |
|---|---|
| G1 | Al completar la sesión, `GalleryCreationHandler` crea la galería sola |
| Pruebas | El fotógrafo sube pruebas (web o celular), máximo 150, 1 MB cada una |
| G2 `OpenSelection` | Publica las pruebas; el cliente recibe un correo **sin enlace** |
| G3 `SubmitSelection` | El cliente elige y envía; queda bloqueada; push al fotógrafo |
| G5 `ReopenSelection` | El fotógrafo la reabre con motivo; el cliente recibe correo |
| Finales | El fotógrafo sube un final por cada foto elegida (hasta 50 MB) con su miniatura |
| G6 `PublishFinals` | Exige el final de cada foto del paquete (las extras no hacen falta) |
| G7 `Deliver` | Lo hace el sistema (`GalleryDelivery`) al publicar o al verificar un pago, **solo con saldo cero** |
| Descargas | El cliente pide las direcciones en el momento y **pedirlas cuenta como una descarga** |
| ZIP | Lo arma el fotógrafo a mano y lo sube (hasta 500 MB); el servidor no arma ZIP |
| Liberar extras | A mano, cuando el cliente ya pagó las fotos adicionales |
| G8 `Archive` | Libera el espacio (ver abajo) |

**Paquete y extras:** las primeras fotos que el cliente eligió, tantas como cubre el paquete, son el paquete; el resto son extras.

**Archivo (G8):** una galería **entregada** se puede liberar a mano (`POST .../release`) o la pasada de mantenimiento la archiva sola. A los **90 días** de entregada se avisa **una sola vez** (`GalleryArchiveDue`); a los **180 días** se archiva automáticamente. `GalleryArchiver` borra primero los objetos de R2 y después guarda el estado: si R2 falla, la galería sigue entregada y se reintenta. Los plazos son constantes de `Gallery`.

Endpoints para el fotógrafo: las mismas operaciones existen en la app (`/api/galleries/{id}/...`, token de la app) y en la web (`/api/web/galleries/{id}/...`, cookie). Compartir manejadores garantiza las mismas reglas. Incluyen: listar y leer, términos, subir y registrar pruebas/finales/ZIP, abrir y reabrir selección, publicar finales, entregar extras, quitar ZIP, descargar un final, liberar espacio y listar las archivables (`/archivable`).

Para el cliente: `GET /public/bookings/{token}/gallery/summary` (solo enlace), `.../gallery` (sesión de portal), `.../gallery/selection`, `.../gallery/downloads`, `.../gallery/zip/download`.

## 8. Maintenance y Worker — `docs/Outbox-Worker Functionality.md`

Ver [A1 §5](A1-Arquitectura.md). El endpoint `POST /api/maintenance/run` exige rol de sistema y tiene límite de una llamada por minuto.

## 9. Invoicing (factura electrónica)

**Diseñado, no implementado.** En el MVP la app solo recuerda emitir la factura; en una segunda fase se integraría un proveedor autorizado vía API (no se implementa la firma XML propia).

## Errores

Respuesta `ProblemDetails` con extensión `code`.

| Origen | Código | HTTP |
|---|---|---|
| Dominio | `booking.invalid_transition`, `booking.guard_failed`, `booking.contract_already_signed`, `payment.invalid_status` | 409 |
| Dominio | `domain.required_value`, `money.*`, `schedule.invalid_time_slot`, `booking.invalid_policy`, `booking.actor_not_allowed`, `booking.session_in_past`, `booking.reason_required`, `payment.not_found`, `payment.invalid_method`, `user.*` (nombre, correo, teléfono, usuario, contraseña débil) | 422 |
| Aplicación | `booking.slot_unavailable`, `concurrency.conflict`, `user.username_taken`, `user.email_taken` | 409 |
| Aplicación | `resource.not_found` (también para lo ajeno) | 404 |
| Galería | `gallery.invalid_state`, `gallery.full`, `gallery.no_photos`, `gallery.finals_incomplete` | 409 |
| Galería | `gallery.invalid_photo`, `gallery.invalid_selection`, `gallery.invalid_zip`, `gallery.invalid_terms` | 422 |
| Solicitud | `request.invalid` | 400 |
| Autenticación | `auth.invalid_credentials`, `auth.invalid_refresh_token` | 401 |
| Autenticación | `auth.account_locked`, `auth.email_code_rate_limited`, `auth.login_code_rate_limited`, `rate_limit.exceeded` (con `Retry-After`) | 429 |
| Autenticación | `auth.registration_disabled`, `request.forbidden_origin`, `auth.web_requires_verified_email` | 403 |
| Correo y almacenamiento | `email.delivery_failed`, `storage.unavailable` | 503 |
| No controlado | `server.error` (sin detalle) | 500 |

## Salud
`GET /health/live` (el proceso responde) y `GET /health/ready` (incluye MongoDB). Railway usa `ready`.

## Documentación de la API
`/swagger` y `/openapi/v1.json` solo existen en Development y donde `API_DOCS_ENABLED=true` (develop). **En producción no se define la variable.**
