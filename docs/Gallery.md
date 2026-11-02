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
5. **Selección del cliente en el portal** (backend hecho en el PR 5a, `feature/gallery-selection-backend`, sección 13; la página del cliente es el PR 5b, hecho, sección 14): sesión por código, cuadrícula con URLs de vida corta, contador, leyendas, mensaje de extras, sin clic derecho; enviar (G3) y push al fotógrafo.
6. **Extras (decisión 2026-11-02, ver sección 15):** las fotos adicionales **no se cargan solas al saldo**; el estudio coordina su pago con el cliente. Ya no hay cargo automático en Billing. Queda, si se quiere, que el fotógrafo registre ese cobro a mano más adelante.
7. **Finales, descargas y entrega** (con la regla de las extras de la sección 16; partido en 7a backend, sección 17; 7b portal, sección 18; 7c ZIP manual y ficha de cada foto, sección 19): subida de finales, publicar (G6), ZIP en el worker, entrega con saldo cero (G7), descarga del cliente con URLs firmadas y avisos.
8. **App del fotógrafo:** tarjeta de galería en la reserva, cuadrícula de pruebas, selección con nombres de archivo, acciones (regalar fotos, abrir y reabrir selección, publicar), diálogo de copiar o WhatsApp para el enlace del cliente.
9. **Subida desde el celular:** selector de fotos, reducción, marca y compresión en el dispositivo (Skia), subida con progreso y reintento; **requiere un build nuevo de EAS**.
10. **Liberar espacio y archivo** (hecho, sección 27): Release a mano y archivado a los 90 días; queda pendiente la limpieza de objetos huérfanos.

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

## 13. La selección del cliente (PR 5a, backend)

**Lo que ve el cliente** (todo bajo `/public/bookings/{token}`, el mismo enlace de siempre):
- `GET /gallery/summary` (solo el enlace): `{ status }`. Es lo que le dice a la página si ofrecer "Ver galería". **404** si la reserva no tiene galería, si la galería todavía espera sus pruebas (`Pending`) o si es de otro fotógrafo: las tres respuestas son iguales. No dice nada de las fotos.
- `GET /gallery` (**exige la sesión de portal**, o sea el código por correo; las fotos son lo que el cliente todavía no pagó): `{ status, selectablePhotos, extraPhotoPrice, photos: [{ id, url, expiresAt }], selection }`. Cada `url` es una dirección firmada de **20 minutos** (el máximo del almacenamiento; la sesión de portal dura 30), hecha en cada petición y nunca guardada ni registrada. **No trae nombres de archivo.** La sesión de una reserva no abre la galería de otra (404), y el token del fotógrafo no abre nada de aquí (401).
- `POST /gallery/selection` (sesión de portal) con `{ photoIds }`: **G3**. La galería pasa de `InSelection` a `InEditing` y **la selección queda bloqueada**; solo el fotógrafo podrá reabrirla (G5, PR 8). Responde con la galería y la selección (`photoIds` en el orden elegido, `extraPhotos`, `extraTotal`). Enviar **la misma selección** otra vez responde igual y no guarda ni avisa de nuevo (un doble toque es seguro). Otra selección distinta después de enviar: 409 `gallery.invalid_state`. Fotos ninguna, repetidas, de otra galería o más de 150: 422 `gallery.invalid_selection`.

**Las extras:** son las fotos elegidas más allá de `includedPhotos + bonusPhotos`; cada una cuesta el precio **congelado en la galería** (₡3 000 por defecto). El servidor las cuenta (el cliente no manda ningún monto) y las guarda en el evento `SelectionSubmitted` (`SelectedPhotos`, `ExtraPhotos`, `ExtraTotal`, `Currency`). **Todavía no se cobran:** el cargo en la reserva es el PR 6, que consumirá ese evento. Como el outbox ya entregó los eventos de las selecciones enviadas antes de que exista ese consumidor, el PR 6 debe incluir un relleno idempotente que cree el cargo de cada galería con selección enviada y extras. En develop solo hay datos de prueba.

**Lo que recibe el fotógrafo:**
- **Aviso** (`NotificationType.SelectionSubmitted`): se programa con el evento, en la misma pasada del outbox llega a la bandeja y sale el **push** ("Selección recibida" / "María Pérez eligió 12 fotos (2 extra)."). La llave es la galería, así que un evento entregado dos veces es un aviso. Si cuando llega la hora la galería ya no tiene selección (el fotógrafo la reabrió), el aviso se descarta. Lleva `selectedPhotos` y `extraPhotos`.
- **App:** la bandeja y el mensaje de WhatsApp ("recibí tu selección de 12 fotos (2 extra)… ya empiezo con la edición") ya lo entienden. **La lista de avisos de la app ahora deja fuera, sin romperse, un tipo que no conoce**: antes un tipo nuevo hacía fallar toda la bandeja. Una app **vieja** (anterior a este PR) sí falla con el tipo nuevo, así que hay que actualizar el JavaScript de la app (no hace falta un build nuevo) antes de que un cliente envíe su primera selección.
- `GET /api/web/galleries/{id}/selection` (web del fotógrafo): `{ submittedAt, extraPhotos, extraTotal, photos: [{ id, name }] }` con **los nombres de los archivos originales** en el orden elegido, para buscarlos en Lightroom. Vacío mientras el cliente no envía. La galería de otro fotógrafo da 404. `GalleryResponse` suma `selectedPhotoCount`, `extraPhotoCount` y `selectionSubmittedAt`.

**Pruebas:** dominio (enviar, extras con el regalo, misma selección dos veces, selección distinta, estados que no la aceptan, fotos inválidas, términos bloqueados, restaurar), casos de uso (resumen, galería, enviar, ajena, inválida, lista del fotógrafo), el aviso (se programa, se entrega con los conteos, se descarta, texto del push), Mongo (la selección y su evento en el outbox) y la API de punta a punta (del enlace al aviso, con la galería pendiente, la selección inválida y las sesiones que no se intercambian). Móvil: el aviso, el mensaje, y la lista tolerante.

