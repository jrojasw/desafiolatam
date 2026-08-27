using CentralPsi.Web.Data;
using CentralPsi.Web.Data.Seed;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CentralPsi.Web.Areas.Admin.Controllers;

/// <summary>
/// Lists unhandled exceptions captured in production (see HomeController.Error) so admins can diagnose
/// production errors without needing access to the hosting platform's logs.
/// </summary>
[Area("Admin")]
[Authorize(Roles = DataSeeder.AdminRole)]
[Route("Admin/Errores")]
public class ErrorsController : Controller
{
    private readonly ApplicationDbContext _db;

    public ErrorsController(ApplicationDbContext db)
    {
        _db = db;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index()
    {
        ViewBag.Logs = await _db.ErrorLogs
            .OrderByDescending(e => e.OccurredAtUtc)
            .Take(200)
            .ToListAsync();
        return View();
    }

    [HttpPost("Limpiar")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Clear()
    {
        await _db.ErrorLogs.ExecuteDeleteAsync();
        TempData["SuccessMessage"] = "Se eliminaron todos los registros de error.";
        return RedirectToAction(nameof(Index));
    }
}
