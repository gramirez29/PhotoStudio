# Billing: reembolsos y retenciones

Qué pasa con el dinero cuando una reserva termina sin entregar la sesión (cancelada, vencida o con el cliente ausente). El fotógrafo ve cuánto debe devolver, a quién, y lo marca como devuelto cuando hace la transferencia o entrega el efectivo.

**Alcance de esta fase:** reembolsos y retenciones. Los **cargos extra** (fotos adicionales en la selección) dependen de Gallery y quedan para cuando exista. La **factura electrónica** (Invoicing) tampoco está aquí.

## 1. Reglas del negocio

Salen de `CLAUDE.md` §8.11 y §8.13 (defaults de la política). Las calcula `SettlementCalculator` (Domain) **desde el estado actual de la reserva**.

| Cómo terminó la reserva | Se retiene | Se devuelve |
|---|---|---|
| Cancela el **fotógrafo** (en cualquier estado) | Nada | Todo lo pagado |
| Cancela el cliente una reserva **tentativa** | Nada | Todo lo pagado |
| Cancela el cliente una reserva **confirmada** con aviso ≥ `FreeCancellationWindowHours` (72 h) | Nada | Todo lo pagado |
| Cancela el cliente una reserva **confirmada** con menos aviso | El anticipo (100 %) | Lo pagado por encima del anticipo |
| La reserva **vence** (`Expired`) con dinero pagado | Nada | Todo lo pagado |
| El cliente **no se presenta** (`ClientAbsent`) | El anticipo (100 %) | Lo pagado por encima del anticipo |
| Se **revierte** la ausencia (vuelve a `Confirmed`) | Se anula la retención | Un reembolso aún pendiente se anula |

- "Anticipo" = `PackagePrice × DepositPercentage` de la política congelada en la reserva; nunca se retiene más de lo que se pagó.
- Los porcentajes de retención (100 %) son constantes de `SettlementCalculator` (`LateCancellationRetention`, `ClientAbsentRetention`); a futuro serán configurables por fotógrafo.
- Una reserva que terminó **sin dinero pagado** no genera liquidación.
- `Completed` no se liquida: cancelar después de completar no existe (disputas = reembolso manual, decisión abierta §8.13-5).

## 2. Diseño

- **Agregado `BookingSettlement`** (`Domain/Billing`): una liquidación por reserva (índice único por reserva). Guarda la razón, lo pagado, lo retenido (`RetentionStatus`: None/Applied/Reversed), el reembolso (`RefundStatus`: None/Pending/Completed/Voided), una captura del cliente y la sesión (para listar sin leer la reserva) y, al completarse, método, nota y fecha.
- **Un pago nunca se edita ni se borra**: devolver dinero se registra aquí como reembolso (regla de `CLAUDE.md` §8.10).
- **Reconcilia desde el estado, no desde el evento** (`SettlementPlanner`): igual que Notifications. Los eventos `BookingCancelled`, `BookingExpired`, `ClientMarkedAbsent` y `ClientAbsenceReverted` solo piden reconciliar; da el mismo resultado sin importar el orden ni cuántas veces llegue cada uno (el outbox entrega *al menos una vez*).
- **Concurrencia:** si dos eventos crean la liquidación a la vez, el índice único hace perder a uno, que la lee y le aplica el resultado (hasta 3 intentos; después el outbox reintenta). Escrituras con versión (409 `concurrency.conflict`).
- **Un reembolso completado es final** (`Apply` y `Void` no hacen nada): esa transferencia ya ocurrió. Caso raro: si ya se devolvió y luego se revierte la ausencia, queda para resolver a mano.
- **Completar es una vez:** el segundo intento (doble toque) recibe 409 `booking.invalid_transition`.
- El módulo lee reservas por `IBookingRepository` (puerto de Application) y se alimenta de eventos del outbox; no lee colecciones de otro módulo.

## 3. API

Todas exigen sesión y se acotan al fotógrafo del token (la liquidación de otro responde 404).

| Método | Ruta | Descripción |
|---|---|---|
| GET | `/api/billing/refunds` | Reembolsos pendientes (hasta 100, el más antiguo primero) y `pendingCount` |
| GET | `/api/billing/settlements/{bookingId}` | Liquidación de una reserva; 404 si no tiene (no terminó, o terminó sin pagos) |
| POST | `/api/billing/settlements/{bookingId}/refund/complete` | Marca el reembolso como devuelto: `{ "method": "Cash" \| "SinpeMovil", "note": "..." }` (nota opcional, máx. 200). 422 `payment.invalid_method` con otro método; 409 `booking.invalid_transition` si no está pendiente |

