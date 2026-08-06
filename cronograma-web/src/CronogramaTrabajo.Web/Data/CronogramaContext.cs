using CronogramaTrabajo.Web.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace CronogramaTrabajo.Web.Data;

public class CronogramaContext : IdentityDbContext<ApplicationUser>
{
    public CronogramaContext(DbContextOptions<CronogramaContext> options) : base(options)
    {
    }

    public DbSet<Tarea> Tareas => Set<Tarea>();
}
