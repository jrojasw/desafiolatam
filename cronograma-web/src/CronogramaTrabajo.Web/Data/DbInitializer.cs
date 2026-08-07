using CronogramaTrabajo.Web.Models;
using Microsoft.EntityFrameworkCore;

namespace CronogramaTrabajo.Web.Data;

public static class DbInitializer
{
    public static void Seed(CronogramaContext context)
    {
        context.Database.Migrate();

        if (context.Tareas.Any())
        {
            return;
        }

        var hoy = DateTime.Today;

        context.Tareas.AddRange(
            new Tarea
            {
                Titulo = "Levantamiento de requisitos",
                Descripcion = "Reunión con el cliente para definir el alcance del proyecto.",
                Responsable = "Jorge Rojas",
                FechaInicio = hoy.AddDays(-3),
                FechaFin = hoy.AddDays(-1),
                Estado = EstadoTarea.Completada,
                Prioridad = Prioridad.Alta
            },
            new Tarea
            {
                Titulo = "Diseño de base de datos",
                Descripcion = "Modelar entidades y relaciones del sistema.",
                Responsable = "Jorge Rojas",
                FechaInicio = hoy.AddDays(-1),
                FechaFin = hoy.AddDays(2),
                Estado = EstadoTarea.EnProgreso,
                Prioridad = Prioridad.Alta
            },
            new Tarea
            {
                Titulo = "Desarrollo del módulo de calendario",
                Descripcion = "Implementar la vista de calendario con los estados de cada tarea.",
                Responsable = "Equipo Desarrollo",
                FechaInicio = hoy.AddDays(1),
                FechaFin = hoy.AddDays(5),
                Estado = EstadoTarea.Pendiente,
                Prioridad = Prioridad.Media
            },
            new Tarea
            {
                Titulo = "Pruebas de usuario",
                Descripcion = "Ejecutar pruebas de aceptación con usuarios finales.",
                Responsable = "QA",
                FechaInicio = hoy.AddDays(6),
                FechaFin = hoy.AddDays(8),
                Estado = EstadoTarea.Pendiente,
                Prioridad = Prioridad.Media
            },
            new Tarea
            {
                Titulo = "Entrega de documentación",
                Descripcion = "Redactar manual de usuario y documentación técnica.",
                Responsable = "Jorge Rojas",
                FechaInicio = hoy.AddDays(-6),
                FechaFin = hoy.AddDays(-4),
                Estado = EstadoTarea.Atrasada,
                Prioridad = Prioridad.Baja
            }
        );

        context.SaveChanges();
    }
}
