# Notificaciones y recordatorios

Bandeja de avisos ("Avisos") para el fotógrafo dentro de la app, con un botón que abre WhatsApp (`wa.me`) con el mensaje ya escrito para el cliente. Se apoya en el outbox y en el worker (ver `Outbox-Worker Functionality.md`).

## 1. Qué hace

| Aviso | Cuándo se entrega | Condición al entregarse |
|---|---|---|
| **Recordatorio de sesión** (`SessionReminder`) | 24 h antes del inicio de la sesión | La reserva sigue `Confirmed`, la sesión sigue siendo la misma (no se reprogramó) y aún no empieza |
| **Saldo pendiente** (`BalanceDue`) | 24 h después de completar la sesión | La reserva está `Completed` y el saldo es mayor que cero |
| **Comprobante por verificar** (`PaymentProofSubmitted`) | De inmediato: en la misma pasada de mantenimiento que despacha el evento `PaymentSubmitted` | El pago sigue `PendingVerification` y la reserva no está cancelada, vencida ni con cliente ausente |

El fotógrafo ve los avisos en la pantalla **Avisos** (contador de no leídos en el inicio). El aviso de comprobante no ofrece WhatsApp: su acción principal es **Revisar comprobante**, que abre la reserva, donde ya están verificar y rechazar (`PendingPaymentsCard`). Los demás avisos ofrecen **Enviar por WhatsApp** (abre WhatsApp con el mensaje; marca el aviso como leído), **Ver reserva** y **Marcar como leído**. Además, **Marcar todo como leído**.

Los avisos de hoy son para el **fotógrafo**: es él quien escribe al cliente. No se envía nada al cliente de forma automática (WhatsApp automatizado es fase 3).

## 2. Diseño

- **El backend no guarda texto.** Un aviso lleva datos estructurados (tipo, cliente, teléfono, paquete, inicio de sesión, saldo). La app redacta el texto en español y arma el mensaje de WhatsApp (`mobile/src/utils/notifications.ts`). Así el backend no tiene idioma de cara al usuario.
- **Agregado `Notification`** (`Domain/Notifications`): `Scheduled → Delivered` (con `ReadAt` cuando se lee) o `Scheduled → Cancelled`. Una transición no válida lanza `booking.invalid_transition`-style `DomainException`.
- **Idempotencia por `dedupKey` único.** Los eventos del outbox se entregan *al menos una vez*, así que programar el mismo aviso dos veces no debe duplicarlo. Claves: `SessionReminder:{bookingId}:{ticksDelInicio}` (reprogramar la sesión cambia la clave) y `BalanceDue:{bookingId}`. Un índice único hace que el segundo `TryAdd` devuelva `false`.
- **El planificador reconcilia desde el estado actual** (`NotificationPlanner`), no desde el evento que lo disparó. Da el mismo resultado sin importar el orden ni cuántas veces llegue cada evento.
- **La relevancia se vuelve a evaluar al entregar** (`DeliverDueNotificationsHandler`): si la reserva se canceló, se movió o ya se pagó, el aviso se cancela en vez de entregarse. Una captura (`NotificationDetails`) congela lo que decía en ese momento.
- **Retención:** índice TTL `ix_notification_retention` sobre `retainUntil` (= `dueAt` + 90 días).
- **Aviso de comprobante:** `PaymentNotificationsHandler` consume `PaymentSubmitted` y el planificador programa el aviso con vencimiento "ahora"; la clave es `PaymentProofSubmitted:{paymentId}` (una por pago, así dos comprobantes son dos avisos y el mismo evento entregado dos veces es uno). Al entregar se reevalúa: si el fotógrafo ya verificó o rechazó el comprobante, el aviso se descarta. El snapshot lleva `proofAmount` (lo que declaró el cliente). Como el outbox se despacha antes de entregar avisos en la misma pasada, no suma espera: llega con la frecuencia del worker (hasta 15 min con el cron de Railway) o al instante con el botón de mantenimiento.
- **Push:** ver la sección 10. Es un complemento de la bandeja: la bandeja es la fuente de verdad y el push solo avisa.

## 3. Flujo

```
Booking cambia de estado ──► evento al outbox (misma transacción)
                                   │  worker / pasada de mantenimiento
                                   ▼
 BookingNotificationsHandler (BookingConfirmed, BookingRescheduled, BookingCancelled,
                              BookingExpired, ClientMarkedAbsent)  → ReconcileSessionReminder
 SessionCompletedNotificationsHandler (SessionCompleted)           → Reconcile + ScheduleBalanceDue
                                   │  crea/cancela Notification (Scheduled, dueAt)
                                   ▼
 RunMaintenanceHandler: expirar → outbox → DeliverDueNotifications
                                   │  Scheduled y dueAt ≤ ahora → Delivered (o Cancelled)
                                   ▼
 GET /api/notifications  ◄── la app consulta cada 60 s y al abrir la bandeja
```

