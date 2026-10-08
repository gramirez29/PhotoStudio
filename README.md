# PhotoStudio (PhotoStud.io)

Plataforma para fotógrafos: reservas, contratos, cobros y entrega de galerías.
El fotógrafo usa la app móvil; el cliente usa un enlace web por reserva.

## Estructura

```
PhotoStudio/
├── backend/                         .NET 10 · Clean Architecture · monolito modular
│   ├── PhotoStudio.slnx
│   ├── Directory.Build.props        Configuración común (net10.0, nullable, XML docs)
│   ├── Directory.Packages.props     Versiones NuGet centralizadas
│   ├── Dockerfile                   Imagen de producción para Railway
│   ├── src/
│   │   ├── PhotoStudio.Domain           Agregado Booking (máquina de estados), value objects, eventos
│   │   ├── PhotoStudio.Application      Casos de uso, puertos, DTOs de respuesta
│   │   ├── PhotoStudio.Infrastructure   MongoDB (documentos + mapeos), health check, políticas
│   │   └── PhotoStudio.Api              Minimal API, manejo de errores, composición
│   └── tests/
│       ├── PhotoStudio.Domain.UnitTests       xUnit v3 + Shouldly
│       └── PhotoStudio.Application.UnitTests  xUnit v3 + NSubstitute + Shouldly
└── mobile/                          Expo SDK 57 · Expo Router · TypeScript estricto
    └── src/
        ├── app/          Pantallas (Expo Router)
        ├── api/          Cliente HTTP tipado, validación de respuestas, tipos espejo del backend
        ├── hooks/        TanStack Query
        ├── state/        Zustand (solo estado de cliente)
        ├── components/   Componentes de UI
        ├── theme/        Tokens de diseño (paleta neutral temporal)
        └── utils/        Formato y etiquetas en español
```

## Backend

Requisitos: .NET SDK 10.0.1xx o superior, MongoDB local o Atlas.

```bash
cd backend
dotnet restore
dotnet build
dotnet test
dotnet run --project src/PhotoStudio.Api
```

Variables de entorno:

| Variable | Obligatoria | Descripción |
|---|---|---|
| `MONGODB_CONNECTION_STRING` | Sí | Cadena de conexión (Atlas en producción) |
| `MONGODB_DATABASE_NAME` | No | Por defecto `photostudio` |
| `PORT` | No | Puerto que asigna Railway; por defecto 8080 en el contenedor |
| `BOOKING_*` | No | Políticas de reserva (ver `EnvironmentBookingPolicyProvider`) |

En desarrollo, `Properties/launchSettings.json` apunta a `mongodb://localhost:27017`.

Endpoints:

| Método | Ruta | Descripción |
|---|---|---|
| POST | `/api/bookings` | Crea una reserva tentativa (B1) |
| GET | `/api/bookings/{id}` | Lee una reserva con sus acciones permitidas |
| POST | `/api/bookings/{id}/contract/in-person` | Registra contrato firmado en persona o en papel (B2) |
| POST | `/api/bookings/{id}/payments/in-person` | Registra pago presencial (B5) |
| GET | `/health/live` · `/health/ready` | Liveness y readiness (MongoDB) |
| GET | `/openapi/v1.json` | Documento OpenAPI |

### Railway

Servicio con *Root Directory* = `backend` y el `Dockerfile` de esa carpeta. Health check: `/health/ready`.

## Mobile

Requisitos: Node 20+.

```bash
cd mobile
npm install
cp .env.example .env      # ajustar EXPO_PUBLIC_API_URL
npm run verify            # typecheck + lint (incluye prohibición de any) + tests
npx expo start --clear
```

En un dispositivo físico, `EXPO_PUBLIC_API_URL` debe usar la IP LAN de tu computadora, no `localhost`.

## Reglas del proyecto

- Toda clase, método, función e interfaz lleva comentario de documentación (XML docs en C#, JSDoc en TypeScript).
- TypeScript sin `any`: la regla `@typescript-eslint/no-explicit-any` está en `error`.
- Las respuestas de la API se validan en tiempo de ejecución (`src/api/guards.ts`) antes de tiparse.
- La lógica de estados vive solo en el dominio; los clientes muestran las `allowedActions` que devuelve la API.
