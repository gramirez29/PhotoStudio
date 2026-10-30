# UI de la app móvil: Material Design 3, temas claro y oscuro, fondo y tipografía de la marca

Plan y estado del rediseño de la interfaz de la app del fotógrafo. La regla de oro: **no romper nada de lo que ya funciona**. Los
cambios van por dentro de cada componente; sus props, sus etiquetas de accesibilidad, los hooks, `forms/`, `api/` y las pantallas
conservan su contrato.

## 1. Decisiones (2026-10-29)

| Tema | Decisión |
|---|---|
| Librería | **react-native-paper 5.15.3** (Material Design 3, la estable). La 6.0.0-alpha no se usa: tiene cambios incompatibles y su API puede cambiar hasta la beta. |
| Material 3 Expressive | Ninguna librería de React Native lo trae todavía. Se aplican sus rasgos con el tema (esquinas redondeadas, contraste tipográfico, movimiento); no hay un modo "Expressive". |
| Temas | **Claro** (crema y terracota) y **oscuro** (azul noche y cobre), los de la marca (`portal/src/theme/tokens.css`). Con un **selector** en la app: Sistema / Claro / Oscuro, que se recuerda. |
| Tipografía | **Josefin Sans** (títulos) y **DM Sans** (texto), las mismas de la web, dentro de la app (`@expo-google-fonts`). |
| Color dinámico de Android (Material You) | **No.** Pisaría la paleta de la marca. |
| Fondo | La foto `camera-background.jpg` **difuminada y lavada en blanco (78 % de lavado en los dos temas, con la foto `camera-background-2` (una cámara con una mano que baja hacia ella, 1638×2048, fondo blanco liso), escalada a 540 px de ancho y con un desenfoque de radio 4: la cámara se reconoce y el resto es blanco limpio, que deja sitio para el texto; se recorta por los lados en los teléfonos, pero cámara y mano están al centro. La foto anterior, un montón de equipo de 303×660, quedaba confusa y se pixelaba con poco desenfoque)** en todas las pantallas (en el tema oscuro, lavada en azul noche). |

## 2. Cómo está hecho (fase 1, ya en el código)

- **Paletas** (`src/theme/palettes.ts`): `LIGHT_PALETTE` y `DARK_PALETTE`, con los roles de Material 3 (`primary`, `surface`,
  `outline`, `error`...). Son la paleta de la web con otro reparto de colores: **el color principal de la app es el azul pizarra del logo `#3A4466`** (botones, foco, opción elegida, botón flotante; 9,5:1 con blanco) y el terracota `#B04A25` es el color secundario. Así los botones no se confunden con el rojo de las acciones destructivas ni con las tarjetas durazno. (La web pinta sus botones en terracota con una letra de 19 px en negrita; la app tiene letras más pequeñas y otro reparto.) En el tema oscuro el principal sigue siendo el cobre. Una prueba comprueba el contraste de cada par de roles en los dos temas.
- **Tema de Paper** (`src/theme/paperTheme.ts`): `buildPaperTheme(scheme)` convierte la paleta en el tema de Material 3, con las
  dos fuentes. Sin color dinámico.
- **Selección del tema** (`src/appearance/`, `src/storage/appearanceStorage.ts`, `src/hooks/useAppTheme.ts`): la preferencia
  (`system` / `light` / `dark`) vive en un store de Zustand y se guarda en `expo-secure-store`. **Por defecto es `light`**, no lo
  que diga el teléfono, y no hay selector todavía: hasta la fase 5 la app se ve siempre clara.
- **Fuentes** (`src/theme/fonts.ts`, `useAppFonts`): se cargan al abrir; si fallan, la app abre con la fuente del sistema.
- **Fondo** (`src/components/AppBackground.tsx`, `src/navigation/appScreenLayout.tsx`): la foto va **ya difuminada** en
  `assets/background.jpg` (se genera con `scripts/blur-background.ps1` desde `assets/background-source.jpg`; un desenfoque en
  tiempo de ejecución costaría memoria y se vería distinto en Android y en iOS) con un lavado encima (`src/theme/background.ts`).
  Se pone **una sola vez**, en el `screenLayout` del `Stack`: cada pantalla lleva su propia copia, opaca, para que una pantalla no
  deje ver la anterior mientras se desliza. Es decoración: oculta del lector de pantalla y sin recibir toques.
- **Compatibilidad** (`src/theme/tokens.ts`): `colors` sigue existiendo con los mismos nombres, ahora con los valores de la paleta
  clara de la marca. Los 52 archivos que lo importan no se tocan; pasan al tema uno por uno en las fases siguientes.

## 3. Fases

Un PR por fase, hacia `develop`. Cada uno pasa `tsc`, ESLint y todas las pruebas.

