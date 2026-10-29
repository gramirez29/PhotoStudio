# Portal web del cliente (plan)

Página que el cliente abre desde el enlace de su reserva. El backend ya existe (`docs/ClientPortal.md`); este documento
planifica la parte web. Se actualiza al terminar cada entrega.

## 1. Alcance de la v1
**Sí:** ver la reserva (fotógrafo, paquete, fecha, montos, estado de los pagos, vencimiento de la reserva tentativa), pedir y
escribir el código de 6 dígitos que llega al correo, enviar el comprobante SINPE con monto y referencia, y ver su estado.

**No (siguientes entregas):** galería, pago con tarjeta, reprogramar.

## 2. Estructura y tecnología
- Carpeta **`portal/`** en este monorepo, junto a `backend/` y `mobile/`.
- Vite + React 19 + TypeScript estricto (sin `any`) + ESLint 9. Pruebas con Vitest + Testing Library.
- Sin librería de UI: CSS plano con variables, mobile-first, inputs a 16 px (iOS no hace zoom).
- Diseño: tokens neutrales temporales (los mismos de mobile) hasta que el usuario entregue **fuentes y paleta de colores**; la
  marca se cambia en un solo archivo.
- TanStack Query para lectura y mutaciones; los pasos son una máquina de estados pequeña (`useReducer`).
- Cada respuesta del API se valida en tiempo de ejecución (misma idea que `mobile/src/api/guards.ts`); más adelante, tipos
  generados desde `/openapi/v1.json`.
- Textos en español; fechas en `es-CR` con zona `America/Costa_Rica`.

## 3. Pantallas (una sola ruta, `/b/:token`)
```
cargando ─▶ vista de la reserva ─▶ [Subir comprobante]
   │              │                      │
   │              │                      ▼
   │              │            pedir código ─▶ escribir código ─▶ formulario de comprobante ─▶ enviado
   ▼              ▼
enlace inválido   (sin correo: "Pídele a tu fotógrafo que agregue tu correo")
(404)
```
- **Código:** muestra el correo enmascarado que devuelve el API; `inputmode="numeric"` y `autocomplete="one-time-code"`;
  "Reenviar" con cuenta regresiva del `Retry-After` real.
- **Comprobante:** monto prellenado con el anticipo que falta (o el saldo) y sin pasar de lo que se debe; referencia opcional;
  `idempotencyKey` creada una vez al abrir el formulario.
- **Sesión de portal:** el token vive solo en memoria y dura 30 min; al expirar vuelve al paso del código sin perder lo escrito.
- **Errores con mensaje propio:** 404, 409 `portal.email_missing`, 429 (con la espera), 422 `portal.invalid_code`,
  422 `money.invalid_amount`, 503 `email.delivery_failed`, sin conexión.

## 4. Seguridad del lado del navegador
- `Referrer-Policy: no-referrer` y `X-Robots-Tag: noindex` (`_headers` de Pages): el enlace es la credencial.
- Open Graph genérico, sin datos de la reserva (vista previa de WhatsApp). Sin analíticas ni scripts de terceros.
- El token no se guarda en `localStorage` ni en logs.

## 5. Despliegue

Ver `docs/Deployment.md`: un solo sitio (página principal en `/` y portal en `/b/{token}`) en Cloudflare Pages, publicado desde GitHub Actions; ambiente `develop` en `develop.photosensestudio.com` con la API en `develop-api.photosensestudio.com`.

## 6. Entregas
| # | Contenido | Estado |
|---|---|---|
| 1 | CORS en el API para `/public/**` (`docs/ClientPortal.md` §5.1) | **Hecho** |
| 2 | Scaffold de `portal/` + vista de la reserva solo lectura + estados de error + configuración de Pages | **Hecho** (el proyecto de Pages `photosense-develop` ya existe y se publica desde Actions en `develop.photosensestudio.com`, protegido con Cloudflare Access; pendiente el de producción: pasos en `portal/README.md`) |
| 3 | Código por correo + formulario de comprobante + estados del pago | **Hecho** |
| 3.1 | Contrato: leer y firmar desde el enlace (cédula, personas, lugar, uso de imagen, nombre escrito como firma). Comparte la sesión del código con el comprobante | **Hecho** (§8) |
| 4 | Pulido: accesibilidad, pruebas de punta a punta contra el API local, lista de revisión de despliegue | Pendiente |

Después, fuera de este plan: nada pendiente aquí. El correo de una reserva existente se corrige con **Editar reserva** en la
app del fotógrafo, y la declaración del contrato ya se muestra ahí.

## 7. Marca aplicada (entrega 2)

Paleta del cliente en dos variaciones (la página sigue `prefers-color-scheme`) y tipografías **Josefin Sans** (títulos) y
**DM Sans** (texto), alojadas en el sitio con `@fontsource-variable` (sin pedidos a Google Fonts).

