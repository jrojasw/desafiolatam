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
[Route("Admin/Slides")]
public class SlidesController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly IFileStorageService _fileStorage;

    public SlidesController(ApplicationDbContext db, IFileStorageService fileStorage)
    {
        _db = db;
        _fileStorage = fileStorage;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index()
    {
        var slides = await _db.SlideImages.OrderBy(s => s.SortOrder).ToListAsync();
        return View(slides);
    }

    [HttpGet("Create")]
    public IActionResult Create() => View("Form", new SlideFormViewModel());

    [HttpGet("Edit/{id:guid}")]
    public async Task<IActionResult> Edit(Guid id)
    {
        var slide = await _db.SlideImages.FindAsync(id);
        if (slide is null) return NotFound();

        return View("Form", new SlideFormViewModel
        {
            Id = slide.Id,
            Title = slide.Title,
            Subtitle = slide.Subtitle,
            ButtonText = slide.ButtonText,
            ButtonUrl = slide.ButtonUrl,
            SortOrder = slide.SortOrder,
            IsActive = slide.IsActive,
            CurrentImagePath = slide.ImagePath
        });
    }

    [HttpPost("Save")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(SlideFormViewModel model)
    {
        if (model.Id is null && model.Image is null)
        {
            ModelState.AddModelError(nameof(model.Image), "Debes subir una imagen para el nuevo slide.");
        }

        if (!ModelState.IsValid) return View("Form", model);

        SlideImage slide;
        if (model.Id is Guid id)
        {
            slide = await _db.SlideImages.FindAsync(id) ?? throw new InvalidOperationException("Slide no encontrado");
        }
        else
        {
            slide = new SlideImage();
            _db.SlideImages.Add(slide);
        }

        slide.Title = model.Title;
        slide.Subtitle = model.Subtitle;
        slide.ButtonText = model.ButtonText;
        slide.ButtonUrl = model.ButtonUrl;
        slide.SortOrder = model.SortOrder;
        slide.IsActive = model.IsActive;
        if (model.Image is { Length: > 0 })
        {
            slide.ImagePath = await _fileStorage.SavePublicAsync(model.Image, "slides");
        }

        await _db.SaveChangesAsync();
        TempData["SuccessMessage"] = "Slide guardado correctamente.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("Delete/{id:guid}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(Guid id)
    {
        var slide = await _db.SlideImages.FindAsync(id);
        if (slide is not null)
        {
            _db.SlideImages.Remove(slide);
            await _db.SaveChangesAsync();
        }
        TempData["SuccessMessage"] = "Slide eliminado.";
        return RedirectToAction(nameof(Index));
    }
}