## 14. La página de la galería del cliente (PR 5b, portal)

**Dónde:** en el mismo enlace de siempre, `/b/{token}`, una tarjeta **"Tus fotos"** entre el contrato y el comprobante (`GalleryFlow`). Aparece solo cuando `GET /gallery/summary` responde (hay una galería con las pruebas listas); en cualquier otro caso (404, sin conexión) **no se muestra nada**, y la reserva funciona igual que antes. Dice en palabras dónde está la galería (eligiendo, selección enviada, editada, entregada) y el botón correspondiente.

**Código por correo:** ver las fotos pide el mismo código de 6 dígitos que firmar el contrato o subir un comprobante (los pasos `CodeRequestStep` y `CodeEntryStep` son los mismos y la sesión se comparte en la página: quien ya confirmó el correo para otra cosa no lo repite). Si la verificación vence mientras elige, la página vuelve al código y **conserva lo elegido**.

**Elegir** (`GalleryPicker`):
- Arriba, la leyenda: *Estás viendo pruebas en calidad baja y con marca de agua. Nuestro compromiso es entregarte fotografías de excelente calidad.*
- **El contador baja con cada foto**: "Te quedan 12 de 15 fotos incluidas". Al llegar a cero: "Ya elegiste las 15 fotos de tu paquete" y el mensaje amable: *Las fotos adicionales cuestan ₡3 000 cada una. Si hay otra que te encante, ¡puedes sumarla!* El precio está siempre a la vista (mientras quedan fotos incluidas dice "Si eliges más, cada foto adicional cuesta ₡3 000"); con extras, la línea "2 fotos adicionales × ₡3 000 = ₡6 000. Se suma a tu saldo." Los números salen de `selectablePhotos` y `extraPhotoPrice` de la galería; el servidor los vuelve a contar al enviar y el cliente nunca manda un monto.
- Cuadrícula de 2 columnas en el teléfono y 3 en pantalla ancha; tocar una foto la elige o la quita (un ✓ y el texto "elegida" para lectores de pantalla: el color nunca es lo único que lo dice). "Ampliar" abre la foto grande (con anterior, siguiente, elegir/quitar; Escape cierra y las flechas del teclado mueven).
- **Revisar y enviar** → una pantalla que dice cuántas fotos son, cuánto cuestan las extras, que **se le avisará al fotógrafo cuáles fotos va a editar** y que **después de enviar no podrá cambiar su selección**. Solo "Sí, enviar mi selección" manda la petición. Las fotos van en el orden en que se eligieron.
- **Lo elegido se recuerda en la pestaña** (`sessionStorage`, solo identificadores de fotos, nada secreto): recargar o cerrar y volver a abrir la galería no lo pierde; al enviarlo se borra.
- Después de enviar: "¡Listo! Recibimos tu selección", y desde entonces (`GallerySentSummary`) solo las fotos elegidas, sin nada que cambiar, con lo que cuestan las extras.

**Contra copiar las pruebas** (capas débiles, dichas como tales): sin menú de clic derecho sobre las fotos, sin arrastrarlas ni seleccionarlas, y en el teléfono sin el menú de mantener presionado (la foto no recibe el toque, lo recibe el botón). **Una captura de pantalla no se puede impedir en una web**: lo que protege al fotógrafo es la calidad baja, la marca grande al centro y que el original nunca se sube.

**Lo que ve el fotógrafo en `/studio/galleries/{id}`:** cuando el cliente envía, la página muestra "Selección del cliente": cuántas fotos, cuántas son extra y cuánto cuestan, la hora en que la envió y **la lista de nombres de archivo en el orden elegido** con un botón **"Copiar los nombres de los archivos"** (uno por línea, para pegarlos en la búsqueda de Lightroom). Si el navegador no deja copiar, lo dice. Deja de ofrecer subir o quitar pruebas (la API ya no lo permite).

**Pruebas (portal 373, 32 nuevas):** la selección y sus números (singular y plural, paquete sin fotos), el borrador, el flujo de pasos, la tarjeta, los lectores de las respuestas, y la página de punta a punta: no muestra nada sin galería, pide el código antes de las fotos, el contador y el precio, la revisión antes de enviar con el orden elegido, el error al enviar, recordar lo elegido, volver al código con la sesión vencida sin perder lo elegido, la foto ampliada, la selección ya enviada solo de lectura, el menú del navegador bloqueado, el reintento al fallar la carga y la lista de nombres del fotógrafo con su copia.

## 15. Las fotos extra se acuerdan aparte (decisión 2026-11-02)

**Por qué:** el cliente paga el paquete al terminar la sesión, y la selección llega después (al día siguiente). Cuando elige, el paquete ya está pagado; sumar las extras al saldo por la fuerza no encaja con ese orden.

**Qué pasa ahora:**
- Si el cliente elige más fotos que las de su paquete, al pulsar "Sí, enviar mi selección" la página le dice: *Seleccionaste N fotos adicionales (monto). PhotoSense Studio va a contactarte para coordinar el pago.*
- **Aceptar y enviar:** manda la selección completa, extras incluidas.
- **Cancelar y omitir las adicionales:** no envía nada; deja solo las primeras fotos que eligió (tantas como incluye el paquete), las últimas quedan fuera, y vuelve a la revisión para que las envíe.
- El texto "Se suma a tu saldo" desapareció: ahora dice que PhotoSense Studio contactará al cliente.
- El servidor lo exige: `POST /gallery/selection` lleva `acceptExtras` (por defecto falso); con fotos de más y sin aceptarlas responde 422 `gallery.extras_not_accepted`. Dentro del paquete no hace falta.
- **El fotógrafo** sigue viendo cuántas son extra y cuánto suman (sección "Selección del cliente" y el aviso/push), y es quien coordina el cobro con el cliente. El saldo de la reserva **no** cambia.
- **PR 6 de Billing ya no existe como cargo automático:** el consumidor de `SelectionSubmitted` no crea cargos. El evento conserva `ExtraPhotos` y `ExtraTotal` para el aviso.

