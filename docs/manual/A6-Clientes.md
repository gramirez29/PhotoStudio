# A6 — App móvil y sitio web

## Parte 1 — App móvil (`mobile/`)

### 1.1 Qué es
App de Android (iOS pendiente) con Expo SDK 57 y React Native 0.86, TypeScript 6.0.2 **estricto** (`strict`, `noUncheckedIndexedAccess`, etc.). Rutas con Expo Router en `src/app/`. Estado del servidor con TanStack Query 5; estado de UI y sesión con Zustand 5. Tema con `react-native-paper` (Material Design 3).

Identidad: nombre visible `PhotoSense Studio`; `slug` y `scheme` técnicos `photostudio`; paquete Android `com.photosensestudio.app` (**no se puede cambiar después de publicar**); proyecto de EAS `@g.raba29-dev/photostudio`.

### 1.2 Estructura de `mobile/src`
| Carpeta | Contenido |
|---|---|
| `app/` | Pantallas (solo componen). Inicio, nueva reserva, detalle, reprogramar, contrato, pago, avisos, reembolsos, galería, galerías por archivar, ajustes, cambio de contraseña, verificación de correo, login |
| `components/` | UI (un componente por archivo) |
| `hooks/` | Estado y lógica de pantalla o de datos |
| `forms/` | Validación y armado de solicitudes (lógica pura) |
| `api/` | Acceso a la red: cliente HTTP, un archivo por área, validación en tiempo de ejecución (`guards.ts`) |
| `types/` | **Todas** las interfaces y tipos, en espejo de las capas |
| `utils/` | Funciones **puras** de formato y normalización (es-CR) |
| `constants/` | Valores `as const` (enums del backend, servicios, subida de fotos) |
| `theme/`, `appearance/` | Paletas, fuentes, fondo, preferencia de tema |
| `session/`, `push/`, `menu/` | Sesión, notificaciones, menú lateral |
| `navigation/` | Envoltorios con efectos sobre el router |
| `media/` | Efectos nativos de fotos (subida desde el celular) |
| `config/` | Variables de entorno de la app |

### 1.3 Reglas de código (obligatorias)
1. **Prohibido `any`**, explícito o implícito. Las respuestas de red entran como `unknown` y se validan antes de tiparse.
2. Ninguna interfaz ni tipo fuera de `src/types/`; `types/` solo tiene tipos.
3. Toda función que formatee o normalice va en `utils/` y es pura; los efectos (router, red, almacenamiento) no van ahí.
4. Un componente por archivo; las pantallas solo componen.
5. Hooks solo cuando hay estado, validación o navegación mezclados con el JSX.
6. Colores y fuentes solo desde el tema (`useAppTheme` / `makeStyles`); una prueba (`noFixedColors`) prohíbe colores fijos.
7. Textos visibles en español (`es-CR`), moneda por defecto CRC.
8. Los clientes **no implementan reglas de estado**: muestran solo las `allowedActions` que devuelve la API.
9. Dependencias con `npx expo install`. No editar `ios/` ni `android/` (se generan).
10. Antes de `npx expo start --clear` correr `npx tsc --noEmit` (o `npm run verify`).

### 1.4 Sesión
Pantalla de acceso con "Iniciar sesión" y "Crear cuenta". Estados: `restoring`, `offline`, `signedOut`, `signedIn`. El access token solo en memoria; el refresh token en el almacén seguro del teléfono. `httpClient` adjunta el token y, ante un 401, renueva **una sola vez** (renovación compartida entre peticiones simultáneas porque el refresh token es de un solo uso) y reintenta. Sin red al arrancar queda en `offline` con "Reintentar" (no cierra la sesión por un fallo de red). Al cerrar sesión se vacía el caché.

### 1.5 Tema
Dos paletas de marca: claro (crema `#FBF3EA`, durazno `#F6D5BE`, texto `#0B1A40`, acento terracota `#C4552E`) y oscuro (azul noche `#020D2C`, cobre `#E3A97F`), con el azul pizarra del logo `#3A4466`. Tipografía Josefin Sans (títulos) y DM Sans (cuerpo). El selector Sistema / Claro / Oscuro está en Ajustes. El logo se pinta tenue como marca de agua detrás de todas las pantallas. Detalle en `docs/MobileUi.md`.

