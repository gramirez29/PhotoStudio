# Outbox y Worker: cómo funciona

Documento de referencia del mecanismo que entrega los eventos de dominio de PhotoStudio y ejecuta los trabajos en segundo plano. Está escrito para que alguien que no vio el código pueda entender qué hace, por qué existe, dónde vive cada pieza y cómo se opera.

- **Rama de origen:** `feature/outbox-and-worker`
- **Alcance:** backend (.NET 10 + MongoDB). La app móvil no cambia.
- **Estado:** implementado y verificado de punta a punta. Todavía **no existe ningún consumidor** de eventos (ver §12).

---

## Índice

1. [El problema que resuelve](#1-el-problema-que-resuelve)
2. [Vista general](#2-vista-general)
3. [Mapa de archivos](#3-mapa-de-archivos)
4. [Parte 1: guardar el evento (escritura)](#4-parte-1-guardar-el-evento-escritura)
5. [Parte 2: el documento del outbox](#5-parte-2-el-documento-del-outbox)
6. [Parte 3: serialización de eventos](#6-parte-3-serialización-de-eventos)
7. [Parte 4: entregar el evento (OutboxProcessor)](#7-parte-4-entregar-el-evento-outboxprocessor)
8. [Parte 5: el Worker](#8-parte-5-el-worker)
9. [Parte 6: expiración de reservas (B7)](#9-parte-6-expiración-de-reservas-b7)
10. [Índices de MongoDB](#10-índices-de-mongodb)
11. [Despliegue y configuración](#11-despliegue-y-configuración)
12. [Cómo agregar un consumidor de eventos](#12-cómo-agregar-un-consumidor-de-eventos)
13. [Qué pasa cuando algo falla](#13-qué-pasa-cuando-algo-falla)
14. [Operación: inspeccionar y reparar](#14-operación-inspeccionar-y-reparar)
15. [Pruebas](#15-pruebas)
16. [Garantías, límites y decisiones](#16-garantías-límites-y-decisiones)

---

## 1. El problema que resuelve

Cuando algo importante ocurre en una reserva (se crea, se confirma, se cancela, vence), otras partes del sistema deben reaccionar: avisar al cliente, programar recordatorios, crear la galería, calcular un reembolso. El dominio ya produce esos hechos como **eventos de dominio** (`BookingCreated`, `BookingConfirmed`, `BookingCancelled`, etc.), pero antes de este cambio los eventos se acumulaban en memoria dentro del agregado y se perdían: nadie los guardaba ni los entregaba.

La solución ingenua (guardar la reserva y *después* llamar a quien deba reaccionar) tiene un fallo clásico, el **doble escritura**:

| Orden | Qué pasa si el proceso se cae a la mitad |
|---|---|
| Guardar reserva → enviar evento | La reserva queda guardada y el evento **se pierde** para siempre. |
| Enviar evento → guardar reserva | El evento sale pero la reserva **no existe** (aviso falso). |

El patrón **Transactional Outbox** lo resuelve así: el evento se escribe en una colección de Mongo (`outbox_messages`) **en la misma transacción** que el cambio de la reserva. O se guardan las dos cosas o ninguna. Después, un proceso aparte (el **worker**) lee esa colección y entrega cada evento a quien corresponda. Si algo falla, el mensaje sigue ahí y se reintenta.

El mismo worker aloja además el trabajo programado que faltaba: la **expiración de reservas tentativas (B7)**, que antes no corría nunca.

---

## 2. Vista general

```
                         MISMA TRANSACCIÓN DE MONGO
 ┌────────┐  ┌──────────────────────────────────────────────┐
 │  API   │  │  bookings            (documento de reserva)  │
 │ handler├─▶│  photographer_calendars (horario reservado)  │
 │        │  │  outbox_messages     (eventos pendientes)    │
 └────────┘  └──────────────────────────────────────────────┘
                                  │
                                  │  el worker sondea cada 5 s
                                  ▼
 ┌──────────────────────────── Worker ─────────────────────────────┐
 │ OutboxDispatcherService ──▶ OutboxProcessor                      │
 │      1. reclama 1 mensaje (lease de 2 min)                       │
 │      2. lo deserializa a su evento de dominio                    │
 │      3. ejecuta cada IDomainEventHandler<T> registrado           │
 │      4. éxito → Processed | fallo → reintento con backoff        │
 │                                                                  │
 │ BookingExpirationService ──▶ ExpireTentativeBookingsHandler      │
 │      cada 1 min: vence las reservas tentativas cuyo apartado     │
 │      terminó (B7) y libera su horario                            │
 └──────────────────────────────────────────────────────────────────┘
```

Diagrama de secuencia de una reserva nueva:

```mermaid
sequenceDiagram
    participant C as Cliente HTTP
    participant A as API (CreateBookingHandler)
    participant R as MongoBookingRepository
    participant M as MongoDB
    participant W as Worker (OutboxProcessor)
    participant H as IDomainEventHandler&lt;BookingCreated&gt;

    C->>A: POST /api/bookings
    A->>A: Booking.Create(...) → Raise(BookingCreated)
    A->>R: AddAsync(booking)
    R->>M: BEGIN transacción
    R->>M: reservar horario (photographer_calendars)
    R->>M: insertar reserva (bookings)
    R->>M: insertar mensaje (outbox_messages, Pending)
    R->>M: COMMIT
    R->>A: booking.ClearDomainEvents()
    A-->>C: 201 Created

    loop cada 5 s
        W->>M: FindOneAndUpdate (reclamar con lease)
        M-->>W: mensaje BookingCreated
        W->>H: HandleAsync(evento)
        H-->>W: ok
        W->>M: status = Processed, processedAt = ahora
    end
```

---

## 3. Mapa de archivos

Todas las rutas son relativas a `backend/`.

### Código nuevo

| Archivo | Capa | Rol |
|---|---|---|
| `src/PhotoStudio.Application/Abstractions/IDomainEventHandler.cs` | Application | Puerto: contrato que implementa cada consumidor de un evento. |
| `src/PhotoStudio.Application/Bookings/ExpireTentativeBookings/ExpireTentativeBookingsCommand.cs` | Application | Comando del trabajo B7 y su resultado (`Expired`, `Skipped`). |
| `src/PhotoStudio.Application/Bookings/ExpireTentativeBookings/ExpireTentativeBookingsHandler.cs` | Application | Caso de uso: vence las reservas cuyo apartado terminó. |
| `src/PhotoStudio.Infrastructure/Persistence/Documents/OutboxMessageDocument.cs` | Infrastructure | Modelo Mongo de un mensaje del outbox. |
| `src/PhotoStudio.Infrastructure/Persistence/Documents/OutboxMessageMappings.cs` | Infrastructure | `IDomainEvent` → `OutboxMessageDocument` (`ToOutboxMessage`). |
| `src/PhotoStudio.Infrastructure/Persistence/Outbox/DomainEventSerializer.cs` | Infrastructure | Evento ⇄ JSON, con registro de tipos por nombre. |
| `src/PhotoStudio.Infrastructure/Persistence/Outbox/OutboxProcessor.cs` | Infrastructure | Reclama, entrega, reintenta y estaciona mensajes. |
| `src/PhotoStudio.Worker/PhotoStudio.Worker.csproj` | Worker | Proyecto `Microsoft.NET.Sdk.Worker`. |
| `src/PhotoStudio.Worker/Program.cs` | Worker | Composition root del worker. |
| `src/PhotoStudio.Worker/OutboxDispatcherService.cs` | Worker | Bucle que sondea el outbox. |
| `src/PhotoStudio.Worker/BookingExpirationService.cs` | Worker | Bucle que ejecuta la expiración B7. |

### Código modificado

| Archivo | Cambio |
|---|---|
| `src/PhotoStudio.Infrastructure/Persistence/MongoBookingRepository.cs` | Escribe los eventos en el outbox dentro de la transacción; limpia `DomainEvents` tras el commit; nuevo `ListExpiredTentativeAsync`. |
| `src/PhotoStudio.Infrastructure/Persistence/MongoIndexInitializer.cs` | Crea los índices de expiración y del outbox (incluido el TTL). |
| `src/PhotoStudio.Infrastructure/DependencyInjection.cs` | Registra `OutboxProcessor` como singleton. |
| `src/PhotoStudio.Application/Abstractions/IBookingRepository.cs` | Nuevo método `ListExpiredTentativeAsync`. |
| `src/PhotoStudio.Application/DependencyInjection.cs` | Registra `ExpireTentativeBookingsHandler`. |
| `PhotoStudio.slnx` | Agrega el proyecto Worker a la solución. |
| `Directory.Packages.props` | Versión central de `Microsoft.Extensions.Hosting` (10.0.12). |
| `Dockerfile` | Publica también el worker (`/app/worker`). |

### Piezas ya existentes que el mecanismo usa

| Archivo | Por qué importa |
|---|---|
| `src/PhotoStudio.Domain/Common/AggregateRoot.cs` | Acumula los eventos (`Raise`), los expone (`DomainEvents`) y los borra (`ClearDomainEvents`). |
| `src/PhotoStudio.Domain/Common/IDomainEvent.cs` | Contrato mínimo del evento: `OccurredAt`. |
| `src/PhotoStudio.Domain/Bookings/Events/BookingEvents.cs` | Los 12 eventos concretos (records inmutables). |
| `src/PhotoStudio.Domain/Bookings/Booking.cs` | Método `Expire(now)` (B7), con sus guardas. |

### Pruebas nuevas

| Archivo | Qué cubre |
|---|---|
| `tests/PhotoStudio.Application.UnitTests/Bookings/ExpireTentativeBookingsHandlerTests.cs` | El caso de uso de expiración (4 pruebas). |
| `tests/PhotoStudio.Infrastructure.IntegrationTests/MongoBookingRepositoryOutboxTests.cs` | Escritura atómica del outbox y la consulta de expiración (6 pruebas). |
| `tests/PhotoStudio.Infrastructure.IntegrationTests/OutboxProcessorTests.cs` | Entrega, reintentos, lease, orden, mensajes ilegibles (8 pruebas). |
| `tests/PhotoStudio.Infrastructure.IntegrationTests/MutableTimeProvider.cs` | Reloj controlable para probar esperas sin dormir. |

---

## 4. Parte 1: guardar el evento (escritura)

**Archivo:** `src/PhotoStudio.Infrastructure/Persistence/MongoBookingRepository.cs`

### 4.1 De dónde salen los eventos

El dominio levanta eventos con `Raise(...)` dentro de cada transición. Por ejemplo, `Booking.Expire` (en `Booking.cs`):

```csharp
TransitionTo(BookingStatus.Expired, Actor.System, null, null, now);
Raise(new BookingExpired(Id, now));
```

`AggregateRoot` los guarda en una lista privada y los expone como `DomainEvents`. Hasta aquí nada se persiste.

### 4.2 Qué hace el repositorio ahora

Tres métodos del repositorio escriben la reserva y ahora también el outbox:

| Método | Cuándo se usa | Transacción |
|---|---|---|
| `AddAsync` | Crear reserva | Siempre: reserva horario + inserta reserva + inserta mensajes. |
| `UpdateReservingSlotAsync` | Reprogramar, revertir ausencia | Siempre: reemplaza reserva + libera/reserva horario + inserta mensajes. |
| `UpdateAsync` (reserva ya no activa) | Cancelar, completar, ausente, expirar | Siempre: reemplaza reserva + libera horario + inserta mensajes. |
| `UpdateAsync` (reserva sigue activa) | Firmar contrato, registrar pago | **Con eventos:** transacción. **Sin eventos:** una sola escritura simple, como antes. |

El patrón común es:

```csharp
var document = booking.ToDocument(expectedVersion + 1);
var messages = PendingMessages(booking);          // 1) convertir eventos a documentos ANTES de la transacción

using var session = await database.Client.StartSessionAsync(cancellationToken: cancellationToken);
await session.WithTransactionAsync(
    async (transactionSession, token) =>
    {
        // ... escritura de la reserva / horario ...
        await StoreMessagesAsync(transactionSession, messages, token);   // 2) insertar mensajes en LA MISMA transacción
        return true;
    },
    MajorityTransaction,
    cancellationToken);

booking.ClearDomainEvents();                      // 3) solo si el commit tuvo éxito
```

Las tres decisiones importantes:

1. **Los documentos del outbox se construyen antes de abrir la transacción.** El driver de Mongo puede reintentar el cuerpo de una transacción ante errores transitorios. Si los `Id` se generaran dentro, cada reintento crearía mensajes distintos. Construidos afuera, el reintento inserta exactamente los mismos.
2. **`ClearDomainEvents()` va después del commit.** Si la transacción falla (por ejemplo `ConflictException` por doble reserva), la excepción sale antes de limpiar: los eventos siguen en el agregado y **no hay mensaje en Mongo**. El evento de un cambio que nunca ocurrió jamás se publica.
3. **Un cambio sin eventos no paga el costo de una transacción** (rama rápida de `ReplaceKeepingSlotAsync`).

### 4.3 Helpers

```csharp
private static List<OutboxMessageDocument> PendingMessages(Booking booking) =>
    [.. booking.DomainEvents.Select(domainEvent => domainEvent.ToOutboxMessage())];

private Task StoreMessagesAsync(IClientSessionHandle session, IReadOnlyList<OutboxMessageDocument> messages, CancellationToken cancellationToken) =>
    messages.Count == 0
        ? Task.CompletedTask
        : _outbox.InsertManyAsync(session, messages, cancellationToken: cancellationToken);
```

`ReplaceKeepingSlotAsync` es el método privado que atiende la reserva "que sigue activa". Si hay mensajes abre una transacción; si el reemplazo no coincide (versión vieja) llama a `ThrowForUnmatchedActiveUpdateAsync`, que lanza `ConflictException` y aborta la transacción, así que tampoco se guarda el mensaje.

### 4.4 Garantía resultante

> Un evento existe en `outbox_messages` **si y solo si** el cambio que lo originó quedó confirmado en `bookings`.

Está probado en `MongoBookingRepositoryOutboxTests` (ver §15).

---

## 5. Parte 2: el documento del outbox

**Archivo:** `src/PhotoStudio.Infrastructure/Persistence/Documents/OutboxMessageDocument.cs`
**Colección:** `outbox_messages` (constante `OutboxProcessor.CollectionName`)

| Campo Mongo | Propiedad | Tipo | Significado |
|---|---|---|---|
| `_id` | `Id` | Guid (v7) | Identificador del mensaje. Al ser UUID v7 es ordenable por tiempo. |
| `type` | `Type` | string | Nombre del tipo del evento (`"BookingCreated"`). Es la clave del registro de tipos. |
| `payload` | `Payload` | string | El evento serializado como JSON. |
| `occurredAt` | `OccurredAt` | date UTC | Cuándo ocurrió el evento. Define el orden de entrega. |
| `status` | `Status` | string | `Pending`, `Processed` o `Failed`. |
| `attempts` | `Attempts` | int | Cuántas entregas se han intentado (se incrementa al reclamar). |
| `nextAttemptAt` | `NextAttemptAt` | date UTC | No se reclama antes de este instante. Inicia en `1970-01-01` (vence ya). |
| `lockedUntil` | `LockedUntil` | date UTC o ausente | Lease: mientras sea futuro, otro worker no lo toca. |
| `processedAt` | `ProcessedAt` | date UTC o ausente | Cuándo se entregó. Activa el borrado automático (TTL). |
| `lastError` | `LastError` | string o ausente | Mensaje del último error. |

### 5.1 Estados

```
              reclamar + entregar OK
   Pending ─────────────────────────────▶ Processed ──(30 días)──▶ borrado por TTL
      │  ▲
      │  └── fallo: attempts < 8 → vuelve a Pending con nextAttemptAt futuro
      │
      └────── fallo: attempts ≥ 8, o evento ilegible ──▶ Failed  (revisión manual)
```

Ejemplo de documento real:

```json
{
  "_id": "01a11d43-d253-7c02-9cde-6aa952483a30",
  "type": "BookingCreated",
  "payload": "{\"bookingId\":\"01a11d43-d212-7843-b77c-b6e78191ebe4\",\"photographerId\":\"7d6b4d90-...\",\"sessionStart\":\"2026-10-13T15:00:00+00:00\",\"expiresAt\":\"2026-10-10T...\",\"occurredAt\":\"2026-10-08T20:45:44+00:00\"}",
  "occurredAt": "2026-10-08T20:45:44Z",
  "status": "Processed",
  "attempts": 1,
  "nextAttemptAt": "1970-01-01T00:00:00Z",
  "processedAt": "2026-10-08T20:45:49Z"
}
```

### 5.2 Mapeo evento → documento

**Archivo:** `Documents/OutboxMessageMappings.cs`

```csharp
public static OutboxMessageDocument ToOutboxMessage(this IDomainEvent domainEvent)
{
    var (type, payload) = DomainEventSerializer.Serialize(domainEvent);
    return new OutboxMessageDocument
    {
        Id = Guid.CreateVersion7(),
        Type = type,
        Payload = payload,
        OccurredAt = domainEvent.OccurredAt.UtcDateTime,
        Status = OutboxMessageDocument.PendingStatus,
        NextAttemptAt = DateTime.UnixEpoch,   // vence de inmediato
    };
}
```

Respeta la regla del proyecto: los tipos de Mongo (`BsonId`, etc.) viven solo en Infrastructure; Domain y Application no los ven.

---

## 6. Parte 3: serialización de eventos

**Archivo:** `src/PhotoStudio.Infrastructure/Persistence/Outbox/DomainEventSerializer.cs`

Convierte un evento a `(tipo, json)` y de vuelta.

- **Formato:** `System.Text.Json` con `JsonSerializerDefaults.Web` (camelCase) y `JsonStringEnumConverter` (los enums viajan como texto: `"Photographer"`, `"InPerson"`).
- **Registro de tipos:** al cargarse la clase, escanea el ensamblado de Domain y arma un diccionario `nombre → Type` con todas las clases concretas que implementan `IDomainEvent`:

```csharp
private static readonly Dictionary<string, Type> EventTypes = typeof(IDomainEvent).Assembly
    .GetTypes()
    .Where(type => type is { IsAbstract: false, IsInterface: false } && typeof(IDomainEvent).IsAssignableFrom(type))
    .ToDictionary(type => type.Name);
```

- `Serialize` usa el **tipo en tiempo de ejecución** (`domainEvent.GetType()`), así que serializa todos los campos del record concreto y no solo `OccurredAt`.
- `Deserialize` lanza `InvalidOperationException` si el nombre no existe en el registro o si el JSON no se puede leer. El procesador interpreta eso como "mensaje irrecuperable".

> **Cuidado al evolucionar eventos.** Como el mensaje guarda el *nombre* del tipo, renombrar un record de evento rompe los mensajes que sigan pendientes (quedarían `Failed`). Agregar campos nuevos con valor por defecto es seguro; quitar o renombrar campos requiere vaciar el outbox antes o mantener compatibilidad.

---

## 7. Parte 4: entregar el evento (OutboxProcessor)

**Archivo:** `src/PhotoStudio.Infrastructure/Persistence/Outbox/OutboxProcessor.cs`
**Registro:** singleton en `Infrastructure/DependencyInjection.cs` (`services.AddSingleton<OutboxProcessor>()`).

Es la pieza central. Su método público es uno:

```csharp
public async Task<int> ProcessBatchAsync(int batchSize, CancellationToken cancellationToken)
```

Reclama y entrega hasta `batchSize` mensajes vencidos, del más antiguo al más nuevo. Devuelve cuántos reclamó.

### 7.1 Constantes

| Constante | Valor | Uso |
|---|---|---|
| `CollectionName` | `"outbox_messages"` | Nombre de la colección. |
| `MaxAttempts` | `8` | Intentos antes de estacionar el mensaje como `Failed`. |
| `LeaseDuration` | `2 minutos` | Cuánto retiene un worker un mensaje reclamado. |
| `BaseRetryDelay` | `5 s` | Espera tras el primer fallo. |
| `MaxRetryDelay` | `15 min` | Techo de la espera. |

### 7.2 Paso 1: reclamar un mensaje (`ClaimNextAsync`)

Un único `FindOneAndUpdate` atómico:

```csharp
filter.And(
    filter.Eq(m => m.Status, "Pending"),
    filter.Lte(m => m.NextAttemptAt, now),
    filter.Or(
        filter.Eq(m => m.LockedUntil, null),      // nunca reclamado (campo ausente o null)
        filter.Lte(m => m.LockedUntil, now)))     // o con lease vencido
```

```csharp
Builders<OutboxMessageDocument>.Update
    .Set(m => m.LockedUntil, now + LeaseDuration)   // toma el lease
    .Inc(m => m.Attempts, 1)                        // cuenta el intento

Sort = OccurredAt ascendente, luego Id ascendente
ReturnDocument = After
```

Por qué es seguro con varios workers: Mongo ejecuta el `FindOneAndUpdate` de forma atómica sobre un documento. Si dos workers compiten por el mismo mensaje, solo uno cumple el filtro (el segundo ya lo ve con `lockedUntil` futuro). Si un worker muere con un mensaje reclamado, cuando pasan 2 minutos el filtro vuelve a cumplirse y otro worker lo toma.

El **intento se cuenta al reclamar**, no al fallar. Así, un mensaje que tumba el proceso una y otra vez también termina agotando sus intentos en vez de reintentarse para siempre.

### 7.3 Paso 2: deserializar (`DeliverAsync`)

```csharp
domainEvent = DomainEventSerializer.Deserialize(message.Type, message.Payload);
```

Si lanza `InvalidOperationException` (tipo desconocido o JSON dañado), el mensaje se **estaciona de inmediato** como `Failed` con el error en `lastError`. Reintentar no arreglaría un mensaje ilegible.

### 7.4 Paso 3: ejecutar los consumidores (`InvokeHandlersAsync`)

```csharp
var handlerType = typeof(IDomainEventHandler<>).MakeGenericType(domainEvent.GetType());
var handleMethod = handlerType.GetMethod(nameof(IDomainEventHandler<IDomainEvent>.HandleAsync))!;

await using var scope = scopeFactory.CreateAsyncScope();
foreach (var handler in scope.ServiceProvider.GetServices(handlerType))
{
    await (Task)handleMethod.Invoke(handler, [domainEvent, cancellationToken])!;
}
```

- El evento se conoce solo en tiempo de ejecución (viene de Mongo), por eso se arma el tipo genérico `IDomainEventHandler<BookingCreated>` por reflexión y se piden **todos** los consumidores registrados para ese tipo al contenedor de DI.
- Se crea un **scope nuevo por mensaje**: los consumidores pueden depender de servicios `Scoped` (repositorios, etc.) y no comparten estado entre mensajes.
- Los consumidores corren **en secuencia**, en el orden de registro.
- La reflexión envuelve las excepciones en `TargetInvocationException`; el código la desenvuelve con `ExceptionDispatchInfo` para registrar el error real del consumidor.
- **Si no hay ningún consumidor** registrado para ese evento, el bucle no hace nada y el mensaje se marca igualmente como `Processed`. Es intencional: el outbox no se llena de eventos que nadie escucha.

### 7.5 Paso 4a: éxito

```csharp
.Set(Status, "Processed").Set(ProcessedAt, now).Set(LastError, null).Unset(LockedUntil)
```

### 7.6 Paso 4b: fallo (`RegisterFailureAsync`)

Cualquier excepción de un consumidor (salvo cancelación por apagado) entra aquí:

- Si `attempts >= 8` → `ParkAsync`: `Status = Failed`, se guarda `lastError`, se registra un log de **Error**.
- Si no → se calcula la espera y el mensaje sigue `Pending`:

```csharp
delay = min(15 min, 5 s × 2^(attempts − 1))
```

| Intento fallido | Espera antes del siguiente |
|---|---|
| 1 | 5 s |
| 2 | 10 s |
| 3 | 20 s |
| 4 | 40 s |
| 5 | 1 min 20 s |
| 6 | 2 min 40 s |
| 7 | 5 min 20 s |
| 8 | (se estaciona como `Failed`) |

En total un mensaje recibe 8 intentos repartidos en ≈ 9 minutos antes de estacionarse. Se guardan `nextAttemptAt = ahora + espera` y `lastError`, y se libera el lease.

### 7.7 Apagado ordenado

Si el host se apaga a mitad de una entrega, la excepción de cancelación **no** se trata como fallo del consumidor: se relanza sin tocar el documento. El lease expira solo y el siguiente worker entrega el mensaje de nuevo.

### 7.8 Logging

Mensajes estructurados con `[LoggerMessage]` (los tres incluyen `MessageId` y `EventType`):

| Método | Nivel | Cuándo |
|---|---|---|
| `LogDeliveryFailed` | Warning | Fallo con reintento programado. |
| `LogMessageParked` | Error | Agotó los 8 intentos. |
| `LogMessageUnreadable` | Error | No se pudo deserializar. |

---

## 8. Parte 5: el Worker

**Proyecto:** `src/PhotoStudio.Worker/` (`Microsoft.NET.Sdk.Worker`)

Es un proceso independiente de la API. Comparte las capas Application e Infrastructure, pero no expone HTTP.

### 8.1 Composition root (`Program.cs`)

```csharp
var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddApplication();          // handlers de casos de uso, TimeProvider
builder.Services.AddInfrastructure();       // Mongo, repositorio, OutboxProcessor, índices, políticas
builder.Services.AddHostedService<OutboxDispatcherService>();
builder.Services.AddHostedService<BookingExpirationService>();

await builder.Build().RunAsync();
```

`AddInfrastructure()` lee `MONGODB_CONNECTION_STRING` y `MONGODB_DATABASE_NAME` del entorno (igual que la API) y también registra `MongoIndexInitializer`, así que el worker asegura los índices al arrancar. La creación de índices es idempotente: no importa quién arranque primero.

### 8.2 `OutboxDispatcherService`

Bucle de sondeo:

```csharp
while (!stoppingToken.IsCancellationRequested)
{
    var claimed = 0;
    try   { claimed = await processor.ProcessBatchAsync(BatchSize, stoppingToken); }
    catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
    catch (Exception e) when (e is MongoException or TimeoutException) { LogPollFailed(logger, e); }

    if (claimed < BatchSize)
        await Task.Delay(PollInterval, timeProvider, stoppingToken)
                  .ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
}
```

| Parámetro | Valor |
|---|---|
| `BatchSize` | 50 mensajes por sondeo |
| `PollInterval` | 5 segundos |

- Si el lote vino **lleno** (hay más trabajo), vuelve a sondear de inmediato, sin dormir. Si vino incompleto, espera 5 s.
- Una caída de Mongo (`MongoException`, `TimeoutException`) se registra como Warning y el bucle continúa en el siguiente ciclo. **Nunca derriba el proceso**, coherente con la regla del proyecto de tolerar caídas de Mongo.
- `SuppressThrowing` evita una excepción al cancelar el `Task.Delay` durante el apagado.

### 8.3 `BookingExpirationService`

```csharp
await using var scope = scopeFactory.CreateAsyncScope();
var handler = scope.ServiceProvider
    .GetRequiredService<ICommandHandler<ExpireTentativeBookingsCommand, ExpireTentativeBookingsResult>>();

var result = await handler.HandleAsync(new ExpireTentativeBookingsCommand(BatchSize), stoppingToken);
if (result.Expired > 0 || result.Skipped > 0) LogRunCompleted(logger, result.Expired, result.Skipped);
```

| Parámetro | Valor |
|---|---|
| `BatchSize` | 100 reservas por corrida |
| `RunInterval` | 1 minuto |

Cada corrida crea un scope, resuelve el handler (que es `Scoped`) y lo ejecuta. Solo registra un log cuando tocó reservas, para no llenar los logs con corridas vacías. Mismo manejo de caídas de Mongo que el despachador.

---

## 9. Parte 6: expiración de reservas (B7)

Implementa la transición **B7** de la máquina de estados (`Tentative → Expired`, actor `System`).

### 9.1 Caso de uso

**Archivo:** `src/PhotoStudio.Application/Bookings/ExpireTentativeBookings/ExpireTentativeBookingsHandler.cs`

```csharp
var now = timeProvider.GetUtcNow();
var due = await repository.ListExpiredTentativeAsync(now, command.BatchSize, cancellationToken);

foreach (var booking in due)
{
    try
    {
        booking.Expire(now);
        await repository.UpdateAsync(booking, cancellationToken);
        expired++;
    }
    catch (Exception exception) when (exception is DomainException or ConflictException)
    {
        skipped++;
    }
}
return new ExpireTentativeBookingsResult(expired, skipped);
```

- `booking.Expire(now)` **reevalúa las guardas del dominio** al ejecutarse (principio 8 del diseño): que la reserva siga `Tentative`, que `now ≥ ExpiresAt` y que **no haya un comprobante de pago pendiente de verificar**. Si alguna falla lanza `DomainException` y la reserva se cuenta como omitida.
- `repository.UpdateAsync` guarda la reserva ya `Expired`; como deja de estar activa, el repositorio **libera su horario y escribe `BookingExpired` en el outbox** en la misma transacción (§4).
- `ConflictException` significa que otra escritura ganó la carrera (por ejemplo, un pago se verificó en ese instante, escenario 12 del diseño). Se omite sin error; gana el primero.
- Cualquier otra excepción (por ejemplo una caída de Mongo) **no** se captura aquí: sube al servicio, que la registra y reintenta en el siguiente ciclo.
- El trabajo es **idempotente**: repetirlo no cambia nada, porque una reserva ya `Expired` no vuelve a aparecer en la consulta.

### 9.2 Consulta

**Archivo:** `MongoBookingRepository.ListExpiredTentativeAsync`

```csharp
.Find(booking => booking.Status == nameof(BookingStatus.Tentative)
    && booking.ExpiresAt <= now.UtcDateTime
    && !booking.Payments.Any(payment => payment.Status == nameof(PaymentStatus.PendingVerification)))
.SortBy(booking => booking.ExpiresAt)
.Limit(limit)
```

Se excluyen en la consulta las reservas con comprobante pendiente porque el dominio se negaría a expirarlas. Si no se filtraran, quedarían siempre al frente de la cola (ordenada por `expiresAt`) y, con 100 o más, taparían a las demás reservas vencidas. Cuando ese comprobante se rechace, la reserva vuelve a aparecer en la consulta y expira en la siguiente corrida (escenario 3 del diseño).

---

## 10. Índices de MongoDB

**Archivo:** `src/PhotoStudio.Infrastructure/Persistence/MongoIndexInitializer.cs`
Se crean al arrancar la API o el worker. Si Mongo no responde, se registra un Warning y se reintenta en el siguiente arranque (no se cae el contenedor).

| Colección | Índice | Claves | Para qué |
|---|---|---|---|
| `bookings` | `ix_photographer_slot` | `photographerId`, `slotStart` | (ya existía) listado por fotógrafo. |
| `bookings` | `ix_status_expires` | `status`, `expiresAt` | Consulta de expiración B7. |
| `outbox_messages` | `ix_outbox_pending` | `status`, `nextAttemptAt`, `occurredAt` | Reclamo del siguiente mensaje vencido, en orden. |
| `outbox_messages` | `ix_outbox_retention` | `processedAt` (TTL 30 días) | Borra solo los mensajes entregados. |

Sobre el TTL: MongoDB elimina un documento cuando `processedAt` es una fecha más vieja que 30 días. Los mensajes `Pending` o `Failed` no tienen `processedAt`, por lo que **nunca** se borran automáticamente. La retención existe porque Atlas M0 tiene solo 0.5 GB. La constante es `MongoIndexInitializer.OutboxRetention`.

---

## 11. Despliegue y configuración

### 11.1 Una imagen, dos servicios

**Archivo:** `Dockerfile`

El build publica ambos proyectos y la imagen final contiene los dos:

```
/app/PhotoStudio.Api.dll           ← punto de entrada por defecto (API)
/app/worker/PhotoStudio.Worker.dll ← worker
```

| Servicio en Railway | Imagen | Comando de inicio |
|---|---|---|
| `photostudio-api` | La del Dockerfile | (por defecto) `dotnet PhotoStudio.Api.dll` |
| `photostudio-worker` | **La misma** | `dotnet worker/PhotoStudio.Worker.dll` |

El servicio del worker no necesita puerto ni health check HTTP. Hay que crearlo manualmente en Railway (Root Directory = `backend`, mismo Dockerfile).

### 11.2 Variables de entorno

Las mismas que la API:

| Variable | Obligatoria | Descripción |
|---|---|---|
| `MONGODB_CONNECTION_STRING` | Sí | Cadena de conexión (debe ser un replica set: se usan transacciones). |
| `MONGODB_DATABASE_NAME` | No (`photostudio`) | Nombre de la base. |
| `BOOKING_*` | No | Políticas. El worker no crea reservas, pero `AddInfrastructure` registra el proveedor de políticas. |

Los intervalos y tamaños de lote (`PollInterval`, `BatchSize`, `RunInterval`, `MaxAttempts`, `LeaseDuration`, `OutboxRetention`) son **constantes en el código**, no variables de entorno. Cambiarlos requiere un nuevo despliegue.

### 11.3 Ejecución local

```bash
cd backend
docker compose up -d --wait                                   # Mongo local en 127.0.0.1:27019
dotnet run --project src/PhotoStudio.Api    --launch-profile PhotoStudio.Api   # API en :8080
dotnet run --project src/PhotoStudio.Worker                                    # worker (otra terminal)
```

El worker necesita las mismas variables que la API. Con el perfil local de la API, la cadena es `mongodb://localhost:27019/?directConnection=true`; para el worker hay que exportarla:

```bash
export MONGODB_CONNECTION_STRING="mongodb://localhost:27019/?directConnection=true"
export MONGODB_DATABASE_NAME=photostudio_dev
```

---

## 12. Cómo agregar un consumidor de eventos

**Estado actual: no hay ningún consumidor.** Los eventos se guardan, se entregan al vacío y se marcan `Processed`. Cuando exista, por ejemplo, el módulo de Notifications, se agrega así; no hay que tocar el worker ni el procesador.

**1. Implementar el puerto** (`Application`):

```csharp
public sealed class SendBookingLinkOnBookingCreated(INotificationSender sender)
    : IDomainEventHandler<BookingCreated>
{
    /// <summary>Sends the booking link to the client. Idempotent: sending twice must not duplicate the message.</summary>
    public async Task HandleAsync(BookingCreated domainEvent, CancellationToken cancellationToken)
    {
        await sender.SendBookingLinkAsync(domainEvent.BookingId, cancellationToken);
    }
}
```

**2. Registrarlo** en `src/PhotoStudio.Application/DependencyInjection.cs` como `Scoped`:

```csharp
services.AddScoped<IDomainEventHandler<BookingCreated>, SendBookingLinkOnBookingCreated>();
```

Se pueden registrar varios consumidores para el mismo evento; correrán en secuencia.

**Reglas que debe cumplir un consumidor:**

1. **Idempotente.** La entrega es *al menos una vez*: el mismo evento puede llegar dos veces (ver §16). Debe usar el `BookingId`/`PaymentId` del evento, o una clave de idempotencia, para no duplicar su efecto.
2. **Lanzar excepción si no pudo completar.** Eso dispara el reintento con backoff. Tragarse el error significa perder el evento.
3. **Si hay varios consumidores y uno falla**, el mensaje completo se reintenta y **todos** vuelven a ejecutarse. Por eso la idempotencia aplica a cada uno.
4. **Terminar en menos de 2 minutos** (el lease). Un consumidor más lento podría hacer que otro worker reclame y entregue el mismo mensaje en paralelo.
5. **No leer colecciones de otros módulos directamente**; usar interfaces de Application (regla de arquitectura del proyecto).

Tabla de qué evento debe provocar qué reacción: ver §8.9 del documento de diseño (`CLAUDE.md`).

---

## 13. Qué pasa cuando algo falla

| Escenario | Resultado |
|---|---|
| La API guarda la reserva y el proceso muere justo después | No hay pérdida: reserva y mensaje se escribieron en la misma transacción. El worker lo entrega cuando corra. |
| La transacción falla (doble reserva, versión vieja, Mongo caído) | No se guarda reserva **ni** mensaje. Los eventos siguen en el agregado en memoria; la API responde 409/500. |
| Un consumidor lanza excepción | Mensaje sigue `Pending`, `attempts`+1, `nextAttemptAt` en el futuro, `lastError` guardado. Reintento con backoff. |
| Un consumidor falla 8 veces | `Failed`. Log de Error. No se vuelve a reclamar. Requiere intervención (§14). |
| El worker muere con un mensaje reclamado | El lease (2 min) expira y otro worker, o el mismo al reiniciar, lo vuelve a entregar. |
| El worker muere después de que el consumidor terminó pero antes de marcar `Processed` | Se entrega **otra vez**. Por eso los consumidores deben ser idempotentes. |
| Mongo no responde al sondear | Warning en logs; el bucle reintenta en el siguiente ciclo. El proceso no se cae. |
| El JSON/tipo del mensaje no se puede leer | `Failed` de inmediato (no tiene sentido reintentar). |
| Apagado ordenado del worker (SIGTERM) | Se cancela sin marcar fallo; el lease vence y se re-entrega. |
| Dos workers corriendo a la vez | Seguro: el reclamo atómico evita que entreguen el mismo mensaje simultáneamente. |
| Un pago se verifica justo cuando corre la expiración | Gana quien escriba primero (concurrencia optimista). Si pierde la expiración, se cuenta como `Skipped`. |
| Una reserva tiene un comprobante pendiente | No expira; no aparece en la consulta de expiración. |

---

## 14. Operación: inspeccionar y reparar

Comandos `mongosh` (ejemplo con el Mongo local del compose):

```bash
docker exec -it photostudio-mongo mongosh photostudio_dev
```

**Resumen por estado:**

```js
db.outbox_messages.aggregate([{ $group: { _id: "$status", total: { $sum: 1 } } }])
```

**Mensajes pendientes con retraso** (algo no está consumiendo):

```js
db.outbox_messages.find({ status: "Pending", nextAttemptAt: { $lt: new Date(Date.now() - 5 * 60 * 1000) } })
```

**Mensajes estacionados, con su error:**

```js
db.outbox_messages.find({ status: "Failed" }, { type: 1, attempts: 1, lastError: 1, occurredAt: 1 })
```

**Reencolar un mensaje `Failed`** (después de corregir la causa):

```js
db.outbox_messages.updateOne(
  { _id: UUID("<id-del-mensaje>") },
  { $set: { status: "Pending", attempts: 0, nextAttemptAt: new Date(0) },
    $unset: { lastError: "", lockedUntil: "" } })
```

**Ver los eventos de una reserva:**

```js
db.outbox_messages.find({ payload: /<booking-id>/ }).sort({ occurredAt: 1 })
```

**Señales de salud en logs del worker:**

- `Booking expiration run: N expired, M skipped.` → la expiración está viva.
- `Outbox message ... failed on attempt ...; retrying in ...` → un consumidor está fallando (Warning).
- `... was parked for manual review.` → hay mensajes `Failed` (Error, requiere acción).
- `The outbox poll failed` / `The booking expiration run failed` → Mongo no responde.

---

## 15. Pruebas

Ejecutar todo: `cd backend && dotnet test`. Las de integración usan el Mongo del compose (`mongodb://localhost:27019/?directConnection=true`, o `MONGODB_TEST_CONNECTION_STRING`) y **se saltan solas** si no hay servidor. Cada clase usa su propia base temporal que se borra al terminar.

### `ExpireTentativeBookingsHandlerTests` (unitarias, repositorio simulado)

| Prueba | Verifica |
|---|---|
| `HandleAsync_WithDueBookings_ExpiresAndSavesEach` | Cada reserva vencida pasa a `Expired` y se guarda. |
| `HandleAsync_QueriesDueBookingsAtTheCurrentInstant` | Consulta con `now` y el tamaño de lote del comando. |
| `HandleAsync_WhenAGuardFails_SkipsTheBookingsWithoutSaving` | Reserva dentro del apartado → omitida, sin guardar. |
| `HandleAsync_WhenAnotherWriteWinsTheRace_SkipsThatBookingAndContinues` | `ConflictException` → omitida y el lote continúa. |

### `MongoBookingRepositoryOutboxTests` (integración)

| Prueba | Verifica |
|---|---|
| `AddAsync_StoresTheCreationEventInTheOutbox` | `BookingCreated` queda `Pending` y el agregado queda sin eventos. |
| `AddAsync_WithOverlappingSlot_StoresNoOutboxMessage` | Doble reserva → **cero** mensajes (atomicidad). |
| `UpdateAsync_OfAnActiveBooking_StoresTheNewEventInTheOutbox` | Firmar contrato guarda `ContractSigned`. |
| `UpdateAsync_WithStaleVersion_StoresNoOutboxMessage` | Perder la carrera de versión → sin mensaje. |
| `UpdateAsync_OfAnExpiredBooking_StoresTheExpirationEvent` | Salir de los estados activos guarda `BookingExpired`. |
| `ListExpiredTentativeAsync_ReturnsOnlyDueTentativeBookingsWithoutPendingProof` | Solo vencidas, tentativas y sin comprobante pendiente. |

### `OutboxProcessorTests` (integración)

| Prueba | Verifica |
|---|---|
| `ProcessBatchAsync_DeliversTheEventAndMarksTheMessageProcessed` | Entrega una vez y marca `Processed`. |
| `ProcessBatchAsync_DoesNotDeliverAProcessedMessageAgain` | Un mensaje procesado no se reentrega. |
| `ProcessBatchAsync_WithoutHandlers_MarksTheMessageProcessed` | Sin consumidores igual se marca procesado. |
| `ProcessBatchAsync_WhenTheHandlerFails_RetriesAfterTheBackoff` | Falla → `Pending`, no se reclama dentro de la espera, sí después. |
| `ProcessBatchAsync_AfterTheMaximumAttempts_ParksTheMessage` | Tras 8 fallos queda `Failed` y deja de reclamarse. |
| `ProcessBatchAsync_WithUnknownEventType_ParksTheMessageAtOnce` | Tipo desconocido → `Failed` en el primer intento. |
| `ProcessBatchAsync_RespectsTheLeaseOfAnotherWorker` | Con lease vigente no se reclama; vencido, sí. |
| `ProcessBatchAsync_DeliversOldestEventFirst` | Entrega ordenada por `occurredAt`. |

`MutableTimeProvider` permite avanzar el reloj en las pruebas para comprobar esperas y leases sin dormir.

### Verificación de punta a punta

Se corrió la imagen Docker (API + worker) contra el Mongo local: se creó una reserva por HTTP, el worker entregó `BookingCreated` (`Processed`, `attempts: 1`); luego se forzó `expiresAt` al pasado y en la corrida siguiente el worker registró `1 expired, 0 skipped`: la reserva quedó `Expired`, `photographer_calendars.entries` quedó vacío y `BookingExpired` se entregó.

---

## 16. Garantías, límites y decisiones

### Garantías

- **Atomicidad:** evento y cambio se confirman juntos o ninguno.
- **Al menos una vez:** un evento confirmado se entrega hasta lograrlo o hasta agotar 8 intentos; nunca se pierde en silencio (si no se logra, queda `Failed` visible).
- **Sin entrega concurrente del mismo mensaje** gracias al lease (siempre que el consumidor termine antes de que expire).
- **Sin pérdida por caídas** de API, worker o Mongo.

### Límites conocidos

1. **No es "exactamente una vez".** Puede haber duplicados; los consumidores deben ser idempotentes.
2. **Orden global solo con un worker.** El reclamo sigue `occurredAt`, pero con varios workers dos mensajes distintos pueden procesarse en paralelo o en otro orden. No hay orden garantizado entre eventos de distintas reservas, ni estricto entre eventos de la misma reserva con varios workers. Hoy se despliega un solo servicio de worker.
3. **Lease fijo de 2 minutos.** Un consumidor que tarde más puede recibir el mismo mensaje duplicado desde otro worker. No hay renovación de lease.
4. **Sondeo, no push.** La latencia de entrega es de hasta ~5 s. Es suficiente para notificaciones y recordatorios; si algún día hace falta menos, se puede usar un change stream de Mongo.
5. **Los eventos ya procesados no se reenvían** cuando se agregue un consumidor nuevo. Un consumidor nuevo solo ve eventos futuros (y los que sigan `Pending`).
6. **Parámetros fijos en código** (intervalos, lotes, intentos, retención).
7. **Nombre de tipo como clave:** renombrar un evento afecta los mensajes pendientes (§6).
8. **Starvation de la expiración atenuada, no eliminada:** la consulta ordena por `expiresAt`; si una reserva falla de forma persistente por una causa distinta al comprobante pendiente, volvería a aparecer en cada corrida (lote de 100).

### Decisiones tomadas y por qué

| Decisión | Motivo |
|---|---|
| Outbox en Mongo, no un broker externo (Kafka, SQS, etc.) | Evita infraestructura nueva y costo; Atlas M0 y el replica set local ya soportan transacciones; el volumen es bajo. |
| Un `FindOneAndUpdate` con lease en vez de un lock distribuido | Es atómico por documento, no requiere servicios extra y se recupera solo de caídas. |
| El intento se cuenta al reclamar | Un mensaje "veneno" que tumba el proceso agota sus intentos en vez de reintentarse indefinidamente. |
| Mensaje ilegible → `Failed` inmediato | Reintentar no puede arreglar un JSON dañado ni un tipo inexistente. |
| Marcar `Processed` aunque no haya consumidores | Evita acumular eventos que nadie lee; la retención TTL cuida el límite de 0.5 GB. |
| Reflexión para despachar a `IDomainEventHandler<T>` | El tipo del evento solo se conoce en tiempo de ejecución. Es una sola llamada por mensaje; el costo es despreciable frente a la E/S de Mongo. |
| `OutboxProcessor` en Infrastructure y los `BackgroundService` en Worker | La lógica de Mongo vive con la persistencia y se prueba con integración; el Worker solo decide *cuándo* ejecutarla. |
| Expiración en Application como caso de uso con `TimeProvider` | Sigue las reglas del proyecto (Clean Architecture, hora inyectada, pruebas deterministas) y el job no contiene reglas de negocio: las guardas siguen en el dominio. |
| Misma imagen para API y worker | Regla de infraestructura del proyecto: "misma imagen, otro comando". |

### Próximos pasos relacionados

- Escribir los primeros consumidores (Notifications: enlace al cliente, recordatorios; Gallery: crear galería en `SessionCompleted`).
- Recordatorios programados por el worker (dependen de Notifications).
- Respaldo semanal `mongodump` → R2 como otra tarea del worker.
- Crear el servicio `photostudio-worker` en Railway.
