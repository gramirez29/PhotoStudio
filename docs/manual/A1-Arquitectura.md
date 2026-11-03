# A1 — Arquitectura

## 1. El repositorio

```
PhotoStudio/
├── backend/            API y worker en .NET 10 (monolito modular, Clean Architecture)
│   ├── src/
│   │   ├── PhotoStudio.Domain/          Reglas de negocio puras (sin dependencias)
│   │   ├── PhotoStudio.Application/     Casos de uso (comandos y consultas) y puertos
│   │   ├── PhotoStudio.Infrastructure/  MongoDB, R2, correo, push, JWT
│   │   ├── PhotoStudio.Api/             Minimal APIs, autenticación, CORS, límites
│   │   └── PhotoStudio.Worker/          Segundo ejecutable: outbox, expiración, avisos, archivo
│   ├── tests/                           4 proyectos de pruebas (xUnit v3)
│   ├── scripts/                         configure-railway.mjs, grant-system-role.js, migrate-tenant.js
│   ├── Dockerfile  docker-compose.yml   Imagen de producción; Mongo y almacenamiento locales
│   └── PhotoStudio.slnx  Directory.Build.props  Directory.Packages.props  global.json
├── mobile/             App del fotógrafo (Expo SDK 57, React Native, TypeScript)
├── portal/             Sitio web (React 19 + Vite): inicio, portal del cliente, web del fotógrafo
├── docs/               Un documento por módulo + este manual
└── .github/workflows/  ci.yml y deploy-develop.yml
```

Hay además un `CLAUDE.md` en la raíz (privado, no se sube a git) con las decisiones de producto para quien trabaje con el asistente de código.

## 2. Tecnologías y por qué

| Capa | Tecnología | Nota |
|---|---|---|
| API | .NET 10 (LTS hasta nov-2028), C# actual, Minimal APIs | .NET 9 se descartó: fin de soporte nov-2026 |
| Base de datos | MongoDB Atlas M0 (gratis), `MongoDB.Driver` 3.12 | Sin copias de seguridad propias: hay que hacer `mongodump` |
| Archivos | Cloudflare R2 (compatible con S3), `AWSSDK.S3` | Bucket privado, todo por URL firmada |
| Pruebas backend | xUnit v3, NSubstitute, Shouldly | FluentAssertions v8 está prohibido (licencia comercial) |
| App | Expo SDK 57, React Native 0.86, TanStack Query 5, Zustand 5, Expo Router, react-native-paper | TypeScript 6.0.2, prohibido `any` |
| Sitio web | React 19, Vite, TypeScript | Cloudflare Pages |
| Correo | Resend | Dominio de envío `mail.photosensestudio.com` |
| Push | Expo Push Service sobre FCM (Android) | iOS pendiente (necesita Apple Developer) |
| CI/CD | GitHub Actions | Despliegue automático a develop |

## 3. Capas del backend y la regla de dependencias

```
Domain  ←  Application  ←  Infrastructure
                       ←  Api / Worker
```

- **Domain** no depende de nada: ni de MongoDB, ni de HTTP. Contiene los agregados (`Booking`, `Gallery`, `BookingSettlement`, `User`, `Notification`...), objetos de valor (`Money`, `TimeSlot`, `EmailAddress`), eventos y errores de dominio.
- **Application** define **puertos** (interfaces como `IBookingRepository`, `IObjectStorage`, `IEmailSender`) y **casos de uso**: un manejador por comando o consulta (`ICommandHandler<,>`, `IQueryHandler<,>`).
- **Infrastructure** implementa los puertos: repositorios de MongoDB, S3/R2, Resend, Expo Push, JWT, hasheo de contraseñas.
- **Api** y **Worker** son las raíces de composición: registran todo y no tienen lógica de negocio.

