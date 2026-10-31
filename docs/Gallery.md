# Galería: plan de desarrollo

Plan para construir la galería (agregado `Gallery`, máquina de estados G1–G8 de `CLAUDE.md` §8.6). Decisiones tomadas con el fotógrafo el 2026-10-30. Si el código y este documento difieren, el código manda y este archivo se actualiza en el mismo cambio.

## 1. Qué es y por qué existe

Después de la sesión, el cliente ve **pruebas** (fotos de calidad baja y con marca de agua), elige las que quiere de su paquete más las extras, el fotógrafo edita **solo esas** y las entrega. La descarga final se **bloquea hasta saldo cero**. Eso es lo que protege el dinero: el cliente no se lleva fotos sin pagar.

## 2. Decisiones

| Tema | Decisión |
|---|---|
| Almacenamiento | **Cloudflare R2 desde el inicio**, para pruebas y finales. No se usa Drive. |
| Límite por galería | **150 fotos** de prueba. |
| Fotos incluidas | Se **congelan al crear la galería** con el paquete (Básico 15, Estándar 30, Premium 50, valores por defecto en la configuración, no amarrados al paquete). El fotógrafo puede sumar **fotos de regalo** a una galería concreta. |
| Foto extra | **₡3 000** por defecto, congelado en la galería (el fotógrafo puede cambiarlo en esa galería). Se cobra al enviar la selección. |
| Pruebas | Se hacen **en el cliente que sube** (navegador o celular), nunca en el servidor: reducción a ~720 px de lado largo, JPEG calidad ~40, **marca de agua grande al centro con el logo** (`mobile/assets/watermark.png`, el mismo del fondo de la app), sin EXIF ni GPS. El original no sale del equipo del fotógrafo. |
| Subida | **Web con login** (computadora) y, después, **celular** (Lightroom móvil también exporta JPEG). Una sola especificación visual de la marca para las dos. |
| Entrega | Finales en R2. El cliente descarga con **URLs firmadas de vida corta** que la API solo emite con saldo cero. Un **ZIP** lo arma el worker al publicar. |
| Liberar espacio | El fotógrafo puede **descargar la galería** completa y luego **liberarla** de R2 (con confirmación). Si no, se archiva sola a los 90 días (G8). |
| Pocos pasos | Es la regla de diseño: completar la sesión es el único paso de la app para el fotógrafo; el cliente solo abre su enlace de siempre. |

## 3. Flujo

**Fotógrafo**
1. Toca **Completar** en la app. El backend crea la galería solo (consume `SessionCompleted`); no hay botón de crear galería.
2. Abre el sitio en la computadora (ya con sesión), elige la galería y arrastra los JPEG (hasta 150). El navegador reduce, comprime y marca cada foto, muestra una vista previa y sube a R2 en paralelo con progreso y reintento.
3. Toca **Publicar pruebas** (abre la selección, G2). El cliente recibe un **correo automático** con su enlace; si quiere, manda además el WhatsApp.
4. Le llega un **push** cuando el cliente envía su selección. La app y la web muestran las fotos elegidas y la **lista de nombres de archivo originales** (botón copiar) para filtrar en Lightroom.
5. Edita, sube los **finales a calidad completa** (web o celular) y toca **Publicar finales** (G6). El worker arma el ZIP.
6. Con saldo cero la galería pasa a **entregada** (G7), también al verificar un pago. El cliente recibe el aviso.
7. Cuando quiera, **descarga** la galería a su computadora y la **libera** de R2.

**Cliente** (el mismo enlace `/b/{token}`, nunca instala nada)
1. Abre el enlace, confirma con el código por correo y entra a **Ver galería**.
2. Lee la leyenda: *verá pruebas en calidad baja y con marca de agua; nuestro compromiso es entregarle fotografías de excelente calidad*.
3. Elige fotos. El **contador baja** con cada una ("Te quedan 12 de 15"). Al llegar a cero ve: *las fotos adicionales cuestan ₡3 000 cada una; si hay otra que te encante, ¡puedes sumarla!*, con el precio y el total acumulado siempre visibles (nada de letra chica).
4. Antes de enviar ve que **se le notificará al fotógrafo** cuáles fotos va a editar. Al enviar, la selección queda **bloqueada**; solo el fotógrafo puede reabrirla.
5. Con saldo cero, descarga sus fotos (una por una o el ZIP).

