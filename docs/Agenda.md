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

### Lo que deja la fase 2 (ya en el código)

Nada de esto se ve todavía en la app; es lo que las pantallas de las fases siguientes usan.

- **API:** `bookingsApi.getCalendar(range, includeEnded)` y los parsers `parseCalendar` / `buildCalendarPath` (`src/api/bookingsApi.ts`); el tipo es `CalendarResponse` (`src/types/api/calendar.ts`).
- **Datos:** `useAgendaRange(range, includeEnded)` (`src/hooks/useAgendaRange.ts`): TanStack Query, se refresca cada minuto y conserva lo anterior mientras carga el
  rango nuevo. `usePrefetchAgenda(view, anchor, includeEnded)` carga de antemano el período anterior y el siguiente. **La clave de caché cuelga de
  `['bookings', fotógrafo, 'agenda', …]`**, la misma de las reservas, así que todo lo que ya refresca las reservas (crear, editar, cancelar,
  reprogramar, completar, el mantenimiento) refresca la agenda sin tocar esos hooks.
- **Estado** (`src/agenda/`): `useAgendaStore` (vista, día ancla, "Mostrar todas") y `agendaManager` (restaura y guarda **solo la vista y el interruptor**; la
  agenda siempre abre en hoy). Por defecto abre en la vista **Agenda** y con el interruptor apagado.
- **Utilidades puras** (`src/utils/agenda/`), probadas en varias zonas horarias (UTC, Costa Rica, Nueva York, Londres, Auckland, con cambios de hora):
  `dates` (aritmética por **días civiles**, no por milisegundos), `ranges` (rango visible, mover el período, cuántos períodos hay entre dos días, rangos
  vecinos; nunca más de 62 días), `layout` (`layoutDay`: columnas lado a lado como Google, con altura mínima de 30 min y minutos **por la hora del reloj**,
  también en los días del cambio de hora), `monthGrid`, `schedule` (agrupar por día; hoy siempre aparece), `titles` y `preferences`.
- **Para las fases siguientes:** el paginador usa `shiftAnchor` y `periodsBetween` (índice de página relativo a un ancla, sin lista infinita real); el mes pide
  su cuadrícula completa (4 a 6 semanas, máx. 42 días) y la lista pide trozos de 30 días.

### Lo que deja la fase 3 (ya visible en la app)

- **La agenda es la pantalla de inicio** (`app/index.tsx`). La lista de antes pasó a `app/bookings/index.tsx`, con el título "Todas las reservas", y se abre desde el
  menú lateral (que ahora dice **Agenda** y **Todas las reservas**).
- **Cabecera tipo Google** (en la barra de siempre, con el menú ☰ a la izquierda): el **mes** de lo que se está mirando (en la vista Agenda sigue al día de
  arriba al deslizar; con otro año agrega el año), el botón **Ir a hoy** y el botón de **vistas** (⋮). Sustituye al logo y el nombre del estudio que tenía el
  inicio (`HeaderBrand` ya no se usa en la app; sigue en el menú lateral el logo con el nombre).
- **Menú de vistas** (`AgendaViewMenu`): Agenda, Día, 3 días, Semana y Mes, con la elegida marcada. Las que aún no tienen pantalla salen atenuadas y no se pueden
  elegir (`IMPLEMENTED_AGENDA_VIEWS` en `constants/agenda.ts`; cada fase agrega las suyas, y una vista guardada que aún no existe cae a la lista). Debajo, el
  interruptor **Mostrar todas las reservas** (apagado: solo tentativas y confirmadas), que recuerda su valor.
- **Vista Agenda** (`ScheduleView`): los días que tienen reservas y siempre hoy ("Sin reservas" si no hay), con **día de la semana y número** (hoy en un círculo del color
  principal), un **banner al cambiar de mes**, y cada reserva como un **bloque del color de su estado** con cliente, horas, paquete y el nombre del estado
  (la cancelada o vencida lleva el nombre tachado). Tocar un bloque abre la reserva. Abre en hoy **con los próximos 90 días ya cargados** (tres trozos de 30
  días, sin botones ni enlaces: la prueba en teléfono mostró que no gustaban); carga **30 días más** al acercarse a un extremo **solo después de deslizar
  a mano** (así no cargue sola una lista corta), con un indicador de carga en ese extremo; mantiene lo que se ve donde está al agregar días arriba; hasta 24 trozos de 30 días (unos dos años) a cada lado; deslizar hacia abajo recarga; avisa si el backend dejó reservas fuera.
- **Datos:** `useAgendaRanges` (varios rangos juntos con `useQueries`, sin repetir una reserva que cruza dos rangos) y `useToday` (el día de hoy, que solo cambia a
  la medianoche).

## 5. Fases

Un PR por fase, hacia `feat/agenda-calendar-main`. Cada uno pasa `tsc`, ESLint y todas las pruebas (y las del backend en la fase 1).

| # | Fase | Tamaño | Estado |
|---|---|---|---|
| 0 | Este plan | — | Hecho (#92) |
| 1 | **Backend:** `GET /api/bookings/calendar`, índice, pruebas y documentación | Pequeño | Hecho (#93) |
| 2 | **Base de la app:** tipos, API, hook de datos, estado y utilidades puras con sus pruebas (sin interfaz) | Mediano | Hecho (#94) |
| 3 | **Vista Agenda** + cabecera (mes, vistas, hoy) + ruta como inicio + "Todas las reservas" en el menú. Incluye el interruptor "Mostrar todas las reservas" | Mediano | En revisión (este PR) |
| 4 | **Día / 3 días / Semana:** cuadrícula, línea de "ahora", solapes lado a lado, días en la cabecera. Empieza con un *spike* | Grande | Pendiente |
| 5 | **Mes + mini-mes** para saltar de fecha | Mediano | Pendiente |
| 6 | **Crear desde un hueco** (el formulario acepta hora y duración iniciales), pulido y accesibilidad. El interruptor "Mostrar todas" ya llegó en la fase 3 | Mediano | Pendiente |
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

## 8. Datos de prueba

`backend/scripts/seed-agenda.mjs` llena una cuenta **local** con 35 reservas repartidas en unos tres meses para probar la agenda (la API local debe estar
corriendo; con Docker, Mongo también):

```
node backend/scripts/seed-agenda.mjs            # crea la cuenta "agenda" (contraseña Agenda-12345) y sus reservas
node backend/scripts/seed-agenda.mjs --reset    # borra las reservas de esa cuenta y las vuelve a crear
```

- Crea tentativas, confirmadas (contrato en papel y anticipo en efectivo) y canceladas, **a través de la API real**; marca el correo de la cuenta como verificado
  directamente en Mongo (en Development el código solo sale en el log de la API). Se niega a correr contra `photosensestudio.com`.
- Incluye los casos difíciles de ver a mano: hoy, un día con tres reservas, una sesión que cruza la medianoche, una de 4 horas y dos reservas canceladas cuyo horario
  tomó otra (se solapan en pantalla con "Mostrar todas las reservas").
- No hay completadas ni de cliente ausente (la API rechaza sesiones pasadas), y las tentativas vencen solas a las 48 horas: con `--reset` se recrean.
- Opciones: `--api-url`, `--username`, `--email`, `--password`, `--container`, `--database`, `--reset`.