Reglas que se hacen cumplir:
1. Nunca tipos de MongoDB (`ObjectId`, `[BsonId]`) en Domain ni Application. Los documentos viven en `Infrastructure/Persistence/Documents` y se mapean a mano (`ToDocument` / `ToDomain`).
2. DTO de entrada (`*Request`, en Api) ≠ comando (Application) ≠ respuesta (`*Response`) ≠ documento de Mongo. Siempre mapeo explícito.
3. Los módulos (Bookings, Billing, Contracts, Galleries, Notifications, Identity, Portal) se comunican por interfaces de Application o por **eventos de dominio**, nunca leyendo colecciones ajenas.
4. Toda configuración sale de variables de entorno; nada de secretos en `appsettings`.
5. `TreatWarningsAsErrors=true`: una advertencia rompe la compilación.
6. Todo miembro lleva comentario XML (en C#) o JSDoc (en TypeScript); código y comentarios en inglés, textos de interfaz en español.

## 4. Patrones centrales

### 4.1 Un agregado = un documento
Cada agregado se guarda en un solo documento de MongoDB y sus cambios son atómicos. La única excepción es la **agenda del fotógrafo** (`photographer_calendars`), un índice derivado que se actualiza junto con la reserva en una **transacción** (Atlas M0 las soporta).

### 4.2 Concurrencia optimista
Cada agregado tiene un campo `version`. Guardar es `ReplaceOne` filtrado por `Id` y `Version`; si nadie coincide, otro cambio ganó y se lanza `ConflictException` → HTTP 409 `concurrency.conflict`. Así, dos acciones simultáneas (por ejemplo un pago y una expiración) no se pisan: gana la primera.

### 4.3 Doble reserva imposible
Un fotógrafo no puede tener dos reservas activas que se traslapen. En vez de "consultar y luego insertar" (carrera), la agenda reserva el rango con un `UpdateOne` condicionado (`$not $elemMatch` de traslape + `$push`) y guarda la reserva en la misma transacción. Reprogramar suelta el rango anterior y reserva el nuevo en una sola operación.

### 4.4 Outbox transaccional
Cuando un agregado cambia, sus eventos de dominio (`BookingCreated`, `PaymentVerified`, `SelectionSubmitted`...) se guardan en `outbox_messages` **en la misma transacción** que el cambio. Nunca hay un cambio sin su evento ni un evento sin su cambio. Después, un procesador (`OutboxProcessor`) los entrega a los consumidores:
- toma el mensaje más antiguo con `FindOneAndUpdate` y un *lease* de 2 minutos (dos workers no entregan el mismo mensaje; si uno cae, otro lo releva);
- entrega **al menos una vez**, así que todo consumidor debe ser **idempotente**;
- ante un fallo reintenta con espera creciente (5 s … 15 min); a los 8 intentos queda `Failed` para revisión manual;
- los entregados se borran solos a los 30 días.

Para agregar un consumidor basta implementar `IDomainEventHandler<TEvento>` y registrarlo; no se toca el worker.

### 4.5 Tiempo y dinero
- La hora siempre viene de `TimeProvider` inyectado (pruebas deterministas). Todo en UTC.
- Los montos son `Money` (decimal + moneda, por defecto CRC) y se guardan como `Decimal128`.
- Los identificadores son `Guid.CreateVersion7`.

### 4.6 Errores
Las excepciones de dominio y de aplicación se convierten en `ProblemDetails` (RFC 9457) con un código estable en la extensión `code`. Los clientes se basan en el `code`, no en el texto. La tabla completa está en [A2](A2-Backend.md#errores).

## 5. Dos procesos, una imagen

La API y el worker salen de la **misma imagen Docker**; solo cambia el comando:
- API: `dotnet api/PhotoStudio.Api.dll` (por defecto).
- Worker: `dotnet worker/PhotoStudio.Worker.dll`.

El worker tiene dos modos:
- **Siempre encendido:** dos servicios en segundo plano (`OutboxDispatcherService` cada 5 s y `MaintenanceService` cada minuto).
- **Una pasada y termina** (`WORKER_RUN_ONCE=true`): lo que se usa hoy en Railway, programado como cron `*/15 * * * *`. Ahorra dinero: el servicio solo se cobra mientras corre.

Cada **pasada de mantenimiento** (`RunMaintenanceHandler`) hace, en este orden y en lotes acotados:
1. Expirar reservas tentativas vencidas (B7).
2. Entregar el outbox.
3. Entregar los avisos que ya vencieron a la bandeja y al push.
4. Avisar de galerías que pueden liberarse y archivar las que cumplieron 180 días.

Además, la **API ejecuta esa misma pasada** tras cada petición exitosa que cambie una reserva (`OutboxWakeUpService`, máximo una por segundo), para que los avisos lleguen en segundos y no esperen al cron. Es seguro con el worker a la vez gracias al *lease* y a la versión. Un operador de sistema también puede lanzarla a mano (`POST /api/maintenance/run`, 1 por minuto).

## 6. Flujo de una petición típica (crear reserva)

1. La app llama `POST /api/bookings` con el token del fotógrafo.
2. El middleware JWT valida la firma, el emisor, la audiencia y el algoritmo (solo HS256); el `PhotographerId` sale del claim `sub`.
3. El endpoint mapea el `*Request` a `CreateBookingCommand` y llama al manejador.
4. El manejador consulta la política vigente, crea el agregado `Booking` (reglas B1 del dominio) y lo guarda con `AddAsync`.
5. `AddAsync` abre una transacción: reserva el rango en la agenda, inserta la reserva y escribe `BookingCreated` en el outbox.
6. La API responde 201 con la reserva y sus `allowedActions`; el despertador lanza una pasada que entrega el evento.

## 7. Estructura de pruebas

| Proyecto | Qué prueba | Necesita |
|---|---|---|
| `Domain.UnitTests` | Transiciones, guardas, objetos de valor | nada |
| `Application.UnitTests` | Manejadores con puertos sustituidos | nada |
| `Infrastructure.IntegrationTests` | Repositorios, outbox, índices | MongoDB real |
| `Api.IntegrationTests` | La API completa en memoria: login, aislamiento entre fotógrafos, portal, galería con R2 real | MongoDB y almacenamiento S3 locales |

Las de integración se saltan solas si no hay Mongo / almacenamiento. Detalle en [A7](A7-Pruebas-y-operacion.md).