## 16. Entrega de las extras (decisión 2026-11-02, para el PR 7)

- **La entrega automática (G7) sigue igual:** se habilita solo con saldo cero. Las extras no entran al saldo, así que no la bloquean ni la retrasan: se entrega lo del paquete.
- **Las extras son un proceso manual.** Si el cliente las paga, el fotógrafo las edita y se las entrega; si no, entrega solo el paquete y no pasa nada. Si el cliente las pide después y paga, se entregan entonces.
- **Lo que debe hacer el PR 7:** al publicar los finales, el fotógrafo **marca cuáles corresponden a las extras**; esas quedan fuera de la descarga automática. Cuando el cliente paga, el fotógrafo las libera con una acción manual ("Entregar extras") y el cliente las ve en el mismo enlace. Si nunca se liberan, no se rompe nada.

## 17. Finales, publicar y entregar (PR 7a, backend)

**Qué hay:** el fotógrafo sube las fotos editadas (finales) de lo que el cliente eligió, las publica, y el cliente las descarga cuando el saldo es cero. Las extras se liberan a mano (sección 16).

**Reglas**
- Un final pertenece a una foto **elegida** (usa el mismo identificador que su prueba; la llave en R2 es `galleries/{galería}/finals/{foto}.jpg`). Solo JPEG, de 1 byte a **50 MB**, subido directo a R2 con dirección firmada (15 min), igual que las pruebas; la API revisa el objeto real al registrarlo y borra el que no sirve.
- **Paquete y extras:** las primeras fotos que el cliente eligió, tantas como cubre el paquete, son el paquete; el resto son extras (`Gallery.PackagePhotoIds` / `ExtraPhotoIds`, `IsExtra`). La selección del fotógrafo trae `isExtra` y `finalBytes` por foto.
- **Publicar (G6, `InEditing → Ready`):** exige el final de **cada foto del paquete**; las extras no hacen falta. Después de publicar los finales del paquete quedan congelados (el cliente puede estar descargando); los de las extras se pueden seguir subiendo y quitando. 409 `gallery.finals_incomplete` si falta alguno.
- **Entregar (G7, `Ready → Delivered`):** lo hace el sistema, nunca el fotógrafo. `GalleryDelivery` lo intenta cuando se publica (`FinalPhotosReady`) y con cada pago verificado (`PaymentVerified`, aunque pasen semanas): entrega solo si el saldo de la reserva es cero. La galería no sabe de dinero; solo `GalleryDelivery` junta las dos cosas. Idempotente.
- **Correo de entrega:** `GalleryDelivered` envía "Tus fotos ya están listas" al correo de la reserva, **sin enlace** (como el de las pruebas), una sola vez (`deliveryNotifiedAt`).
- **Descarga del cliente:** `GET /public/bookings/{token}/gallery` (sesión de portal) trae `finals` solo con la galería **entregada**: direcciones firmadas de **5 minutos** con el nombre original como nombre de archivo. Las extras salen solo si el fotógrafo las liberó (`extrasReleased`).
- **Liberar extras:** `POST /api/web/galleries/{id}/extras/release`, a mano, cuando el cliente ya pagó. Exige que al menos una extra tenga su final.

**Endpoints nuevos (web del fotógrafo, `/api/web/galleries/{id}`):** `POST finals/uploads` (lugares de subida: `{ files: [{ photoId, bytes }] }`), `POST finals` (registrar: `{ photoIds }`; devuelve la galería y lo rechazado con su motivo), `POST finals/publish`, `POST extras/release`. `GalleryResponse` suma `finalsPublishedAt`, `deliveredAt`, `extrasReleasedAt`; `GalleryAction` suma `UploadFinals`, `PublishFinals`, `ReleaseExtras`.

**Pruebas:** dominio 10 nuevas, aplicación 15 nuevas, y una de API de punta a punta (subir finales a R2, no poder publicar incompleto, publicar con saldo pendiente sin entregar, pagar, entrega automática, descarga real, extras ocultas hasta liberarlas).

**Pendiente de este PR:** la página del fotógrafo para subir finales (7b), la separación visual paquete/extras en ambas webs (7b), el ZIP en el worker (7c) y "Reabrir selección" (PR 8).

## 18. Finales y descargas en las webs (PR 7b, portal)

**Web del fotógrafo (`/studio/galleries/{id}`)**
- **Selección separada:** "Selección del cliente" ahora son dos bloques: **Fotos del paquete (N)** y **Fotos adicionales (M)** (este último con borde punteado y el total en colones, "se acuerdan con el cliente"). Cada bloque tiene su propio botón "Copiar los nombres de los archivos" y dice cuántas de sus fotos ya tienen final, con el peso de cada final subido. Las primeras fotos que el cliente eligió, tantas como cubre el paquete, son el paquete; el resto, extras (lo decide el servidor, `isExtra`).
- **Fotos finales** (sección nueva, aparece cuando el cliente envió la selección): "X de Y del paquete con su final". **Elegir las fotos finales** abre el selector de archivos (solo JPEG, hasta 50 MB); cada archivo se **empareja por nombre** con la foto elegida: `IMG_0001.jpg` e `IMG_0001-Edit.jpg` van con `IMG_0001.CR2` (sin importar mayúsculas ni extensión; el nombre exacto gana a uno que solo empieza igual, así `IMG_0001` no toma el archivo de `IMG_00010`). Antes de subir muestra los pares ("archivo → foto") y lo que se dejó fuera con su motivo (no coincide, repetido, no es JPEG, vacío, más de 50 MB). Sube de 5 en 5, directo a R2 (3 a la vez), y registra cada tanda; lo que falle queda en la cola con su motivo y se puede reintentar.
- **Publicar los finales del paquete:** botón con confirmación (después no se pueden cambiar). Si faltan finales, el servidor responde 409 y la página lo dice. El texto aclara que el cliente descarga de inmediato si no debe nada, o cuando se verifique su pago.
- **Entregar las adicionales:** botón con confirmación ("Sí, el cliente ya pagó"), visible cuando hay al menos una extra con final. Es manual a propósito (sección 16).
- La página muestra "Publicadas el …", "Entregada el …" y "Las fotos adicionales ya están disponibles" según el estado.

