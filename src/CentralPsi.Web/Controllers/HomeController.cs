using System.Diagnostics;
using CentralPsi.Web.Data;
using CentralPsi.Web.Models;
using CentralPsi.Web.Models.ViewModels;
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
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
