# B3 — Reglas de negocio

Este documento junta las reglas que gobiernan el producto. Las del dominio están implementadas en `backend/src/PhotoStudio.Domain` y cada una tiene pruebas.

## 1. Principios

1. **Dos máquinas separadas:** `Booking` (¿la sesión está apartada y comprometida?) y `Gallery` (¿en qué punto está la entrega?). La galería nace al completar la sesión.
2. **Un solo `Status` por agregado;** nunca banderas sueltas como `isPaid`.
3. **Contrato firmado y anticipo verificado no son estados**, son condiciones para confirmar. Así el flujo remoto y el presencial llegan al mismo estado.
4. **El dinero no es un estado:** es un saldo calculado.
5. **Las políticas se congelan** en la reserva al crearla: cambiar la política después nunca afecta reservas existentes.
6. **Los estados finales son inmutables.** Una corrección es una transición explícita, con motivo y registro (ejemplo: revertir ausencia).
7. **Las transiciones por tiempo** reevalúan sus condiciones al ejecutarse y son idempotentes. Si dos acciones chocan, gana la primera.
8. **Cualquier transición no listada se rechaza** (409 `booking.invalid_transition`).
9. **Los clientes no deciden reglas**: la API entrega las acciones permitidas.
10. Alcance actual: **una reserva = una sesión.** Varias sesiones por reserva (una boda en varios días) es futuro.

## 2. La reserva

### Estados
| Estado | Significado |
|---|---|
| **Tentativa** | Creada por el fotógrafo; horario apartado hasta su vencimiento |
| **Confirmada** | Contrato firmado y anticipo verificado |
| **Completada** | La sesión ocurrió (aún admite pagos del saldo) y continúa la galería |
| **Cancelada** | Cancelada por el cliente o el fotógrafo |
| **Vencida** | Venció el apartado sin confirmar |
| **Cliente ausente** | No se presentó (revertible dentro de una ventana) |

### Transiciones
| Id | Acción | De → a | Quién | Condiciones y efectos |
|---|---|---|---|---|
| B1 | Crear | — → Tentativa | Fotógrafo | Sesión en el futuro; sin traslape con otra activa. Vence en el menor entre 48 h y el inicio de la sesión. Copia la política y el precio |
| B2 | Firmar contrato | Tentativa | Cliente (portal) o fotógrafo (presencial o papel) | Una sola vez; intenta confirmar |
| B3 | Subir comprobante | Tentativa/Confirmada/Completada | Cliente | SINPE; monto > 0; clave de idempotencia (repetida = el mismo pago) |
| B4 | Verificar / rechazar | igual | Fotógrafo | Pago pendiente; rechazar exige motivo |
| B5 | Pago en persona | igual | Fotógrafo | Efectivo o SINPE; entra verificado; clave de idempotencia |
| B6 | Confirmar | Tentativa → Confirmada | Sistema | Contrato firmado **y** pagos verificados ≥ anticipo |
| B7 | Vencer | Tentativa → Vencida | Sistema | Ya pasó el vencimiento **y** no hay comprobante pendiente; libera el horario |
| B8 | Cancelar | Tentativa → Cancelada | Cliente o fotógrafo | Motivo opcional; libera; sin cargo |
| B9 | Reprogramar | Confirmada | Cliente (con límite) o fotógrafo (sin límite) | Nueva fecha futura. Cliente: máximo 1 reprogramación y 48 h de aviso |
| B10 | Cancelar | Confirmada → Cancelada | Cliente o fotógrafo | **Motivo obligatorio**; evento con quién canceló y con cuánta anticipación |
| B11 | Completar | Confirmada → Completada | Fotógrafo | Desde el inicio de la sesión; crea la galería |
| B12 | Cliente ausente | Confirmada → Ausente | Fotógrafo | Pasada la tolerancia (30 min); libera el horario |
| B13 | Revertir ausencia | Ausente → Confirmada | Fotógrafo | Motivo obligatorio; dentro de 7 días; retoma el horario |

Decisiones clave: una reserva **no vence con un comprobante pendiente** (si luego se rechaza, vence en la siguiente pasada); nadie llama a "confirmar" directamente; reprogramar no es un estado.

### Acciones permitidas por estado (para el fotógrafo)
- **Tentativa:** firmar contrato, registrar pago, verificar comprobante (si hay), editar datos, editar paquete (solo sin contrato firmado ni pagos), cancelar.
- **Confirmada:** registrar pago si hay saldo, reprogramar, cancelar, completar (desde el inicio), cliente ausente (tras la tolerancia), editar datos.
- **Completada:** solo acciones de pago (si hay saldo) y editar datos del cliente.
- **Cliente ausente:** revertir (dentro de la ventana).
- **Cancelada / Vencida:** ninguna.

## 3. Dinero