**Cómo se evita llevarse fotos sin pagar:** la selección se bloquea al enviarla; las extras se cobran en ese momento y suben el saldo; el fotógrafo solo edita lo elegido; la API no emite ninguna URL de descarga con saldo distinto de cero; las URLs duran minutos (5 por defecto) y la descarga ya iniciada termina aunque la URL venza. El clic derecho se deshabilita, pero es una capa débil: **una captura de pantalla no se puede impedir en una web**. Lo que protege es la baja calidad, la marca grande al centro y que el original nunca se sube.

## 4. Estados (ajustes sobre §8.6)

`Pending → InSelection → InEditing → Ready → Delivered → Archived`. Cambios:
- G1 **Create** congela `IncludedPhotos`, `ExtraPhotoPrice`, la política de borrado y el plazo.
- Nueva acción **AddBonusPhotos** (fotógrafo): suma fotos de regalo mientras la selección no esté enviada.
- G6 **PublishFinal** exige un final por cada foto elegida y dispara el ZIP (worker).
- G7 **Deliver** es automático con saldo cero, como en el diseño.
- Nueva acción **Release** (fotógrafo): borra los objetos de R2 antes del plazo; irreversible, con confirmación, y la galería queda como `Archived`. G8 hace lo mismo por tiempo.

## 5. Modelo de datos

Un agregado = un documento (`galleries`): `photographerId`, `bookingId`, `status`, `includedPhotos`, `bonusPhotos`, `extraPhotoPrice`, `photos[]` (máx. 150: id, nombre original, clave de la prueba, bytes, clave y bytes del final), `selection` (ids y fecha de envío), `zipKey`, `deliveredAt`, `version`. Las claves de R2 son aleatorias: el nombre original solo es etiqueta, nunca parte de la clave. Cabe de sobra en un documento (~50 KB).

**Billing:** hoy `Balance = PackagePrice − pagos verificados`. Las fotos extra entran como **cargo** en la reserva (`Charge` de tipo `ExtraPhotos`), de modo que `Balance = PackagePrice + Σ cargos − pagos verificados`; reabrir la selección antes de pagar anula el cargo (G5). Es un cambio de dominio, no solo un endpoint.

## 6. Seguridad

- **Sesión web del fotógrafo:** misma cuenta de la app (con correo verificado). La sesión va en una **cookie `HttpOnly; Secure; SameSite=Strict`** (sitio y API comparten dominio raíz, así que funciona); ningún token queda en el navegador. Endpoints propios (`/api/web/auth/*`) y **alcance limitado**: la sesión web solo abre lo de galerías (listar las suyas, subir, publicar, entregar), nunca clientes ni pagos. CORS con credenciales solo para el sitio. **Segundo factor (decidido, en el PR 3; "seguridad primero"):** un código de 6 dígitos por correo en navegadores nuevos, recordado 30 días en la cookie. Sin costo (Resend, que ya existe; entra en la cuota compartida de 3 000 al mes y 100 al día) y sin cambios en Railway (solo guarda el código temporal y los navegadores recordados en Mongo).
- **R2:** bucket privado; todo acceso por URL firmada; CORS del bucket solo para los orígenes del sitio y de la API; el acceso a R2 es de la API (claves en variables de Railway, nunca en el código).
- **Ver la galería exige el código por correo** (sesión de portal), no solo el enlace.
- **Subida:** solo JPEG, tamaño máximo por foto (**pruebas 1 MB, finales 50 MB**, decidido), máximo 150, la API comprueba con `HEAD` que el objeto existe antes de registrar la foto.
- **Límites de uso** por IP y por sesión en todos los endpoints nuevos, como en el resto de la API.

## 7. Costos (estimados)

R2: 10 GB gratis, luego $0.015 por GB al mes; salida de datos gratis. Un paquete Premium pesa ~300 MB de finales más el ZIP ≈ 600 MB; con 100 galerías ≈ 60 GB ≈ **$0.75 al mes**. Las pruebas (~20 MB por galería) casi no pesan. Liberar espacio y el archivo a los 90 días lo mantienen acotado.

## 8. UI: mismo look and feel que la app

Todo con la paleta de la marca (claro crema y terracota, oscuro azul noche y cobre, azul del logo `#3A4466`), Josefin Sans y DM Sans, esquinas y tarjetas de la app, y ambos temas.
- **Web** (`portal/`): los mismos tokens (`portal/src/theme/tokens.css`) y la misma tipografía y logo; páginas de inicio de sesión, lista de galerías, subida (zona de arrastre, vista previa, progreso) y detalle. En el portal del cliente, la sección "Ver galería" con la leyenda, la cuadrícula, el contador y el mensaje de extras.
- **App** (`mobile/`): componentes de Paper, `makeStyles`, colores solo de la paleta (la guardia `noFixedColors` lo comprueba), un componente por archivo, tipos en `src/types`, textos en español, y pruebas del tema claro y oscuro.
- Accesibilidad: etiquetas para lector de pantalla, contraste AA y que el color nunca sea lo único que dice un estado.

