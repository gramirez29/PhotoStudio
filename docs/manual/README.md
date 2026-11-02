# PhotoSense Studio — Manual completo

Este manual está escrito para alguien que **no sabe nada** de cómo se hizo ni de cómo se usa PhotoSense Studio. Tiene dos partes que se pueden leer por separado.

- **Parte A — Técnica:** cómo está construido, qué se configuró en cada plataforma, cómo se prueba, se despliega y se opera.
- **Parte B — Funcional:** cómo se usa, de punta a punta, y qué reglas de negocio aplican.

Los documentos de `docs/*.md` (uno por módulo) son el detalle fino y la historia de las decisiones; este manual los resume, los ordena y los une. Si algo del manual y del código difieren, **manda el código**.

## Qué es PhotoSense Studio

Una plataforma para fotógrafos (retrato y eventos; mercado inicial Costa Rica) para administrar **reservas, contratos, cobros y entrega de galerías**. Su principio es evitar pérdida de dinero: clientes que no se presentan, saldos sin cobrar, entregas atrasadas. Tiene tres piezas:

| Pieza | Quién la usa | Qué es |
|---|---|---|
| **App móvil** | El fotógrafo | Aplicación de Android (iOS pendiente) hecha con Expo / React Native. Es lo único que se instala. |
| **Sitio web** | El cliente y el fotógrafo | Página principal, **portal del cliente** (un enlace por reserva, sin instalar nada) y **web del fotógrafo** (`/studio`, para subir fotos desde la computadora). |
| **API** | Las dos anteriores | Servicio .NET con MongoDB. Un worker aparte hace trabajos en segundo plano. |

## Mapa del manual

### Parte A — Técnica
| Documento | Contenido |
|---|---|
| [A1 — Arquitectura](A1-Arquitectura.md) | Repositorio, capas, patrones (agregados, outbox, concurrencia), tecnologías |
| [A2 — Backend por módulos](A2-Backend.md) | Cada módulo: qué hace, sus clases, sus endpoints, sus errores |
| [A3 — Datos](A3-Datos.md) | Colecciones de MongoDB, índices, retención, qué guarda R2 |
| [A4 — Infraestructura y despliegue](A4-Infraestructura.md) | Qué se configuró en Railway, Atlas, Cloudflare, Resend, Expo/Firebase, GitHub; variables; CI/CD; montar todo desde cero |
| [A5 — Seguridad y autenticación](A5-Seguridad.md) | Las cuatro formas de entrar, amenazas y defensas |
| [A6 — App móvil y sitio web](A6-Clientes.md) | Cómo están hechos los dos clientes, reglas de código, tema, push, subida de fotos |
| [A7 — Pruebas y operación](A7-Pruebas-y-operacion.md) | Cómo correr todo, qué cubren las pruebas, guía de problemas comunes |

### Parte B — Funcional
| Documento | Contenido |
|---|---|
| [B1 — Guía del fotógrafo](B1-Guia-del-fotografo.md) | Todo lo que se hace en la app y en la web, paso a paso |
| [B2 — Guía del cliente](B2-Guia-del-cliente.md) | Lo que ve y hace el cliente en su enlace |
| [B3 — Reglas de negocio](B3-Reglas-de-negocio.md) | Estados, dinero, políticas, avisos, correos, escenarios y glosario |

## Convenciones de este manual

- *Reserva* = una sesión fotográfica apartada para un cliente (en el código, `Booking`).
- *Galería* = las fotos de esa sesión, desde las pruebas hasta la entrega (`Gallery`).
- *Prueba* = foto reducida, de baja calidad y con marca de agua, que el cliente usa para elegir.
- *Final* = foto editada a calidad completa que se entrega.
- *Extra* = foto elegida por encima de las que cubre el paquete.
- Las rutas de archivos se escriben desde la raíz del repositorio.
- Lo marcado **Pendiente** no está construido todavía.

## Glosario rápido

| Término | Significado |
|---|---|
| **Tentativa** | Reserva creada, con el horario apartado por un tiempo limitado, sin contrato firmado y anticipo verificado todavía |
| **Anticipo** | Parte del precio que se paga para confirmar (por defecto 50 %) |
| **SINPE Móvil** | Transferencia bancaria costarricense; el cliente sube el comprobante y el fotógrafo lo verifica |
| **Outbox** | Tabla donde cada cambio deja sus eventos para que el worker los procese después, sin perderlos |
| **Worker** | Proceso aparte (mismo código que la API) que expira reservas, entrega avisos y archiva galerías |
| **OTP** | Código de 6 dígitos por correo que prueba que el cliente es dueño del correo registrado |
| **URL firmada** | Dirección temporal de R2 que permite subir o bajar un archivo sin pasar por la API |
| **EAS** | Servicio de Expo que compila la app móvil |
| **R2** | Almacenamiento de archivos de Cloudflare donde viven las fotos |