| Rol | Claro | Oscuro |
|---|---|---|
| Fondo | `#FBF3EA` | `#020D2C` |
| Superficie (tarjetas) | `#F6D5BE` | `#26324F` |
| Texto | `#0B1A40` | `#F4F1EC` |
| Texto secundario | `#3A4466` | `#C9CDD6` |
| Acento (botón, bordes) | `#C4552E` | `#E3A97F` |
| Texto sobre el acento | `#FBF3EA` | `#020D2C` |

Fuera de la paleta, solo los puntos de estado (éxito, aviso, error), siempre junto a un texto.

**Hallazgo de contraste:** el terracota claro sobre el crema da **4,08:1**, menos que el 4,5:1 de WCAG AA para texto normal. Se
mantuvo el color de la marca y se limitó su uso: relleno de botón con texto de 19 px en negrita (texto grande, exige 3:1) y
bordes; nunca como color de texto corrido. `src/theme/tokens.test.ts` lo hace cumplir. Si se quiere 4,5:1 también en el botón, hay
que oscurecer ese acento (por ejemplo a `#B04A25`).

## 8. Flujo del comprobante (entrega 3)

Máquina de estados pura en `src/utils/proofFlow.ts`: `cerrado → pedir código → escribir código → formulario → enviado`.

- **La verificación se reutiliza:** la sesión de portal (30 min) vive solo en memoria. Si el cliente reporta un segundo comprobante dentro de ese tiempo no se le pide otro código. Una sesión a menos de 1 minuto de vencer se trata como vencida para no hacerle llenar un formulario que será rechazado.
- **Si la verificación vence a mitad del formulario** (401), vuelve al paso del código con un aviso y **conserva lo que escribió**.
- **Monto:** se sugiere el anticipo que falta (o el saldo) y no puede superar `saldo − comprobantes en revisión`; así no se reporta dos veces el mismo dinero. Se aceptan separadores (`50 000`, `50.000`, `₡50,000`).
- **Idempotencia:** la clave se crea una vez por comprobante; un reintento tras un fallo o un doble toque envía la misma y se registra una sola vez. Se renueva al terminar.
- **Código:** solo dígitos, `autocomplete="one-time-code"`, reenvío con cuenta regresiva (60 s, o el `Retry-After` real si el servidor responde 429).
- **Qué se ofrece:** el botón solo aparece si la API permite el comprobante (`canSubmitProof`), la reserva tiene correo (`canReceiveCode`) y queda algo por reportar. Sin correo, un aviso pide al cliente que se lo dé a su fotógrafo.
- **Accesibilidad:** cada paso mueve el foco a su título, los errores se anuncian (`role="alert"`), los campos enlazan etiqueta, ayuda y error, y el mensaje de error va en el color del texto (el rojo solo lo marca).

## 8. Contrato (entrega 3.1)

El backend está descrito en `docs/ClientPortal.md` §2.1. En el portal:

- **Tarjeta "Contrato"** (`ContractFlow`) en la página de la reserva: dice si está firmado y ofrece "Leer y firmar contrato" (solo si la API manda `canSignContract`) o "Ver contrato firmado". Sin correo en la reserva dice que el fotógrafo debe registrarlo; el portal no decide por su cuenta qué permite el estado.
- **Pasos:** código por correo (los mismos `CodeRequestStep` y `CodeEntryStep`, ahora con el motivo en el texto) → contrato (`ContractReadStep`: el texto en una caja con scroll y foco de teclado, `ContractDocument`) → formulario (`ContractSignForm`) → "Firmaste tu contrato" (`ContractSignedStep`). Leer el contrato exige la sesión porque el texto lleva el teléfono y, firmado, la cédula.
- **Formulario:** nombre escrito (es la firma), cédula o DIMEX, número de personas, lugar (opcional), uso de imagen (dos opciones; **no hay una elegida por defecto**) y la casilla "He leído el contrato completo y lo acepto". `forms/contractForm.ts` repite las reglas del servidor para avisar junto al campo. Lo escrito se conserva si la verificación vence a la mitad.
- **Sesión compartida:** `BookingPage` guarda la sesión que sale de confirmar el código y se la pasa a `ContractFlow` y `ProofFlow`, así el cliente confirma su correo una vez y no una por acción. `ProofFlow` ya no guarda su propia sesión de entrada: el evento `open` la recibe.
- **Privacidad:** la consulta del contrato usa `gcTime: 0` y no se reintenta sola, para no conservar el texto en la caché cuando nada lo muestra.
- **Verificado** a mano contra la API local en un navegador (leer, validar, firmar, ver el contrato firmado, subir un comprobante sin segundo código) y con pruebas: formulario, reductor, errores, API y el flujo completo con `fetch` simulado.
