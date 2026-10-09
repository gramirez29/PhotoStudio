# Notificaciones y recordatorios

Bandeja de avisos ("Avisos") para el fotógrafo dentro de la app, con un botón que abre WhatsApp (`wa.me`) con el mensaje ya escrito para el cliente. Se apoya en el outbox y en el worker (ver `Outbox-Worker Functionality.md`).

## 1. Qué hace

| Aviso | Cuándo se entrega | Condición al entregarse |
|---|---|---|
| **Recordatorio de sesión** (`SessionReminder`) | 24 h antes del inicio de la sesión | La reserva sigue `Confirmed`, la sesión sigue siendo la misma (no se reprogramó) y aún no empieza |
| **Saldo pendiente** (`BalanceDue`) | 24 h después de completar la sesión | La reserva está `Completed` y el saldo es mayor que cero |

El fotógrafo ve los avisos en la pantalla **Avisos** (contador de no leídos en el inicio). Cada aviso ofrece **Enviar por WhatsApp** (abre WhatsApp con el mensaje; marca el aviso como leído), **Ver reserva** y **Marcar como leído**. Además, **Marcar todo como leído**.

Los avisos de hoy son para el **fotógrafo**: es él quien escribe al cliente. No se envía nada al cliente de forma automática (WhatsApp automatizado es fase 3).

## 2. Diseño

- **El backend no guarda texto.** Un aviso lleva datos estructurados (tipo, cliente, teléfono, paquete, inicio de sesión, saldo). La app redacta el texto en español y arma el mensaje de WhatsApp (`mobile/src/utils/notifications.ts`). Así el backend no tiene idioma de cara al usuario.
- **Agregado `Notification`** (`Domain/Notifications`): `Scheduled → Delivered` (con `ReadAt` cuando se lee) o `Scheduled → Cancelled`. Una transición no válida lanza `booking.invalid_transition`-style `DomainException`.
- **Idempotencia por `dedupKey` único.** Los eventos del outbox se entregan *al menos una vez*, así que programar el mismo aviso dos veces no debe duplicarlo. Claves: `SessionReminder:{bookingId}:{ticksDelInicio}` (reprogramar la sesión cambia la clave) y `BalanceDue:{bookingId}`. Un índice único hace que el segundo `TryAdd` devuelva `false`.
- **El planificador reconcilia desde el estado actual** (`NotificationPlanner`), no desde el evento que lo disparó. Da el mismo resultado sin importar el orden ni cuántas veces llegue cada evento.
- **La relevancia se vuelve a evaluar al entregar** (`DeliverDueNotificationsHandler`): si la reserva se canceló, se movió o ya se pagó, el aviso se cancela en vez de entregarse. Una captura (`NotificationDetails`) congela lo que decía en ese momento.
- **Retención:** índice TTL `ix_notification_retention` sobre `retainUntil` (= `dueAt` + 90 días).
- **Puerto para push:** la entrega pasa por `DeliverDueNotificationsHandler`; agregar push más adelante es añadir un puerto `INotificationChannel` ahí, sin tocar el planificador.

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
| Application | `Notifications/NotificationPlanner.cs`, `NotificationPolicy.cs`, `BookingNotificationsHandler.cs`, `SessionCompletedNotificationsHandler.cs`, `DeliverDue/`, `List/`, `MarkRead/`, `NotificationResponses.cs`; puerto `Abstractions/INotificationRepository.cs` |
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
- Application: `NotificationPlannerTests`, `DeliverDueNotificationsHandlerTests` (reubicada, cancelada, pagada, conflicto), `NotificationInboxHandlerTests`, `RunMaintenanceHandlerTests`.
- Infrastructure (Mongo real): `MongoNotificationRepositoryTests` (deduplicación, cancelación con excepción, vencidos, bandeja por fotógrafo, concurrencia, TTL).
- Api (punta a punta): `NotificationsApiTests` (sin token → 401, aislamiento por fotógrafo, reserva confirmada → recordatorio entregado y leído, reserva cancelada → sin aviso).
- Mobile: `notifications.test.ts`, `notificationsApi.test.ts`, `useNotificationActions.test.tsx`, `NotificationListItem.test.tsx`.

## 9. Límites conocidos

- Sin push todavía (puerto previsto). Los avisos aparecen al abrir la app o cada 60 s con ella abierta.
- Los recordatorios usan 24 h fijas (`NotificationPolicy`); a futuro configurables por fotógrafo.
- No hay aviso de "comprobante por verificar" ni de plazo de entrega de galería (dependen del portal y de Gallery).
- La app no valida que WhatsApp esté instalado: `wa.me` cae al navegador si no lo está.