## 4. Dónde vive

| Capa | Archivos |
|---|---|
| Domain | `Domain/Notifications/Notification.cs`, `NotificationEnums.cs` |
| Application | `Notifications/NotificationPlanner.cs`, `NotificationPolicy.cs`, `BookingNotificationsHandler.cs`, `SessionCompletedNotificationsHandler.cs`, `PaymentNotificationsHandler.cs`, `DeliverDue/`, `List/`, `MarkRead/`, `NotificationResponses.cs`; puerto `Abstractions/INotificationRepository.cs` |
| Infrastructure | `Persistence/MongoNotificationRepository.cs`, `Persistence/Documents/NotificationDocument.cs`, índices en `MongoIndexInitializer.cs` |
| Api | `Endpoints/NotificationEndpoints.cs` |
| Worker | `MaintenanceService.cs` (modo siempre encendido, cada minuto), `RunOnceJob.cs` (modo una pasada) |
| Mobile | `api/notificationsApi.ts`, `hooks/useNotifications.ts`, `useMarkNotificationsRead.ts`, `useNotificationActions.ts`, `utils/notifications.ts`, `navigation/externalLinks.ts`, `components/NotificationsButton.tsx`, `NotificationListItem.tsx`, `app/notifications.tsx` |

## 5. API

Todas requieren sesión; todo se acota al fotógrafo del token (otro fotógrafo recibe 404 al leer un aviso ajeno).

| Método | Ruta | Descripción |
|---|---|---|
| GET | `/api/notifications` | Hasta 50 avisos entregados, el más reciente primero, y `unreadCount` |
| POST | `/api/notifications/{id}/read` | Marca uno como leído (idempotente). 404 si no existe, es de otro fotógrafo o no se entregó |
| POST | `/api/notifications/read-all` | Marca todos como leídos; responde `{ "marked": n }` |
| POST | `/api/push-devices` | Registra el teléfono para recibir push: `{ token, platform: Android | Ios }` → 204. Es idempotente; un token que era de otro fotógrafo pasa a este. 422 `push.invalid_token` si no es un token de Expo |
| POST | `/api/push-devices/unregister` | Olvida el teléfono: `{ token }` → 204. Un token desconocido o ajeno se ignora |

`POST /api/maintenance/run` ahora también devuelve `notificationsDelivered`.

## 6. Colección `notifications`

Índices: `ix_notification_dedup` (único), `ix_notification_due` `{status, dueAt}`, `ix_notification_inbox` `{photographerId, status, deliveredAt desc}`, `ix_notification_booking` `{bookingId, type, status}`, `ix_notification_retention` (TTL).

## 7. Frecuencia del worker (Railway)

Un recordatorio "24 h antes" no sirve si el trabajo corre una vez al día. Para recordatorios:

- **Cron `*/15 * * * *`** (cada 15 minutos) en el servicio `photostudio-worker` con `WORKER_RUN_ONCE=true`. Cada ejecución arranca, hace una pasada y termina (unos segundos), así que el costo sigue siendo bajo. Los recordatorios llegan con hasta 15 min de retraso, aceptable para un aviso de 24 h.
- La frecuencia mínima de Railway es de 5 minutos. Si prefieres menos ejecuciones, `*/30 * * * *` también funciona.
- El botón de la app ("Liberar reservas vencidas") sigue disparando una pasada a demanda, que ahora también entrega avisos vencidos.

Cambio respecto a la configuración diaria: solo cambia el **Cron Schedule** del servicio en Railway.

## 8. Pruebas

- Domain: `NotificationTests`.
- Application: `NotificationPlannerTests`, `PaymentNotificationsHandlerTests`, `DeliverDueNotificationsHandlerTests` (reubicada, cancelada, pagada, comprobante pendiente/resuelto, conflicto), `NotificationInboxHandlerTests`, `RunMaintenanceHandlerTests`.
- Infrastructure (Mongo real): `MongoNotificationRepositoryTests` (deduplicación, cancelación con excepción, vencidos, bandeja por fotógrafo, concurrencia, TTL).
- Api (punta a punta): `NotificationsApiTests` (sin token → 401, aislamiento por fotógrafo, reserva confirmada → recordatorio entregado y leído, reserva cancelada → sin aviso; comprobante subido por el portal → aviso con el monto, solo del dueño; comprobante resuelto antes de la entrega → sin aviso).
- Mobile: `notifications.test.ts`, `notificationsApi.test.ts`, `useNotificationActions.test.tsx`, `NotificationListItem.test.tsx`.