**Web del cliente (`/b/{token}`, "Tus fotos")**
- **Resumen de lo elegido:** "Fotos de tu paquete (N)" y "Fotos adicionales (M)" en cuadrículas separadas, con el aviso de que PhotoSense Studio contactará para coordinar el pago de las adicionales.
- **Entregada (saldo cero):** la tarjeta dice "Tus fotos están listas para descargar" y abre **las descargas**: cada foto con su nombre, peso y botón **Descargar** (una por una), una casilla por foto, **"Seleccionar todas (N, X MB)"** y **"Descargar seleccionadas (N, X MB)"**, que dice cuánto pesa lo marcado, avisa que el navegador puede pedir permiso para varias descargas y que con conexión lenta puede tardar. Las descargas van una tras otra con una pausa corta (el navegador descarta algunas si son todas a la vez), y la galería se **vuelve a pedir justo antes** de cada tanda para que las direcciones de 5 minutos estén frescas.
- **Extras:** aparecen en su propio grupo solo cuando el fotógrafo las liberó; mientras tanto la página dice que se coordinan con el estudio. Con la galería "lista" pero con saldo pendiente, el cliente ve que podrá descargar cuando su saldo esté en cero.
- **No hay ZIP todavía.** Ver 7c.

**Pruebas del portal (396, 23 nuevas):** emparejamiento de archivos, descargas en secuencia, parsers, el flujo completo de subir finales (pares, subida real simulada, rechazo, publicar con confirmación, error por faltantes, liberar extras) y las descargas del cliente (grupos, extras ocultas, "seleccionar todas", direcciones frescas, una por una).

## 19. PR 7c, lo que se pidió (definido el 2026-11-03; hecho, secciones 20 y 21)

- **ZIP manual:** desde la web del fotógrafo, subir un ZIP de **hasta 500 MB** (lo arma él; el servidor no crea ZIP, ni en el worker ni en el navegador). Una sola subida firmada a R2; solo se valida tipo y tamaño.
- **Botón del ZIP en la web del cliente** (solo si la galería tiene ZIP y está entregada), **con confirmación**: "Se va a descargar un paquete de X MB. Según tu conexión a internet puede tomar tiempo." Mismas reglas que los finales: saldo cero y dirección de 5 minutos.
- **El fotógrafo ve si el cliente lo descargó** y puede **borrar el ZIP** desde su web (y volver a subirlo si se lo piden de nuevo).
- **Ficha de cada foto** en la galería del fotógrafo: fecha en que se tomó, peso en MB, dimensiones (por ejemplo 1250 × 3000), **veces descargada** y un icono/botón de descarga. Pide leer los datos EXIF/dimensiones al subir (en el navegador del fotógrafo) y contar las descargas del cliente en el servidor.

## 20. ZIP manual, descargas contadas y ficha de cada foto (PR 7c, backend)

**Descargas contadas:** la galería del cliente (`GET /gallery`) **ya no trae direcciones** de los finales ni del ZIP, solo nombre, peso y si es extra (y el peso del ZIP). El cliente pide las direcciones al momento de descargar y **pedirlas cuenta como una descarga**:
- `POST /public/bookings/{token}/gallery/downloads` `{ photoIds }` → una dirección de 5 minutos por foto, con el nombre del archivo. Cuenta 1 por foto (una vez por petición aunque se repita). Si alguna foto no es descargable (galería no entregada, sin final, extra sin liberar, no elegida) falla **toda** la petición con 422 `gallery.invalid_photo` y no cuenta nada; 409 `gallery.invalid_state` si no está entregada. Dos descargas simultáneas no se pierden: ante una colisión se recarga la galería y se vuelve a anotar.
- `POST /public/bookings/{token}/gallery/zip/download` → dirección del ZIP (5 min), su peso, y suma 1 al contador con la fecha. 409 si no está entregada o no hay ZIP.
- Es una medida de "el cliente pidió la descarga", no de que el archivo haya llegado completo.

**ZIP hecho a mano (hasta 500 MB):** el servidor no arma ZIP. Web del fotógrafo: `POST /api/web/galleries/{id}/zip/upload` `{ bytes }` (dirección firmada, `application/zip`, solo con los finales publicados; 422 `gallery.invalid_zip` si pesa 0 o más de 500 MB), `POST .../zip` (registra tras mirar el objeto real; lo que no es un ZIP o pesa de más se borra, y si reemplazaba a otro, ese se olvida) y `POST .../zip/remove` (lo olvida y lo borra de R2). Hay un ZIP por galería (`galleries/{id}/zip/photos.zip`); subir otro de distinto tamaño lo reemplaza y reinicia su contador. `GalleryResponse.zip` trae `bytes`, `uploadedAt`, `downloads` y `lastDownloadedAt`. Acciones nuevas: `UploadZip`, `RemoveZip`.

**Ficha de cada final:** al registrar los finales (`POST .../finals`) el navegador del fotógrafo puede mandar `details: [{ photoId, width, height, capturedAt }]` leídos del archivo. Es información: un valor sin sentido (lado de 0 o de más de 100 000 píxeles, fecha anterior a 1990 o futura) se descarta en vez de rechazar la foto. La selección del fotógrafo trae por foto `finalWidth`, `finalHeight`, `finalCapturedAt` y `finalDownloads`. Un archivo de otro peso reemplaza al anterior y reinicia sus descargas.

