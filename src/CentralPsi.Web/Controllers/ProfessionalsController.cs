using CentralPsi.Web.Data;
using CentralPsi.Web.Models.Entities;
using CentralPsi.Web.Models.ViewModels;
using CentralPsi.Web.Options;
using CentralPsi.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace CentralPsi.Web.Controllers;

[Route("profesionales")]
public class ProfessionalsController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly IFileStorageService _fileStorage;
    private readonly ICertificateValidationService _certificateValidation;
    private readonly INotificationService _notifications;
    private readonly ISlotAvailabilityService _slots;
    private readonly ITimeZoneService _timeZoneService;
    private readonly IWhatsAppNotificationService _whatsApp;
    private readonly AppOptions _appOptions;
    private readonly ILogger<ProfessionalsController> _logger;

    public ProfessionalsController(
        ApplicationDbContext db,
        IFileStorageService fileStorage,
        ICertificateValidationService certificateValidation,
        INotificationService notifications,
        ISlotAvailabilityService slots,
        ITimeZoneService timeZoneService,
        IWhatsAppNotificationService whatsApp,
        IOptions<AppOptions> appOptions,
        ILogger<ProfessionalsController> logger)
    {
        _db = db;
        _fileStorage = fileStorage;
        _certificateValidation = certificateValidation;
        _notifications = notifications;
        _slots = slots;
        _timeZoneService = timeZoneService;
        _whatsApp = whatsApp;
        _appOptions = appOptions.Value;
        _logger = logger;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index()
    {
        var professionals = await _db.Professionals
            .Where(p => p.Status == ProfessionalStatus.Verified)
            .OrderBy(p => p.FullName)
            .ToListAsync();
        ViewBag.BookingEnabled = _appOptions.BookingEnabled;
        return View(new ProfessionalListViewModel { Professionals = professionals });
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Details(Guid id)
    {
        var professional = await _db.Professionals
            .FirstOrDefaultAsync(p => p.Id == id && p.Status == ProfessionalStatus.Verified);
        if (professional is null) return NotFound();

        ViewBag.BookingEnabled = _appOptions.BookingEnabled;
        if (!_appOptions.BookingEnabled)
        {
            return View(new ProfessionalDetailsViewModel { Professional = professional, SlotGroups = new List<AvailableSlotGroup>() });
        }

        var slots = await _slots.GetAvailableSlotsAsync(id);
        var groups = slots
            .GroupBy(s => _timeZoneService.ToLocal(s.StartUtc).Date)
            .OrderBy(g => g.Key)
            .Take(14)
            .Select(g => new AvailableSlotGroup
            {
                DateLocal = g.Key,
                Slots = g.Select(s => new TimeSlotOption
                {
                    StartUtc = s.StartUtc,
                    DisplayTime = _timeZoneService.ToLocal(s.StartUtc).ToString("HH:mm")
                }).ToList()
            })
            .ToList();

        return View(new ProfessionalDetailsViewModel { Professional = professional, SlotGroups = groups });
    }

    [HttpGet("inscripcion")]
    public IActionResult Register() => View(new ProfessionalRegisterViewModel());

    [HttpGet("inicio-de-actividades")]
    public IActionResult IniciacionActividades() => View();

    [HttpGet("como-funcionan-los-pagos")]
    public IActionResult CondicionesPago() => View(_appOptions);

    [HttpPost("inscripcion")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(ProfessionalRegisterViewModel model)
    {
        if (await _db.Professionals.AnyAsync(p => p.Email == model.Email))
        {
            ModelState.AddModelError(nameof(model.Email), "Ya existe una inscripción con este correo.");
        }

        if (model.Orientation == "Otro" && string.IsNullOrWhiteSpace(model.OrientationOther))
        {
            ModelState.AddModelError(nameof(model.OrientationOther), "Especifica tu orientación");
        }

        if (!model.TaxComplianceAccepted)
        {
            ModelState.AddModelError(nameof(model.TaxComplianceAccepted), "Debes confirmar que cuentas con Inicio de Actividades para continuar");
        }

        if (!string.IsNullOrWhiteSpace(model.Rut) && !string.IsNullOrWhiteSpace(model.BankAccountHolderRut)
            && NormalizeRut(model.Rut) != NormalizeRut(model.BankAccountHolderRut))
        {
            ModelState.AddModelError(nameof(model.BankAccountHolderRut), "La cuenta bancaria debe estar a tu propio nombre: este RUT debe coincidir con el que ingresaste arriba. No aceptamos cuentas de terceros ni de sociedades.");
        }

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var resolvedOrientation = model.Orientation == "Otro"
            ? model.OrientationOther!.Trim()
            : model.Orientation.Trim();

        var professional = new Professional
        {
            FullName = model.FullName.Trim(),
            Email = model.Email.Trim().ToLowerInvariant(),
            Phone = model.Phone.Trim(),
            Rut = model.Rut?.Trim(),
            Specialty = string.IsNullOrWhiteSpace(model.Specialty) ? null : model.Specialty.Trim(),
            Orientation = resolvedOrientation,
            Experience = model.Experience.Trim(),
            CertificateValidationCode = model.CertificateValidationCode.Trim(),
            Status = ProfessionalStatus.PendingVerification,
            TaxComplianceAcceptedAtUtc = DateTime.UtcNow,
            BankName = model.BankName.Trim(),
            BankAccountType = model.BankAccountType.Trim(),
            BankAccountNumber = model.BankAccountNumber.Trim(),
            BankAccountHolderName = model.BankAccountHolderName.Trim(),
            BankAccountHolderRut = model.BankAccountHolderRut.Trim()
        };

        professional.CedulaFrontPath = await _fileStorage.SavePrivateAsync(model.CedulaFront, "cedulas");
        professional.CedulaBackPath = await _fileStorage.SavePrivateAsync(model.CedulaBack, "cedulas");
        professional.CertificateFilePath = await _fileStorage.SavePrivateAsync(model.CertificateFile, "certificados");
        if (model.ProfilePhoto is { Length: > 0 })
        {
            professional.ProfilePhotoPath = await _fileStorage.SavePublicAsync(model.ProfilePhoto, "profesionales");
        }

        foreach (var day in model.Availability.Where(a => a.Enabled))
        {
            if (!TimeSpan.TryParse(day.StartTime, out var start) || !TimeSpan.TryParse(day.EndTime, out var end) || end <= start)
            {
                continue;
            }

            professional.Availabilities.Add(new ProfessionalAvailability
            {
                DayOfWeek = day.DayOfWeek,
                StartTime = start,
                EndTime = end
            });
        }

        _db.Professionals.Add(professional);
        await _db.SaveChangesAsync();

        await _whatsApp.SendAsync(
            $"🆕 Nuevo profesional inscrito en CentralPsi: {professional.FullName} ({professional.Email}). Revísalo en el panel admin.");

        await TryAutoValidateAsync(professional);

        // SuccessMessage is rendered HTML-encoded (some call sites elsewhere interpolate user-supplied names into
        // it), so the bold notice goes in this separate, developer-controlled-only key that the layout renders raw.
        TempData["SuccessMessage"] = "¡Gracias por inscribirte! Estamos validando tu certificado; te avisaremos por correo apenas quede publicado tu perfil.";
        TempData["SuccessMessageHtmlSuffix"] = " <strong>Si más adelante necesitas modificar algún dato de tu perfil (foto, descripción, orientación, etc.), escríbenos a admin@centralpsi.cl.</strong>";
        return RedirectToAction(nameof(Index));
    }

    private async Task TryAutoValidateAsync(Professional professional)
    {
        try
        {
            var physicalPath = _fileStorage.GetPrivatePhysicalPath(professional.CertificateFilePath);
            var result = await _certificateValidation.ValidateAsync(professional.CertificateValidationCode, physicalPath);

            professional.CertificateQrRawData = result.QrRawData;
            professional.CertificateVerificationNotes = result.Notes;
            professional.CertificateVerifiedAt = DateTime.UtcNow;

            if (result.IsValid && !result.Inconclusive)
            {
                professional.Status = ProfessionalStatus.Verified;
                await _db.SaveChangesAsync();
                await _notifications.SendProfessionalVerifiedAsync(professional);
            }
            else
            {
                // Left as PendingVerification either way (never auto-reject) so an inconclusive automated
                // check always falls back to a human decision in the admin dashboard.
                await _db.SaveChangesAsync();
                await _notifications.SendProfessionalRejectedAsync(professional, result.Notes);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error en la validación automática del certificado para {ProfessionalId}", professional.Id);
        }
    }

    /// <summary>Strips dots/dashes and uppercases so "12.345.678-9" and "12345678-9" compare equal.</summary>
    private static string NormalizeRut(string rut) =>
        new string(rut.Where(c => !char.IsWhiteSpace(c) && c != '.' && c != '-').ToArray()).ToUpperInvariant();
}
