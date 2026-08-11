using CentralPsi.Web.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CentralPsi.Web.Controllers;

[Route("noticias")]
public class NewsController : Controller
{
    private readonly ApplicationDbContext _db;

    public NewsController(ApplicationDbContext db)
    {
        _db = db;
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Details(Guid id)
    {
        var article = await _db.NewsArticles.FirstOrDefaultAsync(n => n.Id == id && n.IsActive);
        if (article is null) return NotFound();

        return View(article);
    }
}
