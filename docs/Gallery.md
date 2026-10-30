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
3. **Sesión web del fotógrafo** (backend hecho en `feature/gallery-web-session`; las páginas del portal son el PR 3b; detalle en `docs/Authentication.md` §21): cookie, alcance limitado, CORS, página de inicio de sesión y lista de galerías.
4. **Subida de pruebas (web):** URLs firmadas, registro de la foto, módulo de marca de agua y compresión, página de subida y "Publicar pruebas" (G2) con el correo al cliente.
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
