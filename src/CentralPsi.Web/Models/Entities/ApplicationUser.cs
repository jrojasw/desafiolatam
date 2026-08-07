using Microsoft.AspNetCore.Identity;

namespace CentralPsi.Web.Models.Entities;

public class ApplicationUser : IdentityUser
{
    public string FullName { get; set; } = string.Empty;
}
