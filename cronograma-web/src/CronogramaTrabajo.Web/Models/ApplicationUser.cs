using Microsoft.AspNetCore.Identity;

namespace CronogramaTrabajo.Web.Models;

public class ApplicationUser : IdentityUser
{
    public string NombreCompleto { get; set; } = string.Empty;
}
