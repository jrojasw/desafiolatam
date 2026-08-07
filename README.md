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
- **Validación automática del certificado** (`SuperSaludCertificateValidationService`): consulta
  `emisorcertificados.superdesalud.gob.cl/ValidacionCertificados/?id=...` y, si el certificado se subió como
  imagen, decodifica su código QR (ZXing.Net) y lo contrasta con el código ingresado. Si el resultado es
  ambiguo, el profesional queda `PendingVerification` para revisión manual en el panel — nunca se rechaza
  solo, ni se publica solo, de forma automática cuando hay dudas.

  ⚠️ **Importante**: la red de este entorno de desarrollo bloquea la salida hacia
  `superdesalud.gob.cl`, así que los patrones de texto usados para interpretar la respuesta HTML
  (`ValidKeywords` / `InvalidKeywords` en `SuperSaludCertificateValidationService.cs`) no pudieron
  verificarse contra una respuesta real. Antes de confiar en la aprobación 100% automática en producción,
  pruébalo con un certificado real y ajusta esos patrones si es necesario.
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
| `Smtp` | Credenciales del servidor SMTP real. Con `DryRun: true` (default) los correos solo quedan en el log, útil para probar sin servidor de correo. |
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

## Pendiente antes de producción

- Verificar/ajustar el scraping de `SuperSaludCertificateValidationService` contra una respuesta real del
  validador de la Superintendencia de Salud (ver nota arriba).
- Cargar credenciales reales de Transbank (producción) y la cuenta de servicio de Google Calendar.
- Configurar un proveedor SMTP real y desactivar `Smtp:DryRun`.
- Revisar la política de retención de los documentos privados (`App_Data/private-uploads`, fuera de
  `wwwroot`) según los requisitos legales que aplique CentralPsi.
