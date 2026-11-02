# Correos del sistema

Todos los correos que envía PhotoSense Studio tienen **la misma apariencia**: banda azul pizarra (`#3A4466`) con el logo y el nombre, página crema (`#FBF3EA`), tarjeta durazno (`#F6D5BE`), línea terracota (`#C4552E`) y paleta oscura (`#020D2C`, `#26324F`, cobre `#E3A97F`) para los programas de correo en modo oscuro. Se arman con una sola plantilla, `BrandedEmail` (`backend/src/PhotoStudio.Application/Emails/`); ningún correo escribe su propio HTML.

## Cuáles son

| Correo | Para quién | Asunto | Cuándo |
|---|---|---|---|
| Código de verificación | Cliente | Tu código de verificación de PhotoSense Studio | Pide el código en su enlace (`/b/{token}`): firmar, pagar, ver sus fotos |
| Verifica tu correo | Fotógrafo | Verifica tu correo de PhotoSense Studio | Al crear la cuenta |
| Código para entrar | Fotógrafo | Tu código para entrar a PhotoSense Studio | Segundo factor del inicio de sesión en `/studio` |
| Contrato firmado | Cliente | Tu contrato firmado con PhotoSense Studio | Al firmar: el texto exacto que firmó |
| Pruebas listas | Cliente | Tus fotos de prueba ya están listas | Al publicar las pruebas |
| Selección abierta otra vez | Cliente | Tu selección está abierta otra vez | Al reabrir su selección (sin el motivo) |
| Fotos listas para descargar | Cliente | Tus fotos ya están listas | Al entregar la galería (saldo cero) |

Los de galería **no llevan enlace** a propósito: el enlace de la reserva le llega al cliente por WhatsApp y los códigos por correo; juntarlos en un solo buzón dejaría que quien lo lea haga todo.

## Cómo se ven las partes
- **Código:** en un recuadro propio, grande, con borde punteado terracota. En el texto plano sale como `Tu código: 123456` y es el único número de seis dígitos del mensaje.
- **Contrato:** en un recuadro, tal como se firmó, con el texto codificado para que nada de lo firmado pueda volverse HTML.
- **Pasos, nota y cierre:** los pasos numerados en terracota; la nota con una barra a la izquierda.

## El logo viaja dentro del correo
El logo ya **no** es un enlace a `…/icon-192.png` del sitio. Viaja **adjunto dentro del propio correo** (imagen incrustada por `Content-ID`, `cid:photosense-logo`), y el HTML la nombra.

**Por qué:** el sitio de develop está protegido con **Cloudflare Access** (pide iniciar sesión), así que el correo apuntaba a una imagen que el programa de correo no podía bajar: recibía la página de login y mostraba el logo roto o nada. Además muchos programas de correo bloquean las imágenes remotas hasta que el lector lo permite; las incrustadas se ven de inmediato. Tampoco depende de que el sitio esté disponible cuando el cliente abre el correo.

- El logo sale de un recurso incrustado en el ensamblado (`Emails/logo.png`, el mismo ícono del sitio, 192 × 192).
- Resend lo recibe en `attachments` con `content_id`; sin configurar nada más. En local (`LogEmailSender`) solo se registra el texto.
- Agregar un correo nuevo: crear su `EmailContent` y llamar a `BrandedEmail.Message(to, asunto, contenido)`. Con eso lleva la apariencia, el texto plano y el logo.

## Verificación
`EmailLookTests` comprueba que **cada** correo (los de código y el del contrato incluidos) tiene los colores de la marca, la paleta oscura, el logo incrustado y ningún enlace; que el código sale en su recuadro y una sola vez en el texto; y que el contrato sale codificado. `ResendEmailSenderTests` comprueba que las imágenes viajan como adjuntos con su `content_id`.

# Ícono de las notificaciones push

El ícono pequeño de las notificaciones de Android es `mobile/assets/notification-icon.png` (silueta blanca del logo sobre transparente, 96 × 96) con el color `#3A4466`, configurados en el plugin `expo-notifications` de `mobile/app.json`.

**No llega por actualización de JavaScript:** es un recurso nativo y entra en el **build** de la app. Hay que hacer un build nuevo de EAS (`npx eas-cli build --platform android --profile development`) y reinstalar la app para verlo.

**Historia:** esta configuración se había hecho el 29 de octubre en el commit `5b83df7`, pero quedó en la rama `feature/portal-extras-palette-logo` y **nunca se fusionó a `develop`**; por eso los builds hechos desde `develop` no la tenían.