### 1.6 Menú lateral
Inicio, Avisos (con contador de no leídos), Reembolsos pendientes (con contador), **Galerías por archivar**, Ajustes y, al final, Cerrar sesión (con confirmación). La hamburguesa lleva un puntito si hay avisos o reembolsos pendientes.

### 1.7 Pantallas y funciones
- **Inicio:** lista de reservas paginada de 5 en 5 (lo más reciente primero) con franja y píldora de color por estado; botón flotante "+" para nueva reserva.
- **Nueva reserva:** cliente, teléfono (se escribe como `7018-9220`, se guarda `+50670189220`), correo opcional, tipo de sesión (lista de servicios), paquete (rellena precio y duración), extras (modal con +/−), descuento, total a pagar, fecha y hora, duración. Un reenvío idéntico responde 409 y no duplica.
- **Detalle de reserva:** datos, desglose del precio, contrato (`ContractCard`), pagos, liquidación si aplica, tarjeta del enlace (`ShareLinkCard`: crea el enlace y abre WhatsApp), comprobantes por verificar (`PendingPaymentsCard`), tarjeta de galería si la sesión está completada, y las acciones permitidas.
- **Acciones:** firmar contrato (en el celular o en papel), registrar pago, reprogramar, completar, cliente ausente, revertir ausencia, cancelar (con motivo si estaba confirmada), editar reserva.
- **Avisos:** bandeja paginada; el mensaje de WhatsApp lo redacta la app.
- **Reembolsos:** lista, "Marcar como devuelto", escribir por WhatsApp.
- **Galería:** ver [Parte 3](#parte-3--subida-de-fotos-desde-el-celular-y-archivo).
- **Ajustes:** tema, cambio de contraseña, estado del servidor y, solo para operador de sistema, "Liberar reservas vencidas".

### 1.8 Notificaciones push
`PushRegistration` registra el teléfono con `POST /api/push-devices` al haber sesión (canal Android `default`, permiso, token de Expo). Un push con la app abierta recarga la bandeja; tocarlo abre la reserva. **Todo efecto de `expo-notifications` vive en `src/push/pushManager.ts`.** Solo funciona en un dispositivo físico con el build de desarrollo, no en Expo Go. El ícono pequeño de Android es `assets/notification-icon.png` (silueta blanca, color `#3A4466`). Guía completa: `docs/PushNotifications.md`.

### 1.9 Compilar la app (EAS)
La app usa un **build de desarrollo** (APK con `expo-dev-client`):

```
cd mobile
npx eas-cli build --platform android --profile development
npx expo start --dev-client      # misma red Wi-Fi, o --tunnel
```

El build de desarrollo **no trae el JavaScript**: lo sirve `expo start`. Hace falta un build nuevo cada vez que cambia una dependencia nativa, el ícono, el splash o `app.json`; un cambio solo de JavaScript no lo necesita. El ícono, el ícono adaptable, el splash (fondo `#020D2C`) y el nombre vienen de `app.json` y `assets/`.

### APK independiente (sin Expo), contra develop
El perfil `preview` de `eas.json` genera un **APK normal**: el JavaScript va dentro del paquete, no hace falta computadora ni `expo start`, y habla con la API de develop (`https://develop-api.photosensestudio.com`, fijada en el perfil). Se instala igual que cualquier APK y funciona con datos móviles o Wi-Fi.

```
cd mobile
npm run build:apk          # = npx eas-cli build --platform android --profile preview
```

Al terminar, EAS entrega un enlace y un código QR para descargar el APK (también en expo.dev → el proyecto → Builds). En el teléfono: abrir el enlace, permitir "instalar apps de fuentes desconocidas" para el navegador y abrir el archivo. El perfil usa las variables del entorno `development` de EAS (ahí vive `GOOGLE_SERVICES_JSON`, necesario para el push) y sube el número de versión solo (`autoIncrement`). Cualquier cambio de JavaScript exige hacer otro build; no hay actualizaciones por aire configuradas.

Variables y credenciales: `google-services.json` no se versiona (EAS lo recibe como variable de archivo `GOOGLE_SERVICES_JSON`); la clave FCM V1 vive solo en EAS Credentials.

En un teléfono físico durante el desarrollo, `EXPO_PUBLIC_API_URL` usa la IP de la computadora en la red local, y hace falta una regla de firewall de entrada para TCP 8080 limitada a la subred local.

**Pendiente:** perfil `production` de EAS, Google Play, iOS (Apple Developer + APNs).

## Parte 2 — Sitio web (`portal/`)

React 19 + Vite + TypeScript, mobile-first, en Cloudflare Pages. Un solo sitio con tres zonas:

| Ruta | Quién | Qué |
|---|---|---|
| `/` | Todos | Página principal de la marca |
| `/b/{token}` | Cliente | Portal de su reserva |
| `/studio` | Fotógrafo | Web del fotógrafo: galerías, subida de pruebas y finales, ZIP |
| `/studio/galleries/{id}` | Fotógrafo | Detalle de una galería |
| `/studio/archive` | Fotógrafo | Galerías por archivar, con descarga a una carpeta |

La dirección se lee una sola vez; cada enlace es una carga completa de página. Los temas (claro y oscuro) comparten los tokens de la marca (`src/theme/tokens.css`). La política de contenido está en `public/_headers` (permite R2 para subir y bajar fotos). Detalle en `docs/ClientPortalWeb.md`.

### Portal del cliente
Muestra la reserva, la cotización, el contrato (leer y firmar con declaración de cédula, personas, lugar y uso de imagen), el comprobante SINPE y la tarjeta "Tus fotos" (selección y descarga). Una sola sesión de portal sirve para todo; un aviso "Tu correo ya está confirmado en este dispositivo hasta el …" permite cerrarla.

### Web del fotógrafo
- Inicio de sesión con contraseña y código por correo; lista de galerías paginada.
- **Pruebas:** se eligen JPEG, se reducen a 720 px, se les pone el logo y se comprimen **en el navegador** (calidad 0,4; baja si pesa más de 1 MB); antes de subir se ve una muestra con la primera foto. Se suben en lotes de 10 directo a R2.
- **Finales:** se emparejan por nombre con lo que eligió el cliente; suben tal cual (hasta 50 MB) con su miniatura y sus datos (tamaño en píxeles y fecha).
- **ZIP:** hasta 500 MB, con progreso.
- Publicar pruebas, reabrir selección, publicar finales, entregar extras, liberar espacio.
- **Galerías por archivar:** lista las entregadas; "Descargar a una carpeta" (Chrome/Edge) baja los finales comprobando el tamaño de cada uno; "Liberar espacio" se ofrece solo después de la descarga, o si el fotógrafo confirma que ya tiene respaldo.

## Parte 3 — Subida de fotos desde el celular y archivo

Todo lo que se hace en la web de `/studio` también se puede hacer en el celular (criterio acordado), con las mismas reglas porque comparten los manejadores del backend.

**Pruebas (`mobile/src/media/`):** selector del sistema (sin permisos) → la librería nativa reduce la foto a 720 px ya girada → Skia dibuja el logo (70 % del lado corto, 60 % de opacidad, sombra suave) y codifica JPEG calidad 40/30/20 → se muestra una muestra para confirmar → lotes de 10 directo a R2 con progreso. El JPEG de Skia no lleva metadatos (ni ubicación ni cámara).

**Finales:** tal cual, emparejados por nombre, con miniatura y datos. **ZIP:** desde el archivo, sin leerlo a memoria.

**Importante:** la app debe quedar abierta mientras sube. Las constantes de `mobile/src/constants/upload.ts` y `portal/src/constants/proof.ts` son **una sola especificación**: si cambia una, cambia la otra.

**Por verificar en un teléfono real:** que el selector entregue los finales sin recomprimir, la orientación de las dimensiones, el aspecto de la marca de agua frente a la web y la memoria con fotos de muchos megapíxeles.

**Archivo:** "Liberar espacio" en la galería y la pantalla "Galerías por archivar" (menú lateral). La app avisa una vez a los 90 días y el sistema archiva a los 180.
