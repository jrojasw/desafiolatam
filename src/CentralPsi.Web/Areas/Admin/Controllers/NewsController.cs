using CentralPsi.Web.Areas.Admin.Models;
using CentralPsi.Web.Data;
using CentralPsi.Web.Data.Seed;
using CentralPsi.Web.Models.Entities;
using CentralPsi.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CentralPsi.Web.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = DataSeeder.AdminRole)]
[Route("Admin/News")]
public class NewsController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly IFileStorageService _fileStorage;

    public NewsController(ApplicationDbContext db, IFileStorageService fileStorage)
    {
        _db = db;
        _fileStorage = fileStorage;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index()
    {
        var news = await _db.NewsArticles.OrderByDescending(n => n.PublishedAtUtc).ToListAsync();
        return View(news);
    }

    [HttpGet("Create")]
    public IActionResult Create() => View("Form", new NewsFormViewModel());

    [HttpGet("Edit/{id:guid}")]
    public async Task<IActionResult> Edit(Guid id)
    {
        var article = await _db.NewsArticles.FindAsync(id);
        if (article is null) return NotFound();

        return View("Form", new NewsFormViewModel
        {
            Id = article.Id,
            Title = article.Title,
            Summary = article.Summary,
            Content = article.Content,
            Category = article.Category,
            SourceUrl = article.SourceUrl,
            IsActive = article.IsActive,
            CurrentImagePath = article.ImagePath
        });
    }

    [HttpPost("Save")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(NewsFormViewModel model)
    {
        if (!ModelState.IsValid) return View("Form", model);

        NewsArticle article;
        if (model.Id is Guid id)
        {
            article = await _db.NewsArticles.FindAsync(id) ?? throw new InvalidOperationException("Noticia no encontrada");
        }
        else
        {
            article = new NewsArticle();
            _db.NewsArticles.Add(article);
        }

        article.Title = model.Title.Trim();
        article.Summary = model.Summary.Trim();
        article.Content = model.Content.Trim();
        article.Category = model.Category;
        article.SourceUrl = string.IsNullOrWhiteSpace(model.SourceUrl) ? null : model.SourceUrl.Trim();
        article.IsActive = model.IsActive;
        if (model.Image is { Length: > 0 })
        {
            article.ImagePath = await _fileStorage.SavePublicAsync(model.Image, "news");
        }

        await _db.SaveChangesAsync();
        TempData["SuccessMessage"] = "Noticia guardada correctamente.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("Delete/{id:guid}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(Guid id)
    {
        var article = await _db.NewsArticles.FindAsync(id);
        if (article is not null)
        {
            _db.NewsArticles.Remove(article);
            await _db.SaveChangesAsync();
        }
        TempData["SuccessMessage"] = "Noticia eliminada.";
        return RedirectToAction(nameof(Index));
    }
}