**Descargar un final como fotógrafo:** `GET /api/web/galleries/{id}/finals/{photoId}/download` (dirección de 5 minutos; no cuenta como descarga del cliente).

**Pruebas:** dominio 11 nuevas, aplicación 12 nuevas y la de API de punta a punta ahora cubre: ficha de la foto, descarga contada del cliente (y rechazo de una extra sin liberar), descarga del fotógrafo sin contar, ZIP (límite, subida a R2, ver su peso, descargar, contador con fecha, borrar).

## 21. Las páginas del ZIP, las descargas contadas y la ficha de cada foto (PR 7c, portal)

**Web del fotógrafo (`/studio/galleries/{id}`)**
- **Ficha de cada foto elegida** (en los dos bloques, paquete y adicionales): `final subido · 1250 × 3000 px · 4,2 MB · tomada el 17 de octubre de 2026 · descargada 2 veces`, o `sin final`. Cada foto con final tiene un botón **⬇ Descargar** que baja ese final con una dirección de 5 minutos (no cuenta como descarga del cliente).
- **De dónde salen los datos:** el peso lo mide R2; el **tamaño en píxeles y la fecha en que se tomó los lee el navegador del fotógrafo** de los primeros 1 MB del archivo (cabecera del JPEG y bloque Exif; no se decodifica la foto ni se carga entera). Si la cámara giró la foto, se cambian los lados para dar el tamaño con que se ve. La cámara escribe la fecha sin zona horaria: se usa la que da el archivo si la trae, y si no **la de Costa Rica (UTC−6)**. Lo que no se pueda leer queda vacío y no impide subir la foto.
- **ZIP con las fotos (opcional)**, al final de "Fotos finales" cuando los finales están publicados: **Elegir el ZIP** (hasta 500 MB; si no es .zip o pesa más, lo dice sin llamar a la API), se sube directo a R2 en una sola petición (no hay barra de avance: la página avisa que puede tardar varios minutos y que no cierre la página), y se muestra su peso, cuándo se subió y **si el cliente lo descargó** ("todavía no lo ha descargado" o "N veces, la última el …"). **Borrar el ZIP** (con confirmación) lo quita de la galería y de R2 para subir otro cuando el cliente lo pida de nuevo; subir otro de distinto peso lo reemplaza y reinicia la cuenta. La página recuerda no incluir las adicionales si no las pagó.

**Web del cliente (`/b/{token}`, "Tus fotos")**
- **Las direcciones se piden al descargar.** Marcar casillas no pide nada; "Descargar" (una foto) o "Descargar seleccionadas" piden las direcciones de esas fotos en ese momento, y eso es lo que cuenta como descarga para el fotógrafo. Si algo falla, la página lo dice con palabras y no descarga nada; si la verificación venció, vuelve al código.
- **ZIP:** si el fotógrafo subió uno, aparece "Todas en un solo archivo" con el botón **Descargar todo en un ZIP (X MB)**. Al pulsarlo pide confirmar: *Se va a descargar un paquete de X MB. Según tu conexión a internet puede tomar tiempo.* con "Sí, descargar el ZIP" y "Cancelar". Sin ZIP no se menciona.

**Pruebas del portal (415):** lector de JPEG con archivos sintéticos (orden de bytes de las dos formas, fecha con y sin zona, giro de la cámara, archivo sin metadatos o cortado), validación del ZIP, parsers nuevos, subir el ZIP de punta a punta (con rechazo de lo que no es ZIP o pesa de más), mostrar y borrar el ZIP, la ficha de cada foto con su botón de descarga, y las descargas del cliente (direcciones pedidas solo al descargar, una por una, marcadas, error, ZIP con confirmación y sin ZIP).

## 22. Reabrir la selección (G5) y la galería desde la app (PR 8a, backend y web)

**Regla acordada para reabrir:** solo con la galería **en edición** (`InEditing`), es decir, antes de publicar los finales, porque después el cliente puede estar descargando. **Motivo obligatorio** (hasta 500 caracteres; es para tu registro y **no se manda al cliente**). Lo que el cliente eligió **se descarta**; la galería vuelve a `InSelection` y el cliente elige de nuevo, con el paquete y las extras recalculados. **No se borra nada**: los finales ya subidos quedan atados a su foto, así que si el cliente vuelve a elegir esa foto, su final ya está. Los finales de fotos que ya no se elijan se limpian en el PR 10.

- `POST /api/web/galleries/{id}/selection/reopen` y `POST /api/galleries/{id}/selection/reopen` con `{ reason }`. 409 `gallery.invalid_state` fuera de edición; 422 `booking.reason_required` sin motivo o con más de 500 caracteres. La acción `ReopenSelection` aparece en `allowedActions` solo en edición.
- **Correo al cliente** ("Tu selección está abierta otra vez"), **sin enlace y sin el motivo**, una vez por reapertura (`reopenNotifiedAt`; una reapertura posterior se avisa de nuevo). Evento `SelectionReopened`.
- `GalleryResponse` suma `selectionReopenedAt` y `selectionReopenReason`.
- **El aviso al fotógrafo "Selección recibida" ahora es uno por selección enviada**: su llave lleva la galería y el instante de la selección **en milisegundos** (es la precisión que guarda Mongo; con más, la llave armada desde el evento y la armada desde la galería guardada no coincidían y el aviso nunca llegaba). Sin esto, el segundo envío tras reabrir no avisaba, porque la llave era una por galería.
- **Web:** en "Fotos finales", mientras la galería está en edición, "Reabrir la selección" pide el motivo y confirmar.

**Rutas para la app (token del fotógrafo, `/api/galleries/{id}/…`)** con los mismos manejadores que la web, para operar desde el celular: `GET proofs`, `GET selection`, `POST proofs/{photoId}/remove`, `POST selection/open`, `POST selection/reopen`, `POST finals/publish`, `POST extras/release`, `POST zip/remove`, `GET finals/{photoId}/download` (más `terms` y la lectura, que ya existían). **Subir archivos no está aquí:** es el PR 9, porque necesita que el teléfono prepare las fotos.

