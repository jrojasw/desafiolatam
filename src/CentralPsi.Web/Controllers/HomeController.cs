using System.Diagnostics;
using CentralPsi.Web.Data;
using CentralPsi.Web.Models;
using CentralPsi.Web.Models.Entities;
using CentralPsi.Web.Models.ViewModels;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CentralPsi.Web.Controllers;

public class HomeController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly ILogger<HomeController> _logger;

    public HomeController(ApplicationDbContext db, ILogger<HomeController> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<IActionResult> Index()
    {
        var vm = new HomeIndexViewModel
        {
            Slides = await _db.SlideImages
                .Where(s => s.IsActive)
                .OrderBy(s => s.SortOrder)
                .ToListAsync(),
            News = await _db.NewsArticles
                .Where(n => n.IsActive)
                .OrderByDescending(n => n.PublishedAtUtc)
                .Take(6)
                .ToListAsync()
        };
        return View(vm);
    }

    public IActionResult Contact() => View();

    public IActionResult Terms() => View();

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public async Task<IActionResult> Error()
    {
        var exceptionFeature = HttpContext.Features.Get<IExceptionHandlerPathFeature>();
        if (exceptionFeature?.Error is { } ex)
        {
            _logger.LogError(ex, "Excepción no controlada en {Path}", exceptionFeature.Path);
            try
            {
                _db.ErrorLogs.Add(new ErrorLog
                {
                    ExceptionType = ex.GetType().FullName ?? ex.GetType().Name,
                    Message = ex.Message,
                    StackTrace = ex.StackTrace,
                    Path = exceptionFeature.Path,
                    Method = Request.Method,
                    QueryString = Request.QueryString.HasValue ? Request.QueryString.Value : null
                });
                await _db.SaveChangesAsync();
            }
            catch (Exception logEx)
            {
                // Never let a failure to persist the error log break the error page itself.
                _logger.LogError(logEx, "No se pudo guardar el registro de error en la base de datos.");
            }
        }

        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