### El precio
`Total = precio del paquete + Σ(extras × cantidad) − descuento`. El descuento no puede superar paquete + extras. De ese total salen el anticipo, el saldo, el contrato y el portal. Cambiar el precio de un paquete en la app nunca cambia reservas existentes.

### Anticipo y saldo
- `Anticipo = Total × porcentaje` (50 % por defecto; mínimo fijo pendiente).
- `Saldo = Total − Σ pagos verificados`. Un saldo negativo indica sobrepago.
- Un pago **nunca se edita ni se borra**; se compensa con un reembolso.

### Métodos y estados de pago
Métodos: SINPE Móvil, efectivo, tarjeta (la tarjeta aún no está implementada). Estados: pendiente de verificar, verificado, rechazado (se conserva como evidencia).

### Cancelaciones, ausencia y reembolsos
Cuando una reserva con dinero pagado termina Cancelada, Vencida o Ausente, se crea una **liquidación** con lo retenido y lo que se devuelve:

| Caso | Por defecto |
|---|---|
| Cancela el cliente con aviso ≥ 72 h | Se devuelve todo |
| Cancela el cliente con menos de 72 h | Se retiene el anticipo |
| Cliente ausente | Se retiene el anticipo |
| Cancela el fotógrafo | Se devuelve todo lo pagado |
| Vence sin confirmar | No hay dinero verificado que devolver |
| Revertir una ausencia | Se anula la retención |

El fotógrafo marca el reembolso como devuelto (efectivo o SINPE, con nota); es definitivo.

### Fotos extra
Las fotos que el cliente elige por encima del paquete **no se cargan solas al saldo**: se acuerdan y se cobran aparte, y el fotógrafo las "entrega" a mano cuando están pagadas. Cada foto extra tiene el precio de la galería (₡3 000 por defecto).

## 4. Políticas (congeladas en cada reserva)

| Política | Valor | Variable |
|---|---|---|
| Horas de apartado (tentativa) | 48 | `BOOKING_TENTATIVE_HOLD_HOURS` |
| Porcentaje de anticipo | 0,5 | `BOOKING_DEPOSIT_PERCENTAGE` |
| Ventana de cancelación sin cargo | 72 h | `BOOKING_FREE_CANCELLATION_WINDOW_HOURS` |
| Aviso mínimo para reprogramar (cliente) | 48 h | `BOOKING_RESCHEDULE_MIN_NOTICE_HOURS` |
| Reprogramaciones del cliente | 1 | `BOOKING_MAX_RESCHEDULES` |
| Tolerancia para marcar ausente | 30 min | `BOOKING_CLIENT_ABSENT_TOLERANCE_MINUTES` |
| Ventana para revertir ausencia | 7 días | `BOOKING_CLIENT_ABSENT_REVERT_WINDOW_DAYS` |
| Retención por cancelación tardía | 100 % del anticipo | `BOOKING_LATE_CANCELLATION_RETENTION` (0 a 1) |
| Retención por cliente ausente | 100 % del anticipo | `BOOKING_CLIENT_ABSENT_RETENTION` (0 a 1) |
| Reembolso si cancela el fotógrafo | 100 % de lo pagado | `BOOKING_PHOTOGRAPHER_CANCELLATION_REFUND` (0 a 1) |
| Plazo para entregar las fotos finales | 21 días desde que el cliente envía su selección | `BOOKING_DELIVERY_DEADLINE_DAYS` (1 a 365) |

Hoy la política sale de variables de entorno; a futuro será configurable por fotógrafo. **Se congela en cada reserva al crearla**: cambiarla después no afecta a las reservas existentes, y el contrato que firma el cliente dice lo mismo que se aplicará (cláusulas 5 y 6).

## 5. La galería

### Estados
`Pendiente → En selección → En edición → Lista → Entregada → Archivada`.

| Id | Acción | De → a | Quién |
|---|---|---|---|
| G1 | Crear | — → Pendiente | Sistema, al completar la sesión |
| G2 | Publicar pruebas | Pendiente → En selección | Fotógrafo (necesita ≥ 1 prueba) |
| G3 | Enviar selección | En selección → En edición | Cliente (necesita ≥ 1 foto; las extras las cuenta el servidor) |
| G5 | Reabrir selección | En edición → En selección | Fotógrafo, con motivo |
| G6 | Publicar finales | En edición → Lista | Fotógrafo (final de **cada** foto del paquete) |
| G7 | Entregar | Lista → Entregada | **Sistema**, con saldo cero |
| G8 | Archivar | Entregada → Archivada | Fotógrafo (a mano) o sistema (a los 180 días) |

