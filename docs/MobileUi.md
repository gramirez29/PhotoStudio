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
  `outline`, `error`...). Son la paleta de la web, con una excepción: el botón de la app es `#B04A25` y no `#C4552E`, porque con
  etiquetas pequeñas el terracota de la web da 4,08:1 con texto crema y el `#B04A25` da 4,96:1 (WCAG AA exige 4,5:1). Una prueba
  comprueba el contraste de cada par de roles en los dos temas.
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
| 2 | Componentes base por dentro: botón, campo de texto, selector, fecha, duración, estado, tarjetas, cargando y mensajes | Pendiente |
| 3 | Estructura: cabeceras (banda `#3A4466` con el logo, como en la web), botón flotante de "Nueva reserva", y los estilos pasan a depender del tema (`useStyles`) | Pendiente |
| 4 | Pantallas, de menor a mayor riesgo: lista y avisos; detalle de la reserva; formularios (nueva, editar, reprogramar, motivo); contrato, pago y reembolsos; acceso, verificación y contraseña | Pendiente |
| 5 | Activar el tema oscuro: **selector** Sistema / Claro / Oscuro (en el inicio, junto a "Cambiar contraseña"), revisión de cada pantalla en los dos temas, letra grande, Android a borde completo | Pendiente |
| 6 | Limpieza: quitar `tokens.ts`, regla de ESLint contra colores fijos, actualizar documentos | Pendiente |

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
