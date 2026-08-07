using CronogramaTrabajo.Web.Models;
using Microsoft.AspNetCore.Identity;

namespace CronogramaTrabajo.Web.Data;

public static class IdentitySeeder
{
    public const string RolAdministrador = "Administrador";

    public static async Task SeedAsync(
        RoleManager<IdentityRole> roleManager,
        UserManager<ApplicationUser> userManager,
        IConfiguration configuration)
    {
        if (!await roleManager.RoleExistsAsync(RolAdministrador))
        {
            await roleManager.CreateAsync(new IdentityRole(RolAdministrador));
        }

        var adminEmail = configuration["AccessControl:AdminEmail"];
        if (string.IsNullOrWhiteSpace(adminEmail))
        {
            return;
        }

        var admin = await userManager.FindByEmailAsync(adminEmail.Trim().ToLowerInvariant());
        if (admin is not null && !await userManager.IsInRoleAsync(admin, RolAdministrador))
        {
            await userManager.AddToRoleAsync(admin, RolAdministrador);
        }
    }
}
