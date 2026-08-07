using CronogramaTrabajo.Web.Models;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace CronogramaTrabajo.Web.Data;

public class CronogramaContext : IdentityDbContext<ApplicationUser>, IDataProtectionKeyContext
{
    public CronogramaContext(DbContextOptions<CronogramaContext> options) : base(options)
    {
    }

    public DbSet<Tarea> Tareas => Set<Tarea>();

    public DbSet<Microsoft.AspNetCore.DataProtection.EntityFrameworkCore.DataProtectionKey> DataProtectionKeys => Set<Microsoft.AspNetCore.DataProtection.EntityFrameworkCore.DataProtectionKey>();
}