## 9. PRs

0. **Este plan** (documento).
1. **R2 y almacenamiento** (hecho, PR #55): puerto `IObjectStorage` (Application) y adaptador con `AWSSDK.S3` contra R2 (URLs firmadas de subida y lectura, `HEAD`, borrado); MinIO en el compose local; pasos de Cloudflare y variables en `docs/Deployment.md`.
2. **Agregado `Gallery`** (hecho en `feature/gallery-aggregate`; solo creación, lectura y edición de términos: las demás transiciones llegan con el PR que las usa, y los eventos de dominio con su primer consumidor): G1 al consumir `SessionCompleted`, fotos incluidas y de regalo, precio de extra, repositorio, índices, `allowedActions`, eventos y lectura para el fotógrafo.
3. **Sesión web del fotógrafo** (backend hecho, PR #58; las páginas del portal, inicio de sesión, código y lista de galerías, hechas en el PR 3b, `feature/gallery-web-pages`, `docs/ClientPortalWeb.md` §9; detalle en `docs/Authentication.md` §21): cookie, alcance limitado, CORS, página de inicio de sesión y lista de galerías.
4. **Subida de pruebas (web)** (backend hecho en el PR 4a, `feature/gallery-proofs-backend`: fotos en la galería, lugares de subida firmados, registro con comprobación del objeto real, lista, borrado, G2 y el correo al cliente; la página con la marca de agua y la compresión es el PR 4b, hecho; detalle en las secciones 11 y 12): URLs firmadas, registro de la foto, módulo de marca de agua y compresión, página de subida y "Publicar pruebas" (G2) con el correo al cliente.
5. **Selección del cliente en el portal:** sesión por código, cuadrícula con URLs de vida corta, contador, leyendas, mensaje de extras, sin clic derecho; enviar (G3) y push al fotógrafo.
6. **Cargo por extras en Billing:** `Charge` en la reserva, saldo ajustado, anulación al reabrir (G5).
7. **Finales, ZIP y entrega:** subida de finales, publicar (G6), ZIP en el worker, entrega con saldo cero (G7), descarga del cliente con URLs firmadas y avisos.
8. **App del fotógrafo:** tarjeta de galería en la reserva, cuadrícula de pruebas, selección con nombres de archivo, acciones (regalar fotos, abrir y reabrir selección, publicar), diálogo de copiar o WhatsApp para el enlace del cliente.
9. **Subida desde el celular:** selector de fotos, reducción, marca y compresión en el dispositivo (Skia), subida con progreso y reintento; **requiere un build nuevo de EAS**.
10. **Liberar espacio y archivo:** descarga de la galería para el fotógrafo, **Release** y el trabajo de G8.

**Antes del PR 4:** un prototipo local (fuera del repositorio) de la marca de agua y la compresión para aprobar cómo se ven las pruebas: tamaño, opacidad y calidad.

## 10. Riesgos y notas

- **Subida desde el celular (PR 9):** la app debe quedar abierta mientras sube (la subida en segundo plano no es fiable); se muestra el progreso y se reanuda con reintento. Habrá **dos implementaciones** de la misma marca (canvas en la web, Skia en el celular), que se mantienen con una sola especificación (lado largo, calidad, ancho y opacidad de la marca) y se comprueban a ojo con las mismas fotos. RAW no se sube: las pruebas y los finales son JPEG (Lightroom móvil los exporta).
- **Sin revocación de URLs emitidas:** una URL firmada ya emitida vive hasta que vence; por eso la vida corta, y la API registra cada emisión.
- **Responsabilidad:** guardamos fotos de los clientes; hace falta una política de borrado (90 días o liberación manual). La cláusula del contrato sobre las fotos extra y la retención **se omite por ahora** (decisión del fotógrafo) y se revisará con las demás cláusulas pendientes.
- **Tamaño de los finales:** decidido en 50 MB por foto y solo JPEG; si se quiere TIFF o archivos mayores habrá que usar subida en partes.
- **Correo al cliente:** lo redacta Claude (amable y profesional) como plantilla HTML con la paleta, las fuentes y el logo de la app, junto con el PR 4 (correo al abrir la selección) y el PR 7 (correo de entrega); el fotógrafo lo revisa en cada PR.
- **Sin decisiones abiertas** por ahora.

## 11. Las pruebas (PR 4a, backend)

**Cómo sube el fotógrafo** (siempre desde la web, `/api/web/galleries/{id}/...`; los bytes **nunca** pasan por la API):
1. `POST .../proofs/uploads` con `{ files: [{ name, bytes }] }` (1 a 50 archivos): la API valida nombre y tamaño y responde **un lugar por archivo**: un `photoId` generado por el servidor, una URL firmada de subida (PUT, 15 min como máximo) y la cabecera `Content-Type: image/jpeg` que va **dentro de la firma** (el almacenamiento rechaza otro tipo). La clave del objeto es `galleries/{galleryId}/proofs/{photoId}.jpg`: solo identificadores que generó el backend, nunca el nombre del archivo.
2. La página sube cada prueba directo a R2 con ese PUT.
3. `POST .../proofs` con `{ photos: [{ photoId, name }] }`: la API **mira el objeto real** (existe, es `image/jpeg`, pesa entre 1 byte y 1 MB, el nombre es válido) y solo entonces lo registra en la galería. Lo que no cumple se **borra del almacenamiento** y se devuelve con su motivo (`missing`, `wrong_type`, `empty`, `too_big`, `invalid_name`, `gallery_full`). Es necesario porque una URL firmada **no puede limitar el tamaño** de lo que se le envía. Repetir el registro no duplica nada (idempotente), y dos lotes a la vez se reintentan sobre la versión fresca.
4. `GET .../proofs` lista las pruebas (nombre del archivo, tamaño y una URL firmada de lectura de 10 min, hecha en cada petición y nunca guardada). `POST .../proofs/{photoId}/remove` quita una (la galería la olvida primero y el objeto se borra después: si el borrado falla queda un objeto huérfano, no una foto que apunta a nada).
5. `POST .../selection/open` (**G2**): `Pending → InSelection`, exige al menos una prueba (409 `gallery.no_photos`). A partir de aquí **las pruebas quedan congeladas** (409 `gallery.invalid_state` al subir o borrar).

**Reglas:** máximo 150 pruebas por galería (409 `gallery.full`), 1 MB por prueba, nombre de 1 a 200 caracteres sin caracteres de control (el nombre es solo una etiqueta, para encontrar la foto en Lightroom). La galería guarda sus pruebas **dentro de su documento** (nombre, tamaño, fecha); `allowedActions` suma `UploadProofs` (con espacio), `RemoveProof` y `OpenSelection` (con pruebas) y `GalleryResponse` trae `photoCount`.

**El correo al cliente** (evento `SelectionOpened`, primer consumidor del outbox de la galería: el repositorio guarda el evento **en la misma transacción** que el cambio, y una versión vieja no deja evento): `SelectionOpenedEmailHandler` envía **un** correo al email de la reserva (sin email, no envía nada y no es error). La galería recuerda cuándo se avisó (`SelectionNotifiedAt`), así una segunda entrega del evento no duplica el aviso. **El correo no lleva ningún enlace, a propósito**: el enlace de la reserva le llega al cliente por WhatsApp y los códigos para actuar con él, por correo; poner los dos en el mismo buzón dejaría que quien lo lea haga todo. Dice que las fotos están listas, los pasos (abrir el enlace de WhatsApp de siempre, "Ver galería", confirmar con el código, elegir), cuántas fotos incluye su paquete y cuánto cuesta una extra, y la leyenda: son pruebas en calidad baja y con marca de agua, y el compromiso es entregar fotografías de excelente calidad.

**La plantilla de los correos de marca** (`BrandedEmail`, reutilizable por los demás correos): banda azul pizarra `#3A4466` con el logo (si hay una dirección pública para él, el icono del sitio) y el nombre en mayúsculas espaciadas, página crema `#FBF3EA`, tarjeta durazno `#F6D5BE` con texto azul marino, una raya terracota `#C4552E` y, para las apps de correo en modo oscuro, azul noche `#020D2C` con cobre `#E3A97F`. Tablas y estilos en línea (lo único que todas las apps muestran igual); las fuentes de la marca con respaldo seguro (casi ninguna app carga fuentes web); todo texto que viene de datos va **codificado**; la versión de texto sale del mismo contenido. **En develop el logo no se verá en los correos** (el sitio está detrás de Cloudflare Access y el cliente no puede cargarlo): el correo se ve bien igual, con el nombre en la banda; en producción sí.

**Seguridad:** todo bajo `studio-web` (sesión de cookie, `Origin` del sitio, `no-store`), 60 acciones por minuto por IP en estos endpoints, la galería de otro fotógrafo y la que no existe responden 404 igual; los lugares de subida y las URLs de lectura son secretos que **no se registran**.

**Pruebas:** dominio (reglas de nombre y tamaño, límite de 150, congelado, evento, acciones), casos de uso (cada motivo de rechazo con su borrado, idempotencia, reintento por colisión, el orden guardar-luego-borrar, tolerancia al fallo del almacenamiento), el correo y la plantilla (hechos, sin enlace, codificación, versión de texto), Mongo (las pruebas ida y vuelta; el evento va al outbox en la misma transacción; una versión vieja no deja evento) y la API de punta a punta contra un almacenamiento S3 real que **verifica las firmas** (subir, registrar, ver, borrar, abrir, un solo correo aunque corra el outbox varias veces; objeto de más de 1 MB rechazado y borrado; tipo distinto rechazado por la firma; propietario, otro fotógrafo, sin sesión, token de la app y origen ajeno).

## 12. La página de las pruebas (PR 4b, portal)

**Dónde:** `/studio/galleries/{id}` (la tarjeta de cada galería de `/studio` trae "Abrir galería"). La dirección se lee una vez; un id que no es un GUID es "no encontrado". Sin sesión, la misma página pide la contraseña y el código. Todo lo que se puede hacer sale de `allowedActions`: sin `UploadProofs` no hay selector, sin `RemoveProof` no hay "Quitar", sin `OpenSelection` no hay "Publicar pruebas".

**Cómo se hace una prueba (en el navegador, nunca en el servidor):** `media/proofRenderer.ts`. La foto se decodifica ya girada como la tenía la cámara (`createImageBitmap` con `imageOrientation: 'from-image'`), se reduce a **720 px de lado largo** (nunca se agranda), se le dibuja encima el **logo de la app** (`src/assets/proof-watermark.png`, el mismo del fondo de la app, blanco sobre transparente) **al centro, al 70 % del lado corto, con 60 % de opacidad y una sombra suave** (se ve sobre una foto clara y sobre una oscura, y recortar la foto no lo quita) y se codifica como JPEG con calidad **0,4**. Si pesa más de 1 MB baja a 0,3 y 0,2, y si aún pesa, reduce los lados un 25 % (hasta 3 veces). El codificador del canvas **no copia ningún metadato**: ni ubicación, ni cámara, ni fecha. Los valores están en `constants/proof.ts`. Se comprobó en un Chrome real con una foto de 6000×4000 (1,5 MB): quedó de 720×480 y unos 50 KB.

**Subida (`media/proofUploader.ts`):** en **lotes de 10**: se preparan (3 a la vez), se piden los lugares con el peso real de cada prueba, se suben directo al almacenamiento (4 a la vez, `PUT` con el `Content-Type` firmado, **sin cookies ni referrer**) y se registran. Un lote termina antes de que empiece el otro (en memoria hay pocas pruebas y los registros nunca corren a la vez). Una foto que falla no detiene a las demás; una petición que falla (la API, o sin conexión) **detiene la subida y deja el resto en espera**. Lo que falla se queda en la lista con su motivo y se reintenta con un botón. Si cerrar la página interrumpe un registro, la prueba ya subida queda sin registrar en el almacenamiento (un objeto huérfano, que no se ve en ninguna galería; se limpia en el PR 10).

**Antes de subir:** al elegir las fotos (solo JPEG; se dejan fuera con aviso las que no son JPEG, las repetidas y las que no caben en el límite de 150) se muestra **una muestra hecha con la primera foto** ("Así verá tu cliente las pruebas") y hay que confirmar. **Publicar:** "Publicar pruebas" pide confirmar y abre la selección (G2). El correo al cliente sale sin enlace (sección 11).

**Seguridad:** la política de contenido del sitio (`public/_headers`) ahora deja `connect-src` e `img-src` hacia `https://*.r2.cloudflarestorage.com` (y `blob:` para la muestra); las direcciones firmadas nunca se muestran, ni se escriben en un mensaje de error; la lista de pruebas se pide de nuevo antes de que sus direcciones venzan (4 min de 10); una sesión que termina (401) lleva al inicio de sesión. **Hace falta** que el CORS del bucket permita `PUT`/`GET`/`HEAD` con el encabezado `Content-Type` desde el origen del sitio (ya está para develop y `localhost:5173`).

**Pruebas (portal 341):** tamaños y colocación de la marca, selección de archivos, concurrencia, el motor con un canvas falso (calidad, reducción, cierre de la imagen, fallos), el subidor con una API falsa (lotes, fallos por foto, límite lleno, sin conexión, cancelación), la subida al almacenamiento y la página de punta a punta (ver, quitar, muestra, subir, fallo y reintento, publicar, 404, sin sesión).