## 9. Límites conocidos

- El push depende de que el teléfono tenga permiso de notificaciones y un token registrado; sin eso el aviso solo está en la bandeja (se actualiza cada 60 s con la app abierta).
- Los recordatorios usan 24 h fijas (`NotificationPolicy`); a futuro configurables por fotógrafo.
- La API despierta una pasada de mantenimiento tras cada cambio de reserva (sección 10.3), pero es un atajo: si esa pasada falla, el worker lo entrega con su frecuencia (hasta 15 min).
- No hay aviso de plazo de entrega de galería (depende de Gallery).
- La app no valida que WhatsApp esté instalado: `wa.me` cae al navegador si no lo está.

## 10. Push

### 10.1 Qué hace
Cuando un aviso llega a la bandeja, además se envía una notificación del sistema a los teléfonos registrados del fotógrafo (`NotificationPusher`, llamado por `DeliverDueNotificationsHandler` solo para lo que **esa** pasada entregó: lo que ganó otra pasada lo envía esa). Al tocarla, la app abre la reserva (`data.bookingId`).

- **Es un complemento, nunca un requisito.** Un fallo (Mongo, Expo, sin teléfonos) se registra y el aviso sigue entregado; no rompe la pasada.
- **Texto en el backend (excepción documentada).** La bandeja la redacta la app, pero un push lo dibuja el sistema con la app cerrada, así que su texto viaja con él (`NotificationPushText`, solo español). Las horas se escriben en hora de Costa Rica (UTC-6 fijo, sin horario de verano) y a mano, sin depender de los datos de cultura del contenedor.
- **Teléfono = token de Expo** (`ExponentPushToken[...]`), guardado en `push_devices` con el token como `_id`. Un token pertenece a un fotógrafo a la vez: si otro inicia sesión en el mismo teléfono, el registro lo mueve. Expo informa `DeviceNotRegistered` cuando la app se desinstaló y ese token se borra solo.
- **Proveedor:** `ExpoPushSender` → `https://exp.host/--/api/v2/push/send`, hasta 100 mensajes por petición, canal Android `default`. Variable opcional `EXPO_ACCESS_TOKEN` (solo si el proyecto de Expo activó la seguridad mejorada). No se leen los recibos de entrega.

### 10.2 App
`PushRegistration` (en el layout raíz) usa `usePushRegistration`: al haber sesión registra el teléfono (crea el canal `default`, pide permiso, obtiene el token del proyecto de EAS y lo envía), escucha los avisos que llegan con la app abierta (recarga la bandeja) y abre la reserva al tocar una notificación, también la que abrió la app desde cerrada. Al cerrar sesión, `signOutOfDevice` pide al backend olvidar el teléfono (máximo 3 s, sin conexión se ignora) y luego cierra la sesión. Los efectos de `expo-notifications` viven solo en `src/push/pushManager.ts`.

Solo funciona en un dispositivo físico con un build que incluya `expo-notifications` y las credenciales FCM (ver `docs/Deployment.md`, sección 7); no en Expo Go ni en un emulador.

### 10.3 Entrega inmediata
Antes, un aviso podía tardar hasta 15 min (el cron del worker). Ahora la API tiene un `OutboxWakeUpService` (hosted service): un middleware avisa tras cada petición exitosa que **cambia** reservas (`/api/bookings/**` y `/public/bookings/**`, no GET) y el servicio corre una pasada de mantenimiento (máximo una por segundo; las señales seguidas se funden en una). Es seguro con el worker corriendo a la vez: los mensajes del outbox se toman con *lease* y los avisos con control de versión. El worker sigue siendo la red de seguridad.

### 10.4 Pruebas
Domain `PushDeviceTests`; Application `NotificationPushTextTests`, `NotificationPusherTests` (incluye los handlers de registro) y las de entrega; Infrastructure `ExpoPushSenderTests` (Expo simulado) y `MongoPushDeviceRepositoryTests` (Mongo real); Api `PushDevicesApiTests` (de punta a punta: comprobante subido por el portal → push capturado, **sin** correr mantenimiento a mano); Mobile `pushManager`, `PushRegistration`, `pushSignOut`, `push` (utils) y `pushDevicesApi`.
