# Pantalla Home de la app móvil

La app abre en **Home** (`mobile/src/app/index.tsx`), la primera pantalla de la pila. Todo lo demás se abre encima y vuelve a ella con el botón «Atrás».

## 1. Qué muestra

- **Tus citas:** una dona (Skia) con las citas por estado, con el total en el centro y una leyenda (color, nombre del estado, cantidad y porcentaje). El periodo se elige con un selector: **Este mes** (por defecto), **Mes pasado** y **Todo**. Una cita pertenece al periodo por el día en que **empieza su sesión**, en la hora del teléfono.
- **Pendientes:** cada fila aparece solo si tiene algo, con su cantidad, y abre donde se atiende; si no hay nada lo dice.
  - **Avisos sin leer** → Avisos.
  - **Comprobantes por verificar** (reservas con un comprobante SINPE sin verificar ni rechazar) → Avisos, donde cada comprobante tiene su aviso con «Revisar comprobante».
  - **Tentativas por vencer en 24 h** (tentativas cuyo apartado termina en las próximas 24 horas y que pueden vencer de verdad: una con comprobante por revisar no vence) → Todas las reservas, ya filtradas por «Tentativa».
  - **Reembolsos pendientes** → Reembolsos pendientes.
  - **Galerías por archivar** (entregadas, con espacio por liberar) → Galerías por archivar.
- **Ir a:** Agenda, Todas las reservas, Nueva reserva y, si la app conoce su dirección, **Abrir la web del fotógrafo** (`/studio`, en el navegador del teléfono).

## 2. Navegación

- Home es la única raíz. Agenda, Todas las reservas, Avisos, Reembolsos, Galerías por archivar y Ajustes se abren encima (`router.push`) y llevan su «Atrás». Home lleva el botón del menú lateral.
- El menú lateral tiene **Inicio**, **Agenda** y **Todas las reservas**; marca la que está abierta (`useCurrentMainScreen`, por la ruta) y, si ya estás en ella, solo se cierra. **Inicio** cierra todo lo apilado (`openHome`, `router.dismissAll`).
- **Salir de la app:** en Android, el gesto o botón de atrás en Home muestra «Presiona de nuevo para salir» y solo un segundo toque, dentro de 2 segundos, sale (`useExitOnDoubleBack`, `BackHandler`). En iOS no hay gesto de atrás propio.
- Se quitó el ajuste «Pantalla principal» de Ajustes (ya no hace falta). Quedó guardada en el teléfono la clave `photostudio.homeScreen` de quien lo probó; no se lee.

## 3. Backend

`GET /api/bookings/stats?from=&to=` (PR #101): `{ total, byStatus: [{ status, count }] }` con **los seis estados siempre**, cero incluido, en el orden de `BookingStatus`. `from` y `to` son opcionales (sin ellos cuenta todo; `to` exclusivo); un periodo vacío o invertido responde 400. Cuenta con `$match` + `$group` en MongoDB, solo del fotógrafo del token.

`GET /api/bookings/attention`: `{ pendingProofs, expiringSoon }` (reservas con comprobante por revisar y tentativas que vencen en 24 horas, `AttentionLimits.ExpiringSoonHours`). Los dos conteos se hacen en MongoDB y solo del fotógrafo del token.

## 4. App

- Datos: `api/bookingsApi.ts` (`getStats`, `parseBookingStats`, `buildStatsPath`), `hooks/useBookingStats.ts` (clave `['bookings', fotógrafo, 'stats', from, to]`: todo lo que refresca las reservas refresca la dona; se actualiza sola cada minuto).
- Utilidades puras: `utils/home/donut.ts` (`donutSegments`: ángulos desde arriba, en el sentido del reloj), `utils/home/periods.ts` (`periodRange`).
- Componentes: `BookingStatsCard`, `StatusDonut` (Skia), `StatusLegend`, `HomePeriodSelector`, `HomePendingCard`, `HomeShortcutsCard`, `HomeLinkRow`.
- Configuración: `EXPO_PUBLIC_STUDIO_URL` (opcional; sin ella no aparece el enlace a la web). En develop: `https://develop.photosensestudio.com/studio`.
- Pruebas: Skia no corre en Jest; `mobile/__mocks__/@shopify/react-native-skia.tsx` lo sustituye (el lienzo es una vista vacía) y la dona se comprueba por su descripción para el lector de pantalla y por la leyenda. **El dibujo real hay que mirarlo en el teléfono.**