**Pruebas:** dominio 11 nuevas, aplicación 6 nuevas, la de API recorre la app de punta a punta (leer, rechazos por token ajeno y sin token, reabrir sin motivo y con motivo, el cliente elige de nuevo, llegan **dos** avisos al fotógrafo y el correo al cliente), y 2 del portal (reabrir con motivo y confirmación, y el rechazo).

## 23. La galería final se ve como galería: miniaturas y la foto real (PR 8b)

**Decisión (2026-11-04):** la galería final tiene que verse con las fotos, **tal cual se editaron y con buena calidad**, no como una lista de nombres. Hasta el 7c las dos webs mostraban nombres con su ficha; ahora son cuadrículas de fotos.

**Dos imágenes por foto, cada una para lo suyo**
- **Miniatura** (para la cuadrícula): la foto reducida a 720 px de lado largo, calidad alta (JPEG 0,85 y baja solo si no cabe), **sin marca de agua**, hasta 400 KB. La hace **el navegador del fotógrafo al subir el final** y sube junto a él, en su propio lugar firmado (`galleries/{id}/thumbs/{foto}.jpg`). Es opcional: si no se puede hacer o el servidor la rechaza (no es JPEG o pesa de más; se borra), el final se registra igual. Un final de otro peso reemplaza la miniatura. Cargar los finales tal cual en una cuadrícula serían unos 220 MB solo para mirar.
- **Foto real** (al abrir una foto): el **final mismo**, el archivo editado, a pantalla completa, de a una por vez. Mientras llega se muestra la miniatura. Fotógrafo: `GET .../finals/{foto}/download` (5 min, sin contar). Cliente: `GET /public/bookings/{token}/gallery/finals/{foto}/view` (5 min; **no es una descarga y no se cuenta**; solo con la galería entregada, es decir con saldo cero, y una extra solo si se liberó; sin nombre de archivo, para mostrarse y no guardarse). Sin final, el fotógrafo ve la **prueba** (con marca de agua) como referencia.

**Web del fotógrafo:** la selección es una cuadrícula por bloque (paquete y adicionales) con la miniatura del final o, si aún no lo tiene, la prueba; sobre la foto una etiqueta "Final" o "Sin final"; debajo el nombre, la ficha y el botón ⬇ Descargar. Tocar una foto abre el visor con anterior, siguiente y Escape.

**Web del cliente:** las descargas son una cuadrícula con la miniatura de cada foto, su casilla, "Descargar" y el visor con la foto real. Las direcciones de las miniaturas y de la vista no cuentan como descargas.

**API:** `POST finals/uploads` devuelve por foto también `thumbUrl` y `thumbHeaders`; `GET selection` trae `thumbUrl` y `thumbIsFinal` por foto; la galería del cliente trae `thumbUrl` por final. Máximo de miniatura: 400 KB.

**Pendiente:** hacer la miniatura desde el celular al subir finales (PR 9). La app del celular usa esta misma cuadrícula (PR 8c).

**Pruebas:** backend dominio +2, aplicación +6 y la de API de punta a punta (sube final y miniatura a R2, el cliente descarga la miniatura y mira el final real sin que cuente, una extra sin liberar se rechaza); portal 11 nuevas (renderer de miniaturas, visor, cuadrículas, mirar el final real).

## 24. La galería en la app del celular (PR 8c)

**Criterio acordado (2026-11-04):** el trabajo pesado se hace en la computadora, pero todo lo que se puede hacer en la web de `/studio` debe poder hacerse también en el celular, para no depender de tener la computadora a mano. Subir archivos (pruebas, finales, ZIP) es lo único que queda para el PR 9, porque el teléfono tiene que preparar las fotos.

**Dónde está:** en el detalle de una reserva **completada**, la tarjeta **"Galería de fotos"** (estado en una píldora, qué sigue, fotos incluidas y precio de la extra, cuántas fotos eligió el cliente) con **"Abrir la galería"**, que lleva a la pantalla de la galería (`galleries/[id]`).

**La pantalla de la galería** (con las rutas `/api/galleries/{id}/…` y el token de la app, las mismas reglas que la web)
- **Acciones** (solo las que dice `allowedActions`; las que cambian algo piden confirmar con un texto que explica qué pasa): publicar las pruebas, publicar los finales del paquete, entregar las adicionales ("¿el cliente ya pagó?"), **reabrir la selección** (pantalla aparte que pide el motivo, obligatorio, que el cliente no ve) y **fotos incluidas, de regalo y precio de la extra** (pantalla aparte; es la forma de regalar fotos).
- **Pruebas:** se cargan solo al pedirlas ("Ver las pruebas"), porque son muchas imágenes y el teléfono puede estar con datos; mantener presionada una prueba la quita (con confirmación) mientras se pueda.
- **Selección del cliente** en dos bloques, **paquete** y **adicionales**: cuadrícula de fotos de 2 columnas (la miniatura del final o, si aún no lo tiene, la prueba), con la etiqueta "Final / Sin final", el nombre, la ficha (`final subido · 1250 × 3000 px · 4,2 MB · tomada el … · descargada 2 veces`) y **⬇ Descargar**, que abre la dirección del final en el navegador del teléfono. **Copiar o compartir los nombres** de cada bloque abre el menú de compartir del sistema (copiar, o mandarlos a otra app), uno por línea.
- **Foto real:** al tocar una foto se abre a pantalla completa **el final tal cual se editó** (con anterior y siguiente), de a una por vez; mientras llega se ve la miniatura, y si no se puede cargar lo dice y deja la miniatura.
- **ZIP:** su peso y si el cliente lo descargó (cuántas veces y cuándo), y **Borrar el ZIP**.

**Enlace del cliente:** la tarjeta de la reserva ahora ofrece, además de WhatsApp, **"Copiar o enviar por otra app"**, que crea el enlace y abre el menú de compartir del sistema (misma confirmación: el enlace anterior deja de funcionar).

