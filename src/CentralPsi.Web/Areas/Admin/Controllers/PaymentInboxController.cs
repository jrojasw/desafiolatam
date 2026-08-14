using CentralPsi.Web.Data;
using CentralPsi.Web.Data.Seed;
using CentralPsi.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CentralPsi.Web.Areas.Admin.Controllers;

/// <summary>Read-only mirror of the pagos@centralpsi.cl mailbox (via IPaymentInboxSyncService), so the admin
/// never has to log into cPanel's webmail just to see incoming boletas de honorarios.</summary>
[Area("Admin")]
[Authorize(Roles = DataSeeder.AdminRole)]
[Route("Admin/BandejaPagos")]
public class PaymentInboxController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly IFileStorageService _fileStorage;
    private readonly IPaymentInboxSyncService _sync;

    public PaymentInboxController(ApplicationDbContext db, IFileStorageService fileStorage, IPaymentInboxSyncService sync)
    {
        _db = db;
        _fileStorage = fileStorage;
        _sync = sync;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(string filter = "pendientes")
    {
        var query = _db.PaymentInboxMessages.Include(m => m.Attachments).AsQueryable();
        query = filter switch
        {
            "revisados" => query.Where(m => m.Reviewed),
            "todos" => query,
            _ => query.Where(m => !m.Reviewed)
        };

        var messages = await query.OrderByDescending(m => m.ReceivedAtUtc).ToListAsync();

        ViewData["Filter"] = filter;
        ViewData["IsConfigured"] = _sync.IsConfigured;
        return View(messages);
    }

    [HttpPost("Sincronizar")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Sync()
    {
        if (!_sync.IsConfigured)
        {
            TempData["ErrorMessage"] = "Falta configurar PaymentsInbox:Password para poder sincronizar.";
            return RedirectToAction(nameof(Index));
        }

        try
        {
            var imported = await _sync.SyncAsync();
            TempData["SuccessMessage"] = imported > 0
                ? $"Se importaron {imported} correo(s) nuevo(s)."
                : "No hay correos nuevos.";
        }
        catch (Exception ex)
        {
            TempData["ErrorMessage"] = $"No se pudo sincronizar: {ex.Message}";
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost("{id:guid}/MarcarRevisado")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkReviewed(Guid id)
    {
        var message = await _db.PaymentInboxMessages.FindAsync(id);
        if (message is null) return NotFound();

        message.Reviewed = true;
        await _db.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("{id:guid}/MarcarPendiente")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkPending(Guid id)
    {
        var message = await _db.PaymentInboxMessages.FindAsync(id);
        if (message is null) return NotFound();

        message.Reviewed = false;
        await _db.SaveChangesAsync();
        return RedirectToAction(nameof(Index), new { filter = "revisados" });
    }

    [HttpGet("Adjunto/{attachmentId:guid}")]
    public async Task<IActionResult> Attachment(Guid attachmentId)
    {
        var attachment = await _db.PaymentInboxAttachments.FindAsync(attachmentId);
        if (attachment is null) return NotFound();

        var physicalPath = _fileStorage.GetPrivatePhysicalPath(attachment.StoredPath);
        if (!System.IO.File.Exists(physicalPath)) return NotFound();

        var contentType = Path.GetExtension(physicalPath).ToLowerInvariant() switch
        {
            ".pdf" => "application/pdf",
            ".png" => "image/png",
            ".webp" => "image/webp",
            _ => "image/jpeg"
        };
        var bytes = await System.IO.File.ReadAllBytesAsync(physicalPath);
        return File(bytes, contentType, attachment.FileName);
    }
}
