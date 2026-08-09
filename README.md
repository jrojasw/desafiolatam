# CentralPsi

Plataforma de agendamiento online de horas psicológicas. Los psicólogos se inscriben gratis, validan su
certificado del Ministerio de Salud, y quedan publicados en **Nuestros profesionales** para que pacientes
agenden y paguen su sesión ($29.750 CLP) online.

Construida en **ASP.NET Core 8 MVC** (monolito) + **PostgreSQL** + **Entity Framework Core**.

El código legado de este repositorio (un portafolio estático anterior) fue movido a [`legacy/`](legacy/) y no
se toca.

## Estructura

```
src/CentralPsi.Web/
  Areas/Admin/          Panel de administración (login, slider, noticias, profesionales)
  Controllers/          Sitio público: Home, Professionals, Booking, InternalCalendar
  Data/                 DbContext, migraciones EF Core, seed inicial
  Models/Entities/      Entidades del dominio
  Models/ViewModels/    View models del sitio público
  Options/              Clases de configuración (Transbank, Google, SMTP, App, SuperSalud)
  Services/             Lógica de negocio (pagos, correo, disponibilidad, validación de certificados, Google Meet)
  Views/                Razor views
  wwwroot/               Estáticos (css, js, imágenes de ejemplo del slider/noticias)
```

## Funcionalidades implementadas

- **Inscripción de profesionales** (`/profesionales/inscripcion`): datos, cédula (frente/reverso), certificado
  Minsal, código de validación y horario semanal de atención.
- **Validación automática del certificado** (`SuperSaludCertificateValidationService`): abre
  `emisorcertificados.superdesalud.gob.cl/ValidacionCertificados/?id=...` con un navegador Chromium sin
  interfaz (Playwright), ya que esa página arma el resultado ("Estado del certificado: VIGENTE") con
  JavaScript y una verificación reCAPTCHA v3 en segundo plano - una simple petición HTTP nunca ve ese texto.
  Si además el certificado se subió como imagen, decodifica su código QR (ZXing.Net) y lo contrasta con el
  código ingresado. Si el resultado es ambiguo, el profesional queda `PendingVerification` para revisión
  manual en el panel (con un botón "Reintentar validación automática") — nunca se rechaza ni se publica solo
  de forma automática cuando hay dudas.

  ⚠️ **Sobre el uso de un navegador para esto**: no es un intento de "saltarse" reCAPTCHA - Chromium ejecuta
  la página igual que lo haría cualquier persona verificando un certificado (el uso público normal de esa
  herramienta), sin resolver desafíos ni usar tokens falsos. En la práctica (probado en producción el
  2026-08-08/09 contra un certificado real), el sitio detecta que la sesión es automatizada y la consulta
  siempre vuelve "Inconcluso" (nunca confirma VIGENTE), muy probablemente porque reCAPTCHA v3 le asigna un
  puntaje bajo a un navegador sin interacción humana real. **Se decidió, a propósito, no intentar disfrazar el
  navegador** (cambiar el User-Agent, ocultar la marca `navigator.webdriver`, etc.) para evadir esa detección:
  aunque técnicamente no sería "hackear" nada, sí sería evadir activamente un control de seguridad de un sitio
  del Estado, y no vale la pena el riesgo/ambigüedad legal para una función que ya tiene un respaldo sólido (el
  QR automático + aprobación manual con un clic). **La vía correcta a futuro** es solicitar a la
  Superintendencia de Salud / Minsal un acceso oficial (API o convenio) para validar certificados de forma
  programática - vale la pena gestionarlo si el volumen de inscripciones crece.

  ⚠️ **Costo en recursos**: esto requiere una imagen Docker más pesada (incluye Chromium) y puede acercarse al
  límite de RAM del plan gratuito de Render (512 MB) mientras se ejecuta una validación. Si ves que las
  validaciones fallan por falta de memoria, el siguiente paso es subir el Web Service al plan pago más básico
  de Render (más RAM).
- **Listado público de profesionales** (`/profesionales`) y **ficha + agenda** (`/profesionales/{id}`) con
  selector de horarios disponibles (calculados a partir del horario semanal menos las horas ya tomadas).
- **Reserva + pago** (`/reserva/...`): datos del paciente, aceptación obligatoria de términos y condiciones,
  pago con **Webpay Plus (Transbank)** en modo integración (sandbox público, sin credenciales) listo para
  cambiar a producción con `Transbank:CommerceCode` / `Transbank:ApiKey`.
- **Confirmación por correo + Google Meet**: al aprobarse el pago se genera un evento de Google Calendar con
  Google Meet (requiere una cuenta de servicio con delegación de dominio, ver abajo) y se notifica a paciente
  y profesional.
- **Respaldo de la sesión realizada**: al pasar la hora agendada, un job en segundo plano
  (`AttendanceConfirmationBackgroundService`) envía a paciente y profesional un enlace de un clic para
  confirmar si la sesión se realizó. Queda registrado como respaldo administrativo del pago.
- **Cancelación y reembolso**: enlace de cancelación por cita (`/reserva/cancelar/{token}`), cálculo automático
  del tramo de reembolso (100% ≥12h, 50% entre 12h y 0h, revisión manual después) y aviso por correo a
  `reembolsos@centralpsi.cl` con todos los datos para procesar la devolución manualmente.