**Sin módulos nativos nuevos:** compartir usa `Share` de React Native y las descargas `Linking`, así que llega por actualización de JavaScript, sin un build nuevo de EAS.

**Pruebas:** app 43 nuevas (921 en total): lectura de las respuestas, rutas llamadas, textos de estado, ficha y validaciones, las acciones con su confirmación, las pantallas (tarjeta, selección en dos bloques, visor de la foto real, cuadrícula de pruebas, ZIP) y las dos opciones del enlace. El paquete de Android se exporta sin errores.

**Subir archivos desde el celular:** ver la sección 26 (PR 9).

## 25. Las fotos como tarjetas (2026-11-04)

**Qué pasaba:** las fotos de la galería (descargas del cliente y selección del fotógrafo) eran un cuadro sin imagen con una casilla y un botón ancho, y se veían desordenadas. Un final subido **antes** de que existieran las miniaturas no tiene ninguna, y la tarjeta dejaba un hueco vacío. La app del celular tenía lo mismo.

**Ahora, igual en la web del cliente, la del fotógrafo y la app:** cada foto es una **tarjeta**: la imagen arriba (tocarla abre la foto real), el nombre y la ficha **debajo de la imagen**, y el **ícono de descarga dentro de la misma tarjeta** (círculo en el azul de la marca). En la web del cliente la casilla para elegir va sobre la esquina de la imagen; en la del fotógrafo y en la app, la etiqueta "Final / Sin final".
- **Ficha en cuatro líneas, cada dato en la suya** (2026-11-04): `1250 x 3000 px`, `Tamaño: 4.2 MB`, `Tomada: 17/10/2026`, `Descargas hechas: 2`. **Lo que no se sabe sale como `-`** (por ejemplo `Tamaño: -`), así las cuatro líneas siempre están y las tarjetas quedan alineadas. El nombre va arriba, **más pequeño y en negrita**. El peso usa punto decimal. En la web del cliente la tarjeta solo muestra el nombre y `Tamaño:` (lo demás es del fotógrafo).
- **Nunca un hueco:** si una foto no tiene miniatura, la tarjeta muestra la **prueba** (con marca de agua; el cliente la tiene de todas sus fotos) mientras tanto; si no hay ninguna imagen, o falla al cargar, muestra un **ícono de foto** y la tarjeta conserva su tamaño, así la cuadrícula no salta. Las miniaturas reales aparecen en los finales que se suban desde ahora.
- El nombre ocupa hasta dos líneas (no se corta con "…" a media palabra) y el ícono queda arriba junto a él.
- Colores solo de los tokens de la página (fondo de página sobre la superficie de la sección, texto y marca); sin romper la apariencia actual.
- Una sola pieza compartida en la web (`PhotoCard`) para las dos páginas; en la app, `GalleryPhotoTile`.

**Pendiente:** los finales ya publicados antes de las miniaturas siguen sin la suya (se ven con la prueba). Una opción es una acción "generar miniaturas faltantes" desde la web del fotógrafo, para que el cliente vea la foto final y no la de prueba.

## 26. Subir pruebas, finales y ZIP desde el celular (PR 9)

**Qué hay:** todo lo que la web de `/studio` sube, ahora también desde el detalle de la galería en la app. Los bytes **nunca pasan por la API**: van directo a R2 con las direcciones firmadas de siempre.

**Backend:** seis rutas nuevas bajo `/api/galleries/{id}` con el token de la app y los **mismos manejadores** que la web (`StudioEndpoints`): `POST proofs/uploads`, `POST proofs`, `POST finals/uploads`, `POST finals`, `POST zip/upload`, `POST zip`. Mismo límite (60 por minuto por IP). Una galería ajena responde 404 y sin token 401.

**En el celular (`mobile/src/media/`):**
- **Pruebas:** el selector del sistema (sin pedir permisos) → la foto se reduce con la librería nativa (`expo-image-manipulator`, ya girada como la tenía la cámara) a 720 px de lado largo → Skia dibuja el logo (70 % del lado corto, 60 % de opacidad, sombra suave: la misma especificación que `portal/src/media/proofRenderer.ts`) y codifica JPEG con calidad 40, 30 o 20 (si pesa más de 1 MB, baja la calidad y luego el tamaño). El JPEG de Skia no lleva metadatos. Antes de subir se muestra **una muestra con la primera foto** y hay que confirmar. Lotes de 10 (2 preparadas a la vez, 3 subidas a la vez).
- **Finales:** se suben **tal cual** (hasta 50 MB), emparejados por nombre con lo que eligió el cliente (`IMG_0001-Edit.jpg` va con `IMG_0001`; mismo algoritmo que la web). La miniatura se hace con la librería nativa (720 px, calidad 0,85 a 0,55, máx. 400 KB). Se envían ancho, alto y fecha de la foto (EXIF; sin zona horaria se asume Costa Rica).
- **ZIP:** selector de archivos, hasta 500 MB, se envía desde el archivo (no se lee a memoria) con barra de progreso.
- **Reglas:** la app debe quedar abierta mientras sube; una foto que falla se queda en la lista con su motivo y se reintenta; una petición que falla detiene la subida y deja el resto en espera. Los archivos temporales (pruebas hechas, copias del selector) viven en la caché de la app y se borran al terminar; **solo se borra lo que está en la caché de la app**, nunca la fototeca.

**Requiere un build nuevo de EAS** (Skia, selector de fotos y de archivos, manipulador de imágenes).

**Por verificar en un teléfono real:** que el selector entregue el JPEG de los finales sin recomprimir, que las dimensiones de los finales salgan con la orientación correcta, el aspecto de la marca de agua frente a la de la web, y la memoria con fotos de muchos megapíxeles.

**Pruebas:** backend, una de API (subir pruebas con el token de la app, rechazos por token ajeno y sin token); app 12 nuevas (tamaños, selección, emparejado, fecha, renderizador, los tres subidores).

