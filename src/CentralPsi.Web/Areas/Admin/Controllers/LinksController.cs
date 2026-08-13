using CentralPsi.Web.Data.Seed;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CentralPsi.Web.Areas.Admin.Controllers;

/// <summary>Central hub of external services and internal admin pages used to run CentralPsi - sits behind the
/// same admin login as the rest of the dashboard.</summary>
[Area("Admin")]
[Authorize(Roles = DataSeeder.AdminRole)]
[Route("Admin/Enlaces")]
public class LinksController : Controller
{
    [HttpGet("")]
    public IActionResult Index() => View();
}