| Fase | Qué | Estado |
|---|---|---|
| 0 | Red de seguridad: pruebas verdes de partida y lista de recorridos a repetir en el teléfono | Hecha |
| 1 | Cimientos: Paper, paletas, tema, fuentes, fondo, preferencia de tema (sin selector) | **Hecha** |
| 2 | Componentes base por dentro: botón, campo de texto, selector, fecha, duración, estado, tarjetas, cargando y mensajes | **Hecha** (ver §3.1) |
| 3 | Estructura: cabeceras (banda `#3A4466` con el logo, como en la web), botón flotante de "Nueva reserva", y los estilos pasan a depender del tema (`makeStyles`) | **Hecha** (ver §3.2) |
| 4 | Pantallas, de menor a mayor riesgo: lista y avisos; detalle de la reserva; formularios (nueva, editar, reprogramar, motivo); contrato, pago y reembolsos; acceso, verificación y contraseña | **Hecha** (ver §3.3) |
| 5 | Activar el tema oscuro: **selector** Sistema / Claro / Oscuro (en la pantalla de Ajustes), revisión de cada pantalla en los dos temas, letra grande, Android a borde completo | **Hecha** (ver §3.4) |
| 6 | Limpieza: regla de ESLint contra colores fijos (hoy lo hace `noFixedColors.test.ts`), actualizar documentos. `colors` de `tokens.ts` ya se quitó en la fase 4 | Pendiente |

### 3.1 Fase 2: qué cambió en los componentes base

Por dentro, con las mismas props y las mismas etiquetas de accesibilidad (las 715 pruebas anteriores pasan sin tocarlas):

| Componente | Ahora |
|---|---|
| `ActionButton` | Botón relleno de Material 3 (`Button` de Paper); rojo con `error` / `onError` para las acciones que liberan el horario |
| `FormInput` | Campo de texto con contorno (`TextInput` outlined): la etiqueta va **dentro** del campo y sube al borde con el foco o con texto; el mensaje de error va debajo (`HelperText`). **El texto de ejemplo (`placeholder`) solo se ve con el foco**, como manda Material 3 |
| `SelectField` | Mismo aspecto que el campo de texto (contorno y etiqueta sobre el borde); la lista sigue siendo un `Modal`, con hoja redondeada y la opción elegida resaltada |
| `DateTimeField` | Botones con contorno en Android; los selectores nativos en iOS |
| `DurationPicker` | Botones segmentados (`SegmentedButtons`) |
| `StatusBadge` | Píldora tonal con un **punto del color del estado** y su nombre (antes, toda la píldora iba del color del estado con letra blanca): el color nunca es lo único que dice el estado. Los colores salen de la paleta del tema (`STATUS_COLOR_ROLES`) |
| `ScreenLoader`, `ScreenMessage`, `Placeholder`, `AmountRow` | Los de Paper (indicador, texto con la fuente del tema, botón) |
| **`AppCard`** (nuevo) | Tarjeta tonal de Material 3 (`surfaceVariant`, esquinas de 16); la usan `BookingActions`, `BookingDetails`, `ContractCard`, `EditBookingForm` (bloqueado), `NotificationListItem`, `PendingPaymentsCard`, `RefundListItem`, `RescheduleForm`, `SettlementCard` y `ShareLinkCard` |

Todos leen sus colores del tema, así que ya siguen el tema claro y el oscuro. **Quedan para la fase 3 y la 4** las tarjetas que se tocan (`BookingListItem`, `NotificationsButton`), `ContractText`, `ExtrasModal`, `ExtraRow` y `ExtrasFields`, los textos sueltos de las pantallas (que aún usan `Text` de React Native con colores de `tokens.ts`) y las cabeceras.

### 3.2 Fase 3: qué cambió en la estructura

- **Cabecera de la app** (`src/navigation/headerOptions.ts`): la banda azul pizarra `#3A4466` del logo, con el título en blanco y Josefin Sans, **igual en los dos temas** (como la banda de la web). Sin línea debajo. La flecha de volver y los botones del encabezado van en blanco.
- **Inicio** (`HeaderBrand`): en la banda va el logo con su borde plateado y "PHOTOSENSE STUDIO" en mayúsculas con espacio entre letras, igual que en la web. "Salir" queda a la izquierda, en blanco.
- **Botón flotante "Nueva reserva"** (`NewBookingFab`, el `FAB` extendido de Material 3 en el color principal) en la esquina de abajo a la derecha, sobre la barra de navegación del sistema; reemplaza el "+ Nueva" que estaba en la cabecera (`NewBookingButton`, eliminado). La lista deja espacio al final para que no tape la última reserva.
- **Barra de estado** (`statusBarStyleFor`): íconos claros sobre la banda, en cualquier tema; sin banda (acceso, cargando, sin conexión) siguen el tema.
- **`makeStyles`** (`src/theme/makeStyles.ts`): la forma de escribir estilos que dependen del tema. `const useStyles = makeStyles((palette, scheme) => ({ title: { color: palette.onSurface } }))` y, dentro del componente, `const styles = useStyles();`. Se recalculan solo cuando cambia el tema. Lo usan `HeaderBrand` y `SignOutButton`; las fases 4 y 5 lo usan en las pantallas.

### 3.3 Fase 4: qué cambió en las pantallas