Una liquidación aparece cuando el outbox entrega el evento: con el worker siempre encendido en segundos; con cron `*/15` hasta 15 minutos; o al instante con el botón "Liberar reservas vencidas" de la app (que ejecuta una pasada de mantenimiento).

## 4. Colección `settlements`

Índices: `ix_settlement_booking` (único), `ix_settlement_refund` `{photographerId, refundStatus, createdAt}`. No lleva TTL: es un registro financiero.

## 5. App móvil

- **Inicio:** botón rojo "Reembolsos pendientes (n)" (solo aparece si hay alguno).
- **Pantalla Reembolsos** (`app/refunds.tsx`): cada reembolso con cliente, monto, motivo y lo que te quedas; acciones **Marcar como devuelto**, **Escribir por WhatsApp** (mensaje para preguntar el número de SINPE) y **Ver reserva**.
- **Marcar como devuelto** (`app/bookings/refund.tsx`): método (SINPE Móvil por defecto o efectivo) y nota opcional. Es definitivo.
- **Detalle de la reserva:** tarjeta "Dinero de esta reserva" (`SettlementCard`) en reservas canceladas, vencidas o con cliente ausente que tengan liquidación.
- La lista se recarga cada 60 s con la app abierta.

## 6. Dónde vive

| Capa | Archivos |
|---|---|
| Domain | `Domain/Billing/BookingSettlement.cs`, `SettlementCalculator.cs`, `SettlementEnums.cs` |
| Application | `Billing/SettlementPlanner.cs`, `BookingSettlementHandler.cs`, `SettlementResponses.cs`, `GetSettlement/`, `ListRefunds/`, `CompleteRefund/`; puerto `Abstractions/ISettlementRepository.cs` |
| Infrastructure | `Persistence/MongoSettlementRepository.cs`, `Persistence/Documents/SettlementDocument.cs`, índices en `MongoIndexInitializer.cs` |
| Api | `Endpoints/BillingEndpoints.cs` |
| Mobile | `api/billingApi.ts`, `hooks/useRefunds.ts`, `useSettlement.ts`, `useCompleteRefund.ts`, `useRefundForm.ts`, `useRefundActions.ts`, `forms/refundForm.ts`, `utils/billing.ts`, `components/RefundsButton.tsx`, `RefundListItem.tsx`, `RefundForm.tsx`, `SettlementCard.tsx`, `app/refunds.tsx`, `app/bookings/refund.tsx` |

## 7. Pruebas

- Domain: `SettlementCalculatorTests` (cada fila de la tabla), `BookingSettlementTests`.
- Application: `SettlementPlannerTests` (abrir, sin pagos, idempotencia, revertir, carrera entre eventos), `BillingHandlerTests`.
- Infrastructure (Mongo real): `MongoSettlementRepositoryTests` (round trip, duplicado, lista por fotógrafo, concurrencia, índices).
- Api (punta a punta): `BillingApiTests` (401, reserva confirmada cancelada → reembolso de 50 000 → aislamiento → completar una vez; reserva sin pagos → sin liquidación).
- Mobile: `billingApi.test.ts`, `billing.test.ts`, `refundForm.test.ts`, `useRefunds.test.tsx`, `BillingComponents.test.tsx`.

## 8. Límites conocidos

- Sin cargos extra (esperan a Gallery) ni factura electrónica.
- La app registra que devolviste el dinero; **no mueve dinero**. La transferencia la haces tú por SINPE o en efectivo.
- Las cancelaciones del cliente no se pueden hacer todavía desde la app (llegarán con el portal), así que hoy los casos de retención llegan por **cliente ausente**; la retención por cancelación tardía está implementada y probada en el dominio.
- No hay aviso en la bandeja cuando aparece un reembolso; el botón rojo del inicio cumple ese papel. Un `NotificationType.RefundDue` sería el siguiente paso natural.
- Porcentajes de retención fijos (100 %); un reembolso ya completado no se corrige si luego se revierte la ausencia.