### Reglas
- **Fotos incluidas** según el paquete: Básico 15, Estándar 30, Premium 50; Custom, Otro y reservas sin paquete, 0 (el fotógrafo las fija). Máximo **150** entre incluidas y de regalo.
- **Las pruebas se congelan al publicar**: no se pueden subir ni quitar.
- **La selección se bloquea al enviar**; solo el fotógrafo la reabre.
- **Entrega con saldo cero:** se reevalúa al publicar y con cada pago verificado, aunque pasen semanas.
- **Descarga:** la API solo emite direcciones con la galería entregada; las de las extras, solo si fueron liberadas; **todo o nada** (si una foto no es descargable, falla toda la petición y no cuenta nada). Cada petición cuenta como una descarga.
- **Los finales del paquete se congelan al publicar**; los de las extras se pueden seguir subiendo.
- **Un final de otro tamaño reemplaza al anterior** y reinicia sus descargas.
- **ZIP:** uno por galería, hasta 500 MB, se sube con los finales publicados; subir otro de distinto tamaño lo reemplaza y reinicia su contador.
- **Archivo (G8):** borra pruebas, miniaturas, finales y ZIP de R2 y deja la galería sin acciones. **Irreversible.** Aviso único a los 90 días; automático a los 180. Si el almacenamiento falla, la galería sigue entregada y se reintenta.

### Seguridad de la entrega (cómo se evita llevarse fotos sin pagar)
La selección se bloquea al enviar; el fotógrafo solo edita lo elegido; no se emite ninguna dirección de descarga con saldo distinto de cero; las direcciones duran minutos; las pruebas son de baja calidad con marca grande al centro y el original nunca se sube. Una captura de pantalla no se puede impedir en una web.

## 6. Avisos al fotógrafo

| Aviso | Cuándo | Se descarta si |
|---|---|---|
| Recordatorio de sesión | 24 h antes de una sesión confirmada | La reserva se cancela, mueve o ya pasó |
| Saldo pendiente | 24 h después de completar | El saldo ya es cero |
| Comprobante por verificar | De inmediato al subirlo el cliente | — |
| Selección recibida | De inmediato al enviarla el cliente | El fotógrafo reabrió la selección |
| Entrega próxima | 3 días antes de que venza el plazo de entrega (21 días desde que el cliente envió su selección; si el plazo es más corto, de inmediato) | Ya se publicaron las fotos finales, o se reabrió la selección (el plazo se reinicia con la nueva) |
| Galería lista para liberar | A los 90 días de entregada, **una vez** | Ya fue liberada |

Cada aviso llega a la bandeja y como push. El texto lo redacta la app; el push tiene texto fijo en el backend.

## 7. Correos del sistema

Todos comparten la misma apariencia (banda azul pizarra con el logo, tarjeta durazno, versión de texto) y llevan el logo incrustado. En develop el logo puede no verse en algunos clientes de correo porque el sitio está tras Cloudflare Access.

| Correo | Para | Contenido |
|---|---|---|
| Código de verificación de la cuenta | Fotógrafo | 6 dígitos |
| Código de acceso a la web | Fotógrafo | 6 dígitos, atado al navegador |
| Código del portal | Cliente | 6 dígitos, solo al correo registrado |
| Copia del contrato | Cliente | El texto exacto firmado |
| Pruebas listas | Cliente | Pasos y precio de extras, **sin enlace** |
| Selección reabierta | Cliente | Motivo y cómo volver a elegir, sin enlace |
| Tus fotos ya están listas | Cliente | Aviso de entrega, sin enlace |

Los correos de la galería no llevan enlace a propósito: el enlace llega por WhatsApp y los códigos por correo, para que quien lea un solo buzón no tenga todo.

## 8. Escenarios de validación

La máquina está completa si estos casos se cubren sin excepciones especiales:
1. Reserva remota normal hasta galería entregada con saldo cero.
2. El cliente nunca paga → Vencida y horario liberado.
3. Comprobante subido poco antes de vencer → no vence; si se rechaza, vence en la siguiente pasada.
4. Todo presencial en efectivo → Confirmada de inmediato.
5. Cancelación tardía del cliente → retención según la política congelada.
6. El fotógrafo cancela → reembolso total.
7. Cliente marcado ausente por error → se revierte con motivo y se anula la retención.
8. Saldo pagado semanas después → la galería se entrega sola al verificar.
9. Extras en la selección → no se entregan hasta que el fotógrafo las libere.
10. Evento sin selección → (diseño) salta la selección.
11. Cambio de política después de confirmar → aplica la congelada.
12. Carrera pago contra vencimiento → gana el primero (versión).
13. Reprogramación excedida → rechazada al cliente; el fotógrafo sí puede.

## 9. Decisiones abiertas o futuras

Reprogramación por el cliente desde el portal; cobro con tarjeta; factura electrónica; mínimo fijo del anticipo; políticas configurables por fotógrafo; plazos de archivo configurables; varias sesiones por reserva; recordatorios movidos al reprogramar.
