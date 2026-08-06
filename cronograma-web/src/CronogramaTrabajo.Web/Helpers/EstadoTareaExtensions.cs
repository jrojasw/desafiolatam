using CronogramaTrabajo.Web.Models;

namespace CronogramaTrabajo.Web.Helpers;

public static class EstadoTareaExtensions
{
    public static string TextoAmigable(this EstadoTarea estado) => estado switch
    {
        EstadoTarea.Pendiente => "Pendiente",
        EstadoTarea.EnProgreso => "En progreso",
        EstadoTarea.Completada => "Completada",
        EstadoTarea.Atrasada => "Atrasada",
        EstadoTarea.Cancelada => "Cancelada",
        _ => estado.ToString()
    };

    public static string ClaseBadge(this EstadoTarea estado) => estado switch
    {
        EstadoTarea.Pendiente => "bg-secondary",
        EstadoTarea.EnProgreso => "bg-primary",
        EstadoTarea.Completada => "bg-success",
        EstadoTarea.Atrasada => "bg-danger",
        EstadoTarea.Cancelada => "bg-dark",
        _ => "bg-secondary"
    };

    public static string TextoAmigable(this Prioridad prioridad) => prioridad switch
    {
        Prioridad.Baja => "Baja",
        Prioridad.Media => "Media",
        Prioridad.Alta => "Alta",
        _ => prioridad.ToString()
    };

    public static string ClaseBadgePrioridad(this Prioridad prioridad) => prioridad switch
    {
        Prioridad.Baja => "bg-light text-dark border",
        Prioridad.Media => "bg-info text-dark",
        Prioridad.Alta => "bg-warning text-dark",
        _ => "bg-light text-dark border"
    };
}
