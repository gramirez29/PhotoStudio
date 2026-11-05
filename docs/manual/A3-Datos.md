# A3 — Datos

## 1. MongoDB

Base: `photostudio` (producción, pendiente) y `photostudio_dev` (develop). Atlas M0: 0,5 GB, 100 operaciones por segundo, 500 conexiones, sin copias de seguridad, se pausa tras 30 días sin conexiones. El pool está configurado en 50 conexiones.

Configuración del cliente: `GuidRepresentation.Standard`, montos `Decimal128`, enums como texto, `RetryReads` y `RetryWrites`, `ServerSelectionTimeout` y `ConnectTimeout` de 10 s. Si Mongo no responde al arrancar, la API arranca igual y `/health/ready` falla (no se cae el contenedor).

Los índices los crea `MongoIndexInitializer` al arrancar (API y worker). Todos son idempotentes.

### Colecciones

| Colección | Contenido | Índices y retención |
|---|---|---|
| `users` | Cuentas de fotógrafos: usuario, correo, hash de contraseña, nombre, teléfono, intentos fallidos, bloqueo, `isSystemOperator` | Usuario único; correo único (parcial: ignora cuentas antiguas sin correo) |
| `refresh_tokens` | Tokens de renovación de la app: **solo el hash**, familia de login, uso | Hash único; familia; fotógrafo; se borran solos (TTL) |
| `email_verifications` | Código de verificación del correo de la cuenta | TTL de 1 hora |
| `password_resets` | Código de 6 dígitos (solo su hash) para elegir una contraseña nueva | Un documento por usuario; se borra solo 1 hora después de vencer |
| `login_failures` | Fallos seguidos de login por cuenta y dirección (la dirección solo como hash con clave) y el fin del bloqueo | Un documento por cuenta y dirección (`_id`); se borra solo 1 día después del último fallo |
| `studio_sessions` | Sesiones de la web del fotógrafo (cookie): solo el hash | Hash único; usuario; TTL |
| `studio_devices` | Navegadores recordados (salta el código por correo 30 días) | Hash único; usuario; TTL |
| `studio_challenges` | Códigos de segundo factor de la web en curso | Hash único; TTL |
| `bookings` | **Reservas**: cliente, paquete, precio, extras, descuento, política congelada, contrato, pagos (incrustados), historial de transiciones | Fotógrafo + horario; fotógrafo + creación (listas paginadas); estado + vencimiento (expiración) |
| `photographer_calendars` | Un documento por fotógrafo con los rangos de horario ocupados | Una entrada por fotógrafo |
| `outbox_messages` | Eventos pendientes y entregados | Para reclamar el siguiente mensaje; TTL de 30 días tras entregar |
| `notifications` | Bandeja de avisos del fotógrafo | Clave de deduplicación **única**; vencimiento; bandeja; por reserva; retención |
| `push_devices` | Teléfonos registrados para push (token de Expo) | Por fotógrafo |
| `settlements` | Liquidación de una reserva que terminó con dinero pagado (retención y reembolso) | Una por reserva (única); reembolsos pendientes |
| `booking_links` | Enlaces del cliente: **solo el hash del token** | Hash único; por reserva; TTL de 30 días tras vencer |
| `portal_otps` | Códigos por correo del portal del cliente (HMAC) | TTL de 1 hora |
| `galleries` | **Galerías**: estado, términos, fotos (nombre, claves, tamaños), selección, finales, ZIP, contadores de descarga | Una por reserva (única); fotógrafo + creación |

### Qué contiene una reserva (`bookings`)
Fotógrafo (tenant), cliente (nombre, teléfono, correo opcional), paquete (nombre del servicio, nivel, precio base, extras con su precio de ese momento, descuento), precio total, **política congelada** (porcentaje de anticipo, horas de gracia, etc. copiadas al crearla), horario, estado, fecha de vencimiento, contrato (firma, versión de plantilla, texto exacto firmado, declaración del cliente, canal), pagos (monto, método, estado, referencia, clave de idempotencia, canal), historial de transiciones (estado anterior y nuevo, actor, canal, motivo, fecha), número de reprogramaciones y versión.

### Qué contiene una galería (`galleries`)
Reserva y fotógrafo, datos congelados del cliente y paquete, estado, fotos incluidas, de regalo y precio por foto extra, y la lista `photos` (máximo 150) con: id, nombre original, tamaño de la prueba, tamaño del final, tamaño de su miniatura, ancho, alto, fecha de captura, número de descargas. Más: selección (ids en orden y fecha de envío), fechas de publicación, entrega y liberación de extras, ZIP (tamaño, fecha, descargas) y los instantes de los avisos ya enviados. **El nombre original nunca forma parte de la clave de R2.**

## 2. R2 (fotos)

Bucket privado `photostudio-media`. Las claves las construye siempre el backend con identificadores aleatorios (`ObjectKey` solo acepta `a-zA-Z0-9-_./`, sin segmentos vacíos ni `..`):

| Objeto | Clave |
|---|---|
| Prueba | `galleries/{galería}/proofs/{foto}.jpg` |
| Miniatura de un final | `galleries/{galería}/thumbs/{foto}.jpg` |
| Final | `galleries/{galería}/finals/{foto}.jpg` |
| ZIP | `galleries/{galería}/zip/photos.zip` |

Límites: prueba hasta 1 MB, miniatura hasta 400 KB, final hasta 50 MB, ZIP hasta 500 MB, máximo 150 fotos por galería. Solo JPEG para fotos.

**Las fotos nunca pasan por la API.** Subida: la API entrega una URL firmada (15 minutos como máximo, con el tipo de contenido dentro de la firma) y el cliente hace `PUT` directo a R2. Al registrar, la API mira el objeto real (existe, tipo, tamaño) y borra lo que no cumple, porque una URL firmada no puede limitar el tamaño. Bajada: URLs firmadas de 5 a 30 minutos según el caso. Una URL ya emitida no se puede revocar; por eso su vida es corta.

**CORS del bucket:** debe permitir `PUT`, `GET` y `HEAD` con el encabezado `Content-Type` desde el origen del sitio (develop y `localhost:5173` ya están).

**Costos:** 10 GB gratis, luego unos $0,015 por GB al mes; salida de datos gratis. Una galería Premium pesa unos 300 MB de finales más el ZIP.

**Pendiente:** limpiar objetos huérfanos (subidas que se interrumpieron antes de registrarse). Hace falta listar el bucket por prefijo, y el puerto de almacenamiento todavía no lo hace.

## 3. Datos que no se guardan
- Contraseñas (solo su hash PBKDF2).
- Tokens de refresco, de enlace, de sesión web y códigos (solo hashes).
- Texto de los avisos: el backend guarda tipo y datos; la app redacta el texto (excepción: el texto fijo del push, en `NotificationPushText`).
- Archivos RAW: nunca se suben.
