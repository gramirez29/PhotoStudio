# Notificaciones push

Guía completa del push del fotógrafo: qué es, con qué herramientas se hace, en qué plataformas funciona, cómo está implementado y por qué, cómo se configura, cómo se prueba y qué falta.

> **Estado (2026-10-16):** implementado y probado de forma automática (534 pruebas de backend, 436 de la app, una de punta a punta que captura el push). **Un push real a un teléfono Android se probó y funciona (2026-10-28).** Pendiente: iOS y el resto de §14.

Documentos relacionados: [`Notifications.md`](Notifications.md) (la bandeja de avisos, de la que el push es un complemento), [`Outbox-Worker Functionality.md`](Outbox-Worker%20Functionality.md) (cómo llegan los eventos), [`Deployment.md`](Deployment.md) §7 (build de EAS y credenciales), [`ClientPortal.md`](ClientPortal.md) (el comprobante que dispara el aviso principal).

## Índice

1. [Qué es y qué no es](#1-qué-es-y-qué-no-es)
2. [Qué recibe el fotógrafo](#2-qué-recibe-el-fotógrafo)
3. [Herramientas y servicios](#3-herramientas-y-servicios)
4. [Plataformas](#4-plataformas)
5. [Arquitectura y flujo](#5-arquitectura-y-flujo)
6. [Enfoque de implementación y decisiones](#6-enfoque-de-implementación-y-decisiones)
7. [Modelo de datos](#7-modelo-de-datos)
8. [API](#8-api)
9. [Dónde vive el código](#9-dónde-vive-el-código)
10. [Configuración y credenciales](#10-configuración-y-credenciales)
11. [Cómo probarlo](#11-cómo-probarlo)
12. [Si algo falla](#12-si-algo-falla)
13. [Seguridad, privacidad y límites](#13-seguridad-privacidad-y-límites)
14. [TODO](#14-todo)

---

## 1. Qué es y qué no es

**Es** una notificación del sistema (la que aparece en la barra del teléfono, con la app cerrada o el teléfono bloqueado) que avisa al fotógrafo de algo que ya está en su bandeja de **Avisos**. Al tocarla, la app abre la reserva.

**No es** la fuente de la verdad. La bandeja (`notifications` en Mongo) lo es: un aviso existe aunque el push falle, y la app lo muestra al abrirse o cada 60 s con ella abierta. El push solo evita que el fotógrafo tenga que abrir la app para enterarse. Por eso:

- un fallo del push (Expo caído, Mongo lento, sin teléfonos registrados) se **registra en el log y nunca** hace fallar la entrega del aviso;
- no hay reintentos del push (ver TODO);
- el push no es una garantía de entrega: Android o el fabricante pueden retrasarlo o descartarlo.

**Tampoco es** un push para el cliente. El cliente nunca instala nada (CLAUDE.md §1); su canal es el enlace web, el correo y WhatsApp (`wa.me`).

## 2. Qué recibe el fotógrafo

| Aviso | Cuándo sale | Título | Texto (ejemplo) |
|---|---|---|---|
| Comprobante por verificar (`PaymentProofSubmitted`) | En cuanto el cliente sube un comprobante SINPE por el portal | Comprobante por verificar | María Pérez envió un comprobante de ₡25 000. |
| Sesión próxima (`SessionReminder`) | 24 h antes de la sesión de una reserva confirmada | Sesión próxima | María Pérez · Retrato Familiar · sáb 17 oct, 3:00 p. m. |
| Saldo pendiente (`BalanceDue`) | 24 h después de completar la sesión, si aún se debe dinero | Saldo pendiente | María Pérez debe ₡50 000 de su sesión de Retrato Familiar. |

Cada push lleva, además del texto, `data` con `notificationId`, `bookingId` y `type`. La app usa `bookingId` para abrir la reserva al tocarlo.

## 3. Herramientas y servicios

| Pieza | Para qué | Dónde |
|---|---|---|
| **Expo Push Service** (`https://exp.host/--/api/v2/push/send`) | Recibe nuestros mensajes y los reenvía a Firebase (Android) o Apple (iOS). Es el único proveedor con el que habla el backend. | Backend: `ExpoPushSender` |
| **Firebase Cloud Messaging (FCM V1)** | Transporte de push en Android. Gratis. | Proyecto de Firebase `photosense-studio` |
| **Apple Push Notification service (APNs)** | Transporte de push en iOS. **Pendiente** (exige Apple Developer, 99 USD/año). | — |
| **EAS Build** (Expo Application Services) | Compila el APK de desarrollo en la nube. Plan gratis: 15 builds Android + 15 iOS al mes. | `mobile/eas.json` |
| **EAS Credentials / Environment variables** | Guarda la clave de cuenta de servicio de FCM y el `google-services.json` fuera del repositorio. | expo.dev, proyecto `@g.raba29-dev/photostudio` |
| `expo-notifications` ~57.0.23 | Permisos, token de Expo, canal Android, escuchar llegadas y toques. | `mobile/src/push/pushManager.ts` |
| `expo-device` ~57.0.2 | Saber si es un dispositivo físico (un emulador no recibe push). | `pushManager.ts` |
| `expo-constants` ~57.0.21 | Leer el `projectId` de EAS de la configuración. | `pushManager.ts`, `utils/push.ts` |
| `expo-dev-client` ~57.0.20 | Build de desarrollo propio (Expo Go no soporta push remoto en Android). | `mobile/package.json` |
| **TanStack Query** 5 | Recargar la bandeja cuando llega un push con la app abierta. | `usePushRegistration` |
| **.NET 10**: `IHttpClientFactory`, `Channel<T>`, `BackgroundService` | Cliente HTTP a Expo (tipado, con timeout de 10 s) y el servicio que despierta el procesamiento. | `PhotoStudio.Infrastructure`, `PhotoStudio.Api` |
| **MongoDB** | Colección `push_devices` y la bandeja `notifications`. | Atlas M0 |
| **Outbox + Worker** | Entregan los eventos de dominio que originan los avisos. | `docs/Outbox-Worker Functionality.md` |
| **Railway** | Corre la API y el worker (ambos pueden enviar pushes). | `docs/Deployment.md` |

## 4. Plataformas

| Plataforma | Estado | Notas |
|---|---|---|
| **Android, dispositivo físico, build de desarrollo (EAS)** | **Implementado y verificado con un push real (2026-10-28)** | Es el objetivo actual. APK interno (`distribution: internal`). Canal `default` ("Avisos"). Android 13+ pide permiso `POST_NOTIFICATIONS` (lo gestiona `expo-notifications`). |
| iOS | Pendiente | El código ya contempla `Platform.OS === 'ios'` y el backend acepta `platform: Ios`, pero faltan Apple Developer, la clave APNs en EAS y un build. No hay canal que crear en iOS. |
| Expo Go | **No funciona** | Desde SDK 53 el push remoto en Android exige un build propio. |
| Emulador / simulador | No | `Device.isDevice` es falso: la app responde `unavailable` y no registra nada. |
| Web / portal del cliente | No aplica | El portal no usa push; sus avisos van por correo (Resend). |
| Producción (tienda) | Pendiente | Falta el perfil `production` de EAS, el keystore de producción y Google Play (ver §14). |

Versiones: Expo SDK 57, React Native 0.86, React 19, .NET 10, MongoDB.Driver 3.12.

## 5. Arquitectura y flujo

```
 CLIENTE (portal web)                    FOTÓGRAFO (app Android)
        │  sube comprobante SINPE                  ▲  ▲
        ▼                                          │  │ 6. toca → abre la reserva
 ┌──────────────────────────────┐                  │  │
 │ API  POST /public/bookings/  │                  │  │
 │      {token}/payments        │                  │  │
 └──────┬───────────────────────┘                  │  │
        │ 1. Booking.SubmitPaymentProof            │  │
        │    + evento PaymentSubmitted             │  │
        │    en outbox_messages (misma transacción)│  │
        │                                          │  │
        │ 2. middleware: petición OK que cambia    │  │
        │    reservas → IOutboxWakeUp.Signal()     │  │
        ▼                                          │  │
 ┌──────────────────────────────┐                  │  │
 │ OutboxWakeUpService (API)    │  (o el worker, en su cron de 15 min: red de seguridad)
 │  RunMaintenanceHandler:      │                  │  │
 │   a) expira reservas         │                  │  │
 │   b) outbox → handlers       │                  │  │
 │      PaymentNotifications-   │                  │  │
 │      Handler: programa la    │                  │  │
 │      Notification (due=ahora)│                  │  │
 │   c) DeliverDueNotifications │                  │  │
 │      · reevalúa relevancia   │                  │  │
 │      · Delivered → bandeja   │                  │  │
 │      · NotificationPusher ───┼─ 3. mensajes ──► Expo Push Service
 └──────────────────────────────┘                  │       │ 4. FCM V1
                                                   │       ▼
                                                   │   Firebase (FCM) ── 5. notificación ──┘
                                                   │
                                  La bandeja (GET /api/notifications) ya tiene el aviso
```

El mismo camino (pasos b y c) sirve para los recordatorios y el saldo pendiente: lo único que cambia es qué evento programa el aviso y cuándo vence.

**Registro del teléfono** (independiente del flujo anterior): al iniciar sesión la app pide permiso, obtiene su token de Expo y lo manda a `POST /api/push-devices`. Al cerrar sesión pide `POST /api/push-devices/unregister`.

## 6. Enfoque de implementación y decisiones

Cada decisión con su razón, para poder revisarla.

1. **Expo Push Service en vez de FCM directo.** Una sola integración y un solo formato para Android e iOS; el backend no necesita el SDK de Firebase ni de Apple, ni sus credenciales (esas viven en EAS). Costo: una dependencia más y la cuota/latencia de Expo. FCM sigue siendo el transporte por debajo. Cambiar de proveedor es cambiar `IPushSender` (puerto en Application).
2. **El push es un complemento de la bandeja.** Primero se persiste el aviso como `Delivered` (con control de versión) y solo entonces se intenta el push. Si el push falla, no hay nada que deshacer. `NotificationPusher` captura todo menos la cancelación.
3. **Se envía solo lo que *esa* pasada entregó.** La entrega gana una escritura con control de versión; si otra pasada (la del worker y la de la API pueden coincidir) ya entregó el aviso, esta lo salta y no lo envía. Resultado: como máximo un push por aviso. Contrapartida aceptada: si el proceso muere entre guardar `Delivered` y enviar, ese push se pierde (el aviso sigue en la bandeja).
4. **Relevancia al entregar, no al programar.** El aviso de comprobante se programa "ahora", pero al entregarse se vuelve a mirar la reserva: si el fotógrafo ya verificó o rechazó el pago (o la reserva se canceló), se descarta y no hay push. La clave de deduplicación `PaymentProofSubmitted:{paymentId}` lleva el pago, así un comprobante nuevo es un aviso nuevo y el mismo evento entregado dos veces es uno.
5. **El texto del push vive en el backend (excepción documentada).** La regla del proyecto es que el backend no guarda texto de cara al usuario y la app redacta la bandeja. Un push, en cambio, lo dibuja el sistema operativo con la app cerrada: no hay código de la app que pueda redactarlo. Por eso `NotificationPushText` fija el texto en español (el producto es solo es-CR). Las fechas se escriben en hora de Costa Rica con un desfase fijo de UTC-6 (el país no tiene horario de verano) y **sin** depender de los datos de cultura del contenedor, que una imagen mínima puede no traer.
6. **Un teléfono = un token de Expo, y el token es el `_id`.** Así no hace falta un índice único aparte y registrar dos veces el mismo teléfono (carrera) no crea dos documentos: es un `upsert` atómico. Un token pertenece a un fotógrafo a la vez: si otra persona inicia sesión en el mismo teléfono, el `upsert` lo mueve, y el dueño anterior deja de recibir sus avisos en él.
7. **Registro idempotente y "se rearma solo".** La app registra en cada arranque con sesión: refresca `lastSeenAt` y recoge un token que haya cambiado. Un fallo no bloquea nada; se reintenta en el siguiente arranque.
8. **Cerrar sesión olvida el teléfono, con tope de 3 s.** Debe ir *antes* de descartar la sesión (necesita el token de acceso). Sin conexión o con el backend colgado, se ignora y se cierra igual. Si la sesión muere por otra vía (401 persistente), el token queda registrado hasta que otro inicie sesión (lo mueve) o Expo lo reporte inválido.
9. **Tokens inválidos se limpian solos.** Expo responde `DeviceNotRegistered` cuando la app se desinstaló; `ExpoPushSender` lo reporta por posición del ticket y `NotificationPusher` borra el token.
10. **Despertar la entrega desde la API.** Antes, un aviso esperaba al cron del worker (hasta 15 min). Ahora un middleware avisa tras cada petición exitosa **no GET** a `/api/bookings/**` o `/public/bookings/**` y `OutboxWakeUpService` corre una pasada completa de mantenimiento (como mucho una por segundo; las señales seguidas se funden en una). Es seguro con el worker corriendo a la vez porque el outbox se reclama con *lease* y los avisos con control de versión. Se limita a esas rutas para no correr mantenimiento en cada login. Sigue siendo un atajo: el worker es la red de seguridad. La alternativa descartada fue un *change stream* de Mongo (más complejo; el sondeo del worker ya cubre el caso).
11. **El worker también envía.** La entrega (y por tanto el push) ocurre donde corra `DeliverDueNotificationsHandler`: en la API (despertador) y en el worker (cron). Los dos registran `IPushSender` en `AddInfrastructure`.
12. **Lotes de 100 y respuesta por posición.** Expo acepta hasta 100 mensajes por petición y contesta un ticket por mensaje en el mismo orden.
13. **No se versiona `google-services.json`.** El repositorio es público. EAS lo recibe como variable de archivo (`GOOGLE_SERVICES_JSON`) y `app.config.ts` la lee; en local, de `mobile/google-services.json` (ignorado por git). Sin ninguno de los dos, `expo start` y las pruebas siguen funcionando.
14. **Efectos aislados.** Todo lo que toca `expo-notifications` está en `src/push/pushManager.ts`; los hooks y componentes solo lo llaman (convención del proyecto: efectos fuera de utils y componentes).
15. **Build de desarrollo, no Expo Go.** El push necesita código nativo (`expo-notifications`) y credenciales; el APK de desarrollo lo incluye y carga el JavaScript de un servidor local.

## 7. Modelo de datos

**`push_devices`** (un documento por teléfono)

| Campo | Tipo | Notas |
|---|---|---|
| `_id` | string | Token de Expo, `ExponentPushToken[...]` o `ExpoPushToken[...]` (≤ 200 caracteres) |
| `photographerId` | Guid (estándar) | Tenant. El `upsert` lo reasigna si el teléfono cambia de dueño |
| `platform` | string | `Android` o `Ios` |
| `registeredAt` | fecha UTC | Solo se escribe al insertar (`$setOnInsert`) |
| `lastSeenAt` | fecha UTC | Se refresca en cada registro |

Índice: `ix_push_photographer` sobre `photographerId`. No hay TTL (ver TODO).

**`notifications`** (existente) gana `details.proofAmount` / `details.proofCurrency` para el aviso de comprobante (el monto que declaró el cliente). Los avisos anteriores no lo tienen y siguen leyéndose igual (`Money? ProofAmount = null`).

## 8. API

Todas exigen sesión y se acotan al fotógrafo del token (CLAUDE.md §7.1).

| Método | Ruta | Cuerpo | Respuesta |
|---|---|---|---|
| POST | `/api/push-devices` | `{ "token": "ExponentPushToken[...]", "platform": "Android" \| "Ios" }` | 204. 422 `push.invalid_token` si no es un token de Expo (también si falta) |
| POST | `/api/push-devices/unregister` | `{ "token": "..." }` | 204 siempre; un token desconocido o de otro fotógrafo se ignora |

`GET /api/notifications` ahora incluye `proofAmount` (`{ amount, currency }` o `null`) en cada ítem.

## 9. Dónde vive el código

**Backend**

| Capa | Archivos |
|---|---|
| Domain | `Notifications/PushDevice.cs` (+ `PushPlatform`), `NotificationEnums.cs` (`PaymentProofSubmitted`), `Notification.cs` (`PaymentProofKey`, `TryGetPaymentId`, `NotificationDetails.ProofAmount`), `DomainErrorCodes.InvalidPushToken` |
| Application | `Abstractions/IPushDeviceRepository.cs`, `IPushSender.cs`, `IOutboxWakeUp.cs` (+ `NullOutboxWakeUp`); `Notifications/PaymentNotificationsHandler.cs`, `NotificationPlanner.SchedulePaymentProofAsync`; `Notifications/Push/` (`NotificationPusher`, `NotificationPushText`, `RegisterPushDeviceCommand`, `PushDeviceHandlers`); `DeliverDueNotificationsHandler` (relevancia del comprobante y llamada al pusher) |
| Infrastructure | `Persistence/MongoPushDeviceRepository.cs`, `Documents/PushDeviceDocument.cs`, índice en `MongoIndexInitializer`; `Push/ExpoPushSender.cs` (+ `ExpoPushOptions`); registro en `DependencyInjection` |
| Api | `Endpoints/PushDeviceEndpoints.cs`, `PushDeviceRequests.cs`; `Maintenance/OutboxWakeUpService.cs`; el middleware y el registro en `Program.cs` |

**App móvil**

| Capa | Archivos |
|---|---|
| Efectos | `push/pushManager.ts` (permisos, canal, token, registro, olvidar, escuchar), `push/pushSignOut.ts` |
| UI / hooks | `components/PushRegistration.tsx` (en el layout raíz), `hooks/usePushRegistration.ts`, `components/SignOutButton.tsx` (usa `signOutOfDevice`), `NotificationListItem` ("Revisar comprobante") |
| API | `api/pushDevicesApi.ts`, `api/client.ts` |
| Puro | `utils/push.ts` (`readProjectId`, `readBookingId`, `toPushPlatform`), `utils/notifications.ts` (texto de la bandeja) |
| Constantes y tipos | `constants/push.ts`, `types/api/pushDevices*.ts`, `types/push/pushManager.types.ts` |
| Config | `app.json` (paquete, plugin `expo-notifications`, `extra.eas.projectId`), `app.config.ts`, `eas.json` |

## 10. Configuración y credenciales

| Qué | Dónde se guarda | Notas |
|---|---|---|
| Identificador de la app | `app.json` (`com.photosensestudio.app`) | No se puede cambiar tras publicar |
| Proyecto de EAS | `app.json` (`extra.eas.projectId`, `owner`) | `@g.raba29-dev/photostudio` |
| `google-services.json` (Firebase, identifica la app) | Variable de archivo de EAS `GOOGLE_SERVICES_JSON` (entorno `development`, visibilidad *sensitive*); copia local en `mobile/google-services.json` (git-ignorado) | Debe corresponder al paquete `com.photosensestudio.app` |
| Clave de cuenta de servicio de FCM V1 (permite **enviar**) | EAS Credentials (expo.dev → proyecto → Credentials → Android) | Es como una contraseña. Nunca al repositorio ni a un chat |
| `EXPO_ACCESS_TOKEN` | Variable de Railway (API **y** worker) | Opcional: solo si el proyecto de Expo activa la seguridad mejorada para push |
| `EXPO_PUBLIC_API_URL` | `eas.json` (build) y `mobile/.env` (servidor de desarrollo) | Va dentro del paquete: nunca secretos |

**Build de desarrollo (Android)**

```bash
cd mobile
npx eas-cli build --platform android --profile development
```

Un build de desarrollo **no trae el JavaScript**: al abrirlo muestra "Development servers" y espera un servidor (`npx expo start --dev-client`, con el teléfono en la misma red Wi-Fi, o `--tunnel`). Cambiar solo JavaScript no exige un build nuevo; sí lo exige añadir o quitar módulos nativos o cambiar plugins/`app.json`.

**Firebase, paso a paso** (una vez): crear el proyecto → añadir una app Android con el paquete exacto → descargar `google-services.json` → *Project settings → Service accounts → Generate new private key* → subir esa clave a EAS (expo.dev → Credentials). Se **ignora** el paso "Add Firebase SDK" del asistente de Firebase: no hay carpeta `android/` en el repositorio (Expo la genera en cada build) y el plugin de Expo aplica Google Services por sí solo.

## 11. Cómo probarlo

**Automático** (en CI y local)

| Qué | Dónde |
|---|---|
| Validación del token, registro | `PushDeviceTests` |
| Texto, formato de dinero y hora de Costa Rica | `NotificationPushTextTests` |
| Quién recibe qué, tokens inválidos, que un fallo no se propague | `NotificationPusherTests` |
| Cuándo se envía y cuándo no (descartado, saltado) | `DeliverDueNotificationsHandlerTests` |
| Formato de la petición a Expo, lotes, tickets, fallos | `ExpoPushSenderTests` (Expo simulado) |
| Repositorio contra Mongo real (mover token, borrar acotado) | `MongoPushDeviceRepositoryTests` |
| **Punta a punta**: comprobante por el portal → push capturado en segundos, sin correr mantenimiento | `PushDevicesApiTests` |
| App: permisos, canal, token, registro, olvidar con tope, eventos | `pushManager.test.ts`, `PushRegistration.test.tsx`, `pushSignOut.test.ts`, `push.test.ts`, `pushDevicesApi.test.ts` |

Nota (Windows): Smart App Control puede bloquear el DLL recién compilado y hacer fallar *todas* las pruebas con `An Application Control policy has blocked this file`. No es un fallo del código: corre la suite en un contenedor sobre una copia sin `bin/obj` (CLAUDE.md §6).

**Manual, en un teléfono Android** (hecho el 2026-10-28; sirve para repetirla tras cambios nativos)

1. Instala el APK de desarrollo (enlace del build en expo.dev).
2. En la computadora: `cd mobile && npx expo start --dev-client`; en el teléfono abre la app y conéctala (misma red, QR o URL).
3. Crea una cuenta (o inicia sesión) y **acepta** el permiso de notificaciones.
4. Crea una reserva con el correo del cliente y genera su enlace.
5. Abre el enlace `/b/{token}` (en develop, con un correo autorizado en Cloudflare Access), pide el código y sube un comprobante.
6. En pocos segundos debe sonar "Comprobante por verificar". Tócalo: abre la reserva. Con la app abierta debe verse un banner y la bandeja recargarse.
7. Cierra sesión y vuelve a subir otro comprobante: ya no debe llegar nada a ese teléfono.

## 12. Si algo falla

| Síntoma | Causa probable | Qué mirar |
|---|---|---|
| No llega nada y la bandeja sí tiene el aviso | El teléfono no está registrado, o Expo/FCM no entregó | Mongo `push_devices` (¿hay un documento con tu `photographerId`?); logs de la API (`Expo refused…`, `Expo reported an error…`) |
| `push_devices` vacío | La app no pidió o no obtuvo el token | Permiso denegado; dispositivo emulado; build sin `extra.eas.projectId`; `google-services.json` ausente en el build |
| La app responde `failed` al registrar | Sin red, sesión vencida, o el token no es de Expo | Respuesta de `POST /api/push-devices` (422 `push.invalid_token`) |
| Expo responde `InvalidCredentials` / `MismatchSenderId` | Falta o está mal la clave FCM V1 en EAS, o el `google-services.json` es de otro proyecto | expo.dev → Credentials; que el paquete coincida |
| `DeviceNotRegistered` | La app se desinstaló o cambió el token | Se borra solo; reinstala y vuelve a iniciar sesión |
| Llega con retraso | El despertador no corrió y esperó al cron del worker | Logs `The maintenance pass triggered by a booking change failed`; que la petición fuera exitosa y a `/api/bookings` o `/public/bookings` |
| Llega pero no abre la reserva | El `data` no trae `bookingId` o la ruta no existe | `readBookingId` ignora datos mal formados |
| En Expo Go no hace nada | Esperado: no soporta push remoto | Usa el APK de desarrollo |
| Todas las pruebas locales fallan con "Application Control policy" | Smart App Control | Ejecutar en contenedor (ver §11) |

## 13. Seguridad, privacidad y límites

- **Aislamiento entre fotógrafos:** los endpoints toman el `PhotographerId` solo del token; `unregister` solo borra un token del propio fotógrafo.
- **El token de Expo identifica un teléfono, no autentica a quien envía.** Con la configuración por defecto de Expo, la API de envío no exige credenciales: quien conozca un token podría mandarle mensajes a ese teléfono. La *seguridad mejorada* de Expo lo cierra exigiendo un token de acceso (`EXPO_ACCESS_TOKEN`), y está en el TODO. Los tokens solo viajan por HTTPS, se guardan en `push_devices` y no se escriben en los logs.
- **Contenido en la pantalla de bloqueo:** el push incluye el nombre del cliente y montos. Es lo que hace útil el aviso, pero quien vea el teléfono bloqueado lo ve. Un modo "privado" es un TODO.
- **La clave de cuenta de servicio de FCM** permite enviar notificaciones con tu proyecto de Firebase: solo en EAS.
- **`google-services.json`** no es secreto en sentido estricto (las claves de Android están ligadas al paquete), pero no se versiona por ser un repositorio público.
- **Entrega "como máximo una vez" por aviso:** ver decisión 3; un push puede perderse, no duplicarse.
- **Costo en Atlas M0 (100 ops/s):** el despertador añade unas pocas consultas por cambio de reserva (máx. 1 pasada/s). Es despreciable con pocos fotógrafos; vigilar si crece.

## 14. TODO

Ordenados por prioridad.

**Bloqueantes**

- [x] **Probar un push real en un teléfono Android** (§11). Hecho el 2026-10-28: el tramo Expo → FCM → teléfono funciona.
- [x] Fusionar el PR (#31) y confirmar que el despliegue en develop arranca con el despertador sin errores. Fusionado el 2026-10-16; el despliegue de develop del 2026-10-29 (PR #32) terminó bien y `/health/ready` responde `Healthy`.

**Antes de usarlo con clientes reales**

- [ ] **Perfil `production` de EAS** y proyecto/credenciales de producción (keystore propio, `EXPO_PUBLIC_API_URL` de producción, Firebase de producción o el mismo con apps separadas).
- [ ] **Distribución:** Google Play (~25 USD, pago único) o APK directo; el perfil `preview` para pruebas internas sin servidor de desarrollo.
- [ ] **Activar la seguridad mejorada de Expo** y definir `EXPO_ACCESS_TOKEN` en Railway (API y worker).
- [ ] **Ícono de notificación y colores de marca** en Android (el ícono pequeño debe ser monocromo): depende de definir la paleta y los íconos (CLAUDE.md §3, brecha 6).
- [ ] **Privacidad en pantalla de bloqueo:** texto genérico cuando el teléfono está bloqueado (visibilidad de la notificación en Android) o un ajuste para elegirlo.

**iOS**

- [ ] Apple Developer (99 USD/año), clave APNs subida a EAS, `eas build --platform ios`, probar permisos y banner. Revisar `app.json` (`ios.infoPlist`) si hace falta.

**Fiabilidad**

- [ ] **Leer los recibos de Expo** (se consultan unos 15 min después del envío) para detectar errores tardíos de FCM/APNs y borrar tokens inválidos que el ticket no reportó.
- [ ] **Reintentos del push con retroceso** (hoy un fallo transitorio pierde el push) y/o un outbox propio de pushes para no perder uno si el proceso muere entre guardar `Delivered` y enviar.
- [ ] **Limpiar tokens viejos** (TTL sobre `lastSeenAt`, p. ej. 90 días).
- [ ] **Métricas y alarmas:** pushes enviados/fallidos, latencia comprobante → push, errores del despertador.
- [ ] Prueba automática de punta a punta en dispositivo (Maestro o Detox).

**Producto**

- [ ] **Preferencias por tipo y horas silenciosas** (p. ej. no avisar de saldo de noche) y un interruptor general en la app.
- [ ] **Acciones desde la notificación** (Verificar / Rechazar sin abrir la app).
- [ ] **Agrupar** varios comprobantes en una sola notificación; contador (*badge*) en el ícono.
- [ ] **Zona horaria e idioma por fotógrafo** (hoy fijos: UTC-6, español).
- [ ] Pantalla en la app que muestre el estado del push ("Notificaciones activadas / desactivadas, abrir ajustes") para el caso de permiso denegado.
- [ ] Más avisos cuando existan Gallery y Billing (selección enviada, plazo de entrega, reembolso por devolver).
- [ ] Canal para el **cliente** (fase 3: WhatsApp automatizado o correo); no push.

**Técnico**

- [ ] Valorar un *change stream* de Mongo en lugar del despertador por petición si el volumen crece.
- [ ] Actualizar `eas-cli` (hay versiones nuevas) y revisar la deprecación de Node 20 en las acciones de CI.
- [ ] Documentar en `mobile/` cómo regenerar el build cuando cambien módulos nativos.
