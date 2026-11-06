# Agenda de la app móvil (estilo Google Calendar)

Plan y estado de la vista de agenda de la app del fotógrafo: un calendario con las vistas **Agenda, Día, 3 días, Semana y Mes**, muy parecido al
de Google. Se trabaja aparte del resto: una rama de integración **`feat/agenda-calendar-main`** (sale de `develop`) y **un PR por fase hacia
ella**. Al final se prueba todo el conjunto y un solo PR lleva la rama a `develop`.

## 1. Decisiones (2026-11-06)

| Tema | Decisión |
|---|---|
| Dónde vive | La agenda es la **pantalla de inicio**. La lista actual pasa a **"Todas las reservas"** en el menú lateral y conserva sus filtros. |
| Inicio de la semana | **Domingo**. |
| Qué reservas se ven | Por defecto, **solo las tentativas y las confirmadas**. Un **interruptor** ("Mostrar todas") agrega las completadas, las de cliente ausente, las canceladas y las vencidas. |
| Vistas | Las cinco: **Agenda, Día, 3 días, Semana y Mes**, entregadas por fases (cada PR ya es útil). |
| Solapes | **Lado a lado**, como Google (algoritmo de columnas). Pueden aparecer con "Mostrar todas": una completada no ocupa el horario y otra reserva puede estar encima. |
| Arrastrar para reprogramar | **Fuera de esta entrega, anotado** (ver §6). |
| Librerías nuevas | **Ninguna**. Todo con `FlatList`, `ScrollView`, `Pressable` y `react-native-reanimated`, que ya están. No hace falta un build nuevo de EAS. |
| Zona horaria | La del teléfono, como el resto de la app. |

## 2. Qué se copia de Google Calendar

| Google | Aquí |
|---|---|
| Barra superior con el mes, menú de vistas y botón "hoy" | Igual. Tocar el título abre un **mini-mes** para saltar de fecha. |
| Vista **Agenda** (lista por día, sin fin) | Vista "Agenda": días con reservas, encabezado de mes, hoy siempre visible. |
| **Día / 3 días / Semana**: cuadrícula de horas, línea roja de la hora actual | Igual, con bloques de color según el estado de la reserva. |
| **Mes**: cuadrícula con chips y "+N" | Igual. Tocar un día abre su vista de día. |
| Deslizar de lado para cambiar de período | Igual (en Mes, vertical). |
| Tocar un hueco vacío para crear | Aparece un bloque fantasma "+ Nueva reserva" a esa hora; al tocarlo se abre el formulario con la hora puesta. |
| Botón "+" flotante | Ya existe (`NewBookingFab`). |

Queda fuera, porque no aplica al producto: varios calendarios, invitados, recordatorios propios, búsqueda y widgets. La fidelidad al píxel no es el
objetivo; sí la estructura, la navegación y los gestos.

## 3. Backend

`GET /api/bookings/calendar?from=…&to=…&includeEnded=false` (fase 1). Devuelve `{ items, truncated }`, sin paginar, ordenado por inicio, con los
mismos `BookingSummaryResponse` de la lista.

- Filtra por **solape** con el rango: `inicio < to` y `fin > from`. Solo reservas del fotógrafo del token.
- Rango máximo de **62 días**; si es mayor, 400 `request.invalid`. Tope de **500** elementos; si se pasa, `truncated: true`.
- Por defecto solo `Tentative` y `Confirmed`; con `includeEnded=true`, todos los estados.
- Sin límite superior de duración de una sesión en el dominio, así que la consulta no supone ninguno. Se agrega el índice
  `(photographerId, slotEnd)` junto al existente `(photographerId, slotStart)` para que Mongo escoja el que le sirva según el rango.

## 4. App

Reglas del repo: tipos en `src/types/` en espejo, funciones puras en `src/utils/`, un componente por archivo, colores solo del tema, textos en
español, comentarios en inglés.

- **Utilidades puras** (`utils/agenda/`): rango visible por vista, semana y mes, agrupar por día y el layout de solapes. Son lo más probado.
- **Datos:** `useAgendaRange(from, to)` con TanStack Query, que precarga el período anterior y el siguiente. Una clave `['agenda']` se invalida
  desde los hooks de reservas (crear, editar, cancelar, reprogramar, completar...), para que no quede nada desactualizado.
- **Estado de UI** (Zustand, recordado entre sesiones como hace Google): vista elegida, fecha ancla e interruptor "Mostrar todas".
- **Componentes:** `AgendaHeader`, `MiniMonth`, `ScheduleView`, `TimeGridView` (`HourColumn`, `DayColumn`, `EventBlock`, `NowIndicator`) y `MonthView`.
- **Paginación sin librería:** `FlatList` con `pagingEnabled` y `getItemLayout` sobre un índice virtual de períodos desde una época fija; es
  "infinita" sin recargar listas.
- **Accesibilidad:** cada bloque se lee, por ejemplo, "Reserva de María Pérez, Retrato Familiar, de 3:00 p. m. a 5:00 p. m., confirmada".

## 5. Fases

Un PR por fase, hacia `feat/agenda-calendar-main`. Cada uno pasa `tsc`, ESLint y todas las pruebas (y las del backend en la fase 1).

| # | Fase | Tamaño | Estado |
|---|---|---|---|
| 0 | Este plan | — | En revisión |
| 1 | **Backend:** `GET /api/bookings/calendar`, índice, pruebas y documentación | Pequeño | Pendiente |
| 2 | **Base de la app:** tipos, API, hook de datos, estado y utilidades puras con sus pruebas (sin interfaz) | Mediano | Pendiente |
| 3 | **Vista Agenda** + cabecera (mes, vistas, hoy) + ruta como inicio + "Todas las reservas" en el menú | Mediano | Pendiente |
| 4 | **Día / 3 días / Semana:** cuadrícula, línea de "ahora", solapes lado a lado, días en la cabecera. Empieza con un *spike* | Grande | Pendiente |
| 5 | **Mes + mini-mes** para saltar de fecha | Mediano | Pendiente |
| 6 | **Crear desde un hueco** (el formulario acepta hora y duración iniciales), interruptor "Mostrar todas", pulido y accesibilidad | Mediano | Pendiente |
| — | Prueba de todo el conjunto y PR de la rama a `develop` | — | Pendiente |

## 6. Anotado para después

- **Arrastrar una reserva para reprogramarla** (como en Google). Necesita `react-native-gesture-handler` (un build nuevo de EAS) y solo aplica a
  las confirmadas, que son las que hoy se pueden reprogramar. Hay que decidir qué pasa si el nuevo horario choca con otra reserva (el backend
  responde 409 `booking.slot_unavailable`).
- Cambiar el inicio de la semana a lunes desde Ajustes, si alguna vez hace falta.

## 7. Riesgos

- **Cuadrícula de horas:** requiere un scroll vertical compartido entre páginas (horas fijas a la izquierda, días fijos arriba, deslizar de
  lado). Se resuelve primero con un *spike* en la fase 4; si no queda fluido en Android, plan B: una columna de horas por página y el scroll
  vertical sincronizado con Reanimated.
- **Rendimiento:** muchos bloques posicionados de forma absoluta. Un fotógrafo tiene pocas reservas por día, así que no se espera problema; se
  mide con 200 reservas en un teléfono real antes de cerrar la fase 4.
- **Zona horaria:** las reservas viajan en UTC y se pintan en la hora del teléfono. Costa Rica no tiene horario de verano; si el teléfono cambia de
  zona, la agenda cambia con él (igual que Google).
