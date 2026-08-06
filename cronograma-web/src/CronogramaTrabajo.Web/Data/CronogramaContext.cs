using CronogramaTrabajo.Web.Models;
using Microsoft.EntityFrameworkCore;

namespace CronogramaTrabajo.Web.Data;

public class CronogramaContext : DbContext
{
    public CronogramaContext(DbContextOptions<CronogramaContext> options) : base(options)
    {
    }

    public DbSet<Tarea> Tareas => Set<Tarea>();
}
