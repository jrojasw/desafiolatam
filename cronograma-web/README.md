# Cronograma de Trabajo

Aplicación web en ASP.NET Core MVC (.NET 8) para gestionar un cronograma de
trabajo: tareas con responsable, fechas de inicio/término, prioridad y
estado, más una vista de calendario.

## Características

- CRUD completo de tareas (crear, ver, editar, eliminar).
- Estados: Pendiente, En progreso, Completada, Atrasada, Cancelada.
- Prioridades: Baja, Media, Alta.
- Filtro del listado por estado.
- Vista de calendario mensual (FullCalendar, servido localmente desde
  `wwwroot`, sin dependencias externas) con eventos coloreados según su
  estado.
- Persistencia con Entity Framework Core y SQLite.

## Requisitos

- .NET 8 SDK

## Cómo ejecutar

```bash
cd src/CronogramaTrabajo.Web
dotnet run
```

La aplicación crea automáticamente la base de datos SQLite (`cronograma.db`)
y la puebla con datos de ejemplo la primera vez que se ejecuta.

Por defecto queda disponible en `http://localhost:5189` (ver
`Properties/launchSettings.json`).

- Listado: `/Tareas`
- Calendario: `/Tareas/Calendario`
- Nueva tarea: `/Tareas/Create`

## Estructura

```
src/CronogramaTrabajo.Web
├── Controllers/TareasController.cs   # CRUD + endpoint JSON para el calendario
├── Data/                             # DbContext y seed de datos
├── Models/                           # Tarea, EstadoTarea, Prioridad
├── Helpers/                          # Extensiones para mostrar estado/prioridad
├── Views/Tareas/                     # Index, Create, Edit, Details, Delete, Calendario
└── Migrations/                       # Migraciones de EF Core
```