**Ajustes tras probar en el teléfono:** el botón flotante es azul (`brand`); el color principal pasa a ser el azul del logo, y una acción destructiva (`ActionButton` `danger`) va **con contorno rojo** en vez de rellena, para que nunca se confunda con la acción principal (terracota y rojo casi no se distinguían sobre una tarjeta durazno); el estado de una reserva (`StatusBadge`) vuelve a ser una píldora **rellena con el color del estado** y la tarjeta de la lista lleva una **franja** de ese color a la izquierda; la hoja del selector (tipo de sesión, paquete) va sobre la superficie con la opción elegida rellena en azul.

Todas las pantallas y componentes con estilos propios pasaron de los colores fijos de `tokens.ts` a **la paleta del tema** (`makeStyles`), y todo el texto usa las fuentes de la marca:

- **Colores:** cada estilo lee su color de un rol de la paleta (`onSurface`, `onSurfaceVariant`, `primary`, `error`, `surfaceVariant`...). Así, al activar el tema oscuro en la fase 5, **nada se queda en claro**. Los pares de texto y fondo ya cumplen WCAG AA en los dos temas (prueba de `palettes`) y también sobre la parte más clara y más oscura de la foto de fondo (prueba de `background`).
- **Fuentes:** el texto que tenía un `fontSize` y un `fontWeight` ahora tiene su familia: los títulos de 17 puntos o más en negrita van en **Josefin Sans** y el resto en **DM Sans** (normal, media o negrita). En Android cada peso es un archivo distinto, por eso no queda ningún `fontWeight` suelto.
- **Los colores dinámicos** (`ApiStatus`, los estados) ahora son roles de la paleta (`ApiHealthDescription.color` es una clave de `Palette`).
- **`tokens.ts` ya no tiene `colors`**: solo quedan `spacing` y `radius`. `src/theme/__tests__/noFixedColors.test.ts` impide que vuelvan: ninguna pantalla ni componente puede escribir un color (hex o `rgb()`), importar `colors` ni usar un `fontWeight`.
- Comportamiento, rutas, hooks, formularios y API: sin cambios.

### 3.4 Fase 5: el tema oscuro, activado

- **Selector de apariencia** (`ThemeSelector`, en la pantalla de **Ajustes**, `SettingsList`; se entra con la **tuerca** a la derecha de la cabecera del inicio, frente a "Salir"; allí también están "Cambiar contraseña" y el estado del servidor ("Servidor conectado", `ApiStatus`), que ya no están en el inicio, para que esa pantalla solo tenga lo que se usa a diario): tres botones, **Sistema / Claro / Oscuro**. La elección se aplica al instante a todas las pantallas y se recuerda (`expo-secure-store`). El valor de partida sigue siendo **Claro**, no lo que diga el teléfono, hasta que se elija otra cosa. "Sistema" sigue el tema del teléfono y cambia con él.
- **Revisión de las dos variaciones:** `ThemedRendering.test.tsx` renderiza 10 escenas (cabecera, estados, botones, tarjetas, campos, selector, mensajes, contrato, extras, selector de tema) en cada tema y comprueba que **no aparece ningún color del otro tema**; también que cada tema sí pinta colores propios, para que esa ausencia signifique algo. Un color que se quedara fijo en claro haría fallar la prueba.
- **Botón flotante:** usa el color principal del tema: azul del logo en claro, cobre en oscuro.
- **Interruptor del contrato:** sigue el tema.
- **Letra grande:** la altura fija del selector pasó a mínima (crece con la letra) y el nombre de la cabecera deja de crecer a partir de 1,2 veces.
- **Android a borde completo** (`useBottomInset`): las pantallas que se desplazan (inicio, avisos, reembolsos, detalle, reprogramar y todos los formularios) dejan al final el espacio de la barra de navegación del sistema. Sin barra, no cambian.
- **Lo que no sigue el tema de la app**, porque lo dibuja el sistema: el diálogo de fecha y hora de Android, las alertas (`Alert`) y los íconos de la barra de navegación del sistema siguen el tema **del teléfono**. Con la app en oscuro y el teléfono en claro, esos diálogos salen claros.

**Recorridos a repetir en el teléfono** (antes de fusionar las fases 2 a 5): iniciar sesión y salir; crear una reserva con
paquete, extras y descuento; editarla; firmar el contrato en el celular; registrar un pago; completar; cancelar con motivo;
reembolsos; avisos y push; enlace al cliente; cambiar la contraseña; verificar el correo.

## 4. Reglas que se mantienen

- Un componente por archivo; los tipos solo en `src/types/`, en la carpeta espejo; las funciones puras en `src/utils/`; la
  navegación solo por `src/navigation/`; todo con comentario.
- Un componente nuevo de la fase 2 en adelante **no escribe colores**: los lee del tema (`useAppTheme`).
- Los componentes de Paper se envuelven para conservar las props y las etiquetas de accesibilidad de los actuales.
- La lógica, la API y las `allowedActions` no se tocan.
- Paper y las fuentes son JavaScript: se espera que no haga falta un build nuevo de EAS; el primer build de prueba lo confirma.