## 27. Liberar espacio y archivo (PR 10, G8)

**Qué hace:** una galería **entregada** puede archivarse: se borran de R2 las pruebas, las miniaturas, los finales y el ZIP, y la galería pasa a `Archived` (estado final; el cliente ya no puede descargar). Es **irreversible**.

**Dos caminos, el mismo código (`GalleryArchiver`):**
- **A mano (Release):** `POST /api/galleries/{id}/release` (app) y `POST /api/web/galleries/{id}/release` (web). Botón "Liberar espacio" con confirmación ("¿Ya descargaste las fotos?"). Acción `Release` en `allowedActions`, solo con la galería entregada. 409 `gallery.invalid_state` en cualquier otro estado.
- **Aviso a los 90 días (decisión 2026-11-04):** la pasada de mantenimiento avisa **una sola vez por galería** (bandeja y push, tipo `GalleryArchiveDue`, clave `GalleryArchiveDue:{galleryId}` que deduplica) cuando lleva 90 días entregada (`Gallery.ArchiveNoticeDays`). El aviso abre la reserva y de ahí la galería, donde tú decides con "Liberar espacio". No se vuelve a avisar; si la liberas antes de que se entregue el aviso, este se descarta.
- **Respaldo automático (G8):** las que nadie liberó se archivan solas a los **180 días** (`Gallery.RetentionDays`, constante del dominio, hasta 20 por lote y 10 lotes por pasada). `MaintenanceResponse.galleriesArchived` dice cuántas.

**Orden de seguridad:** primero se borran los archivos y después se guarda el estado. Si R2 falla, la galería sigue entregada y se reintenta en la siguiente pasada (la pasada la cuenta como omitida); si el guardado falla después de borrar, el siguiente intento termina el trabajo (borrar lo que ya no existe no es error). Lo contrario dejaría una galería "archivada" con archivos ocupando espacio.

**Requisito operativo:** el **worker ahora registra el almacenamiento** (`AddObjectStorage`), así que necesita las variables `R2_ACCOUNT_ID`, `R2_BUCKET`, `R2_ACCESS_KEY_ID` y `R2_SECRET_ACCESS_KEY` igual que la API. Sin ellas el archivado por tiempo se omite sin tumbar el proceso (la API sigue funcionando; Release responde 503 `storage.unavailable`).

**Pendiente:** limpiar objetos huérfanos en R2 (subidas interrumpidas antes de registrarse): hace falta listar el bucket por prefijo (`galleries/{id}/`) y compararlo con lo registrado; queda para un PR aparte porque el puerto de almacenamiento no lista objetos. Los plazos (90 y 180 días) aún no son configurables por galería ni por entorno. La pantalla **"Galerías por archivar"** (menú lateral de la app, `app/archivable.tsx`) lista las galerías entregadas, la más antigua primero, con cuántos días llevan; cada una trae "Abrir la galería" y "Liberar espacio" (con la misma confirmación), así puedes liberar antes de tiempo con tu respaldo en la computadora. La alimenta `GET /api/galleries/archivable` (hasta 100, sin paginar).

**Pruebas:** dominio 6 nuevas (archivar una vez, estados que no se pueden archivar, cuenta de los 90 días), aplicación 3 nuevas (borra todo y luego archiva, rechaza lo no entregado y lo ajeno, un fallo del almacenamiento no archiva) y la pasada de mantenimiento sigue verde. Suite completa del backend en contenedor: 1 186 pruebas.

## 28. Galerías por archivar en la web, con descarga a una carpeta (PR 11)

**Dónde:** `/studio/archive` (enlace "Galerías por archivar" en `/studio`). Lista las galerías entregadas, la más antigua primero (`GET /api/web/galleries/archivable`, el mismo manejador que la app). Cada tarjeta trae "Descargar", "Abrir la galería" y, cuando corresponde, el panel "Liberar espacio".

**Descargar a una carpeta (`media/folderDownload.ts`, `media/browserFolder.ts`):**
- **Chrome y Edge:** el navegador deja elegir una carpeta (File System Access API, con el permiso de escritura que pide el propio navegador). Se bajan **solo los finales**, de uno en uno (nunca más de una foto en memoria), cada uno desde su dirección firmada de 5 minutos (`GET .../finals/{id}/download`, que no cuenta como descarga del cliente), y se compara el **tamaño recibido con el que registró la API al subirlo**. Una foto que llega con otro tamaño, o que no se pudo recibir o escribir, se reporta y no se da por buena; las demás siguen. Dos finales con el mismo nombre no se pisan (`foto.jpg`, `foto-2.jpg`).
- **Firefox y Safari:** no existe ese selector; las descargas se inician una por una (con pausa), y la página avisa que **no puede comprobar** que quedaron completas.

**Cuándo se ofrece "Liberar espacio"** (el panel de siempre, con su confirmación): solo cuando la descarga a la carpeta terminó **sin ningún fallo**, cuando se iniciaron las descargas en un navegador sin carpeta, o cuando el fotógrafo marca "Ya tengo estas fotos respaldadas en mi computadora". Nunca antes. La página no puede saber si los archivos quedaron bien en el disco, solo que llegaron completos al navegador: por eso la confirmación final sigue siendo del fotógrafo.

**Costo:** la salida de datos de R2 es gratis. Una galería Premium son ~300 MB de finales; una de 150 finales de 50 MB serían ~7 GB (se baja de a una foto y se puede repetir lo que falló).

**Pendiente:** el CORS del bucket de R2 debe permitir `GET` desde el origen del sitio (ya lo permite para develop y `localhost:5173`; en producción, al configurarla). No hay descarga en bloque en la app del celular.

**Pruebas:** backend, una de API (la lista solo para el navegador con sesión); portal 10 nuevas (guardado correcto, tamaño distinto, fallo de recepción, nombres repetidos, cancelación, la ruta, y la tarjeta en sus cinco estados).