- **Panel de administración** (`/Admin`, login propio con ASP.NET Core Identity): resumen, gestión de
  profesionales (aprobar/rechazar/desactivar/eliminar, ver documentos), slider de inicio (CRUD) y noticias
  (CRUD). Incluye recuperación de contraseña por correo y cambio de contraseña.
- **Calendario interno** (`/panel-interno/calendario`): página aparte, sin enlaces desde el sitio público ni
  desde el panel, protegida con el mismo login de administrador. Lista todas las citas con profesional,
  paciente y estado de pago.

## Configuración (`appsettings.json`)

| Sección | Qué hacer |
|---|---|
| `ConnectionStrings:DefaultConnection` | Cadena de conexión a PostgreSQL |
| `Transbank` | `Environment: "Integration"` funciona sin credenciales (sandbox público de Transbank). Cambiar a `"Production"` y completar `CommerceCode`/`ApiKey` una vez que Transbank los emita. |
| `GoogleCalendar` | `Enabled: true`, `ServiceAccountJsonPath` apuntando al JSON de una cuenta de servicio de Google Cloud con **delegación de dominio en todo el dominio** habilitada (scope `https://www.googleapis.com/auth/calendar`), e `ImpersonateUser` con un correo del Workspace (ej. `agenda@centralpsi.cl`) que actuará de organizador. Sin esto, las citas se confirman igual pero sin enlace de Meet (se informa por correo que llegará luego). |
| `Smtp` | Credenciales del servidor SMTP real. Con `DryRun: true` (default) los correos solo quedan en el log, útil para probar sin servidor de correo. El puerto define el tipo de conexión segura automáticamente (465 = TLS implícito, 587 = STARTTLS). |
| `App` | Precio, duración de sesión, zona horaria, correos de administración/reembolsos, URL pública del sitio. |
| `Seed:AdminEmail` / `Seed:AdminPassword` | Usuario administrador creado al iniciar por primera vez. **Cámbialo desde el panel apenas despliegues.** |

## Correr en local

```bash
# 1. Base de datos
createuser centralpsi --pwprompt   # o ajusta la cadena de conexión a tu Postgres
createdb centralpsi -O centralpsi

# 2. Migraciones (se aplican automáticamente al iniciar, pero puedes hacerlo a mano)
cd src/CentralPsi.Web
dotnet tool install --global dotnet-ef
dotnet ef database update

# 3. Ejecutar
dotnet run
```

El usuario administrador se crea automáticamente al primer arranque con los valores de `Seed:AdminEmail` /
`Seed:AdminPassword` (`admin@centralpsi.cl` / ver `appsettings.json` — cámbiala de inmediato).

## Despliegue actual y portabilidad a otra plataforma

El sitio está desplegado en **Render** (plan gratis) vía Docker: Web Service (`desafiolatam-1`) + base de
datos PostgreSQL administrada, ambos dentro del mismo "Environment" (`Centralpsi`) en Render.

El proyecto **no depende de nada exclusivo de Render** — es un contenedor Docker estándar + Postgres estándar,
así que se puede migrar más adelante a otra plataforma (Railway, Fly.io, DigitalOcean, un VPS propio, etc.)
sin reescribir nada del sistema. Migrar implica solo trabajo de configuración:

1. **Base de datos**: exportar con `pg_dump` desde la base de Render e importarla en la base nueva.
2. **Variables de entorno**: copiar las mismas (`DATABASE_URL` o `ConnectionStrings__DefaultConnection`,
   `Seed__AdminEmail`, `Seed__AdminPassword`, `Smtp__*`, `App__BaseUrl`, etc.) a la plataforma nueva.
3. **Repositorio**: conectar la plataforma nueva al mismo repo/rama; el `Dockerfile` de la raíz sirve tal cual
   (cualquier plataforma que soporte "Deploy from Dockerfile" lo va a reconocer).
4. **Dominio** (si compran uno propio): solo se apunta a la IP/URL de la plataforma nueva.

Notas si migran a una plataforma que **no** entregue una `DATABASE_URL` en formato `postgres://user:pass@host/db`
(Render sí lo hace): usar en su lugar la variable `ConnectionStrings__DefaultConnection` con el formato
Npgsql (`Host=...;Port=...;Database=...;Username=...;Password=...`), que el código soporta igual de bien
(`Program.cs` usa `DATABASE_URL` solo si existe, si no cae al formato de `ConnectionStrings`).

## Pendiente

- Solicitar acceso oficial a la Superintendencia de Salud/Minsal para validar certificados vía API (ver nota
  sobre reCAPTCHA arriba) - hoy la validación automática vía navegador headless casi siempre queda
  "Inconclusa" y pasa a revisión manual, lo cual funciona pero no es 100% automático.
- Cargar credenciales reales de Transbank (producción, hoy en modo integración/sandbox) y la cuenta de
  servicio de Google Calendar (hoy deshabilitada, `GoogleCalendar:Enabled = false`, así que las citas se
  confirman sin enlace de Meet automático).
- Revisar la política de retención de los documentos privados (`App_Data/private-uploads`, fuera de
  `wwwroot`) según los requisitos legales que aplique CentralPsi - en Render (free tier) ese disco es efímero
  y se pierde en cada redeploy/reinicio; si esto pasa a producción real, conviene un disco persistente de
  Render o migrar esos archivos a almacenamiento en la nube (S3, Cloudflare R2, etc.).
