using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CentralPsi.Web.Models.ViewModels;

public class AvailabilitySlotInput
{
    public DayOfWeek DayOfWeek { get; set; }
    public bool Enabled { get; set; }
    public string? StartTime { get; set; }
    public string? EndTime { get; set; }
}

public class ProfessionalRegisterViewModel
{
    [Required(ErrorMessage = "Ingresa tu nombre completo")]
    [Display(Name = "Nombre completo")]
    public string FullName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Ingresa tu correo electrónico")]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Ingresa tu teléfono")]
    [Display(Name = "Teléfono")]
    public string Phone { get; set; } = string.Empty;

    [Display(Name = "RUT")]
    public string? Rut { get; set; }

    [Display(Name = "Especialidad (opcional)")]
    public string? Specialty { get; set; }

    [Required(ErrorMessage = "Indica tu orientación/enfoque terapéutico")]
    [Display(Name = "Orientación / enfoque terapéutico")]
    public string Orientation { get; set; } = string.Empty;

    [Display(Name = "Especifica tu orientación")]
    public string? OrientationOther { get; set; }

    [Required(ErrorMessage = "Cuéntanos tu formación y forma de trabajar (mínimo 40 caracteres)")]
    [MinLength(40, ErrorMessage = "Cuéntanos un poco más sobre tu formación y forma de trabajar (mínimo 40 caracteres)")]
    [Display(Name = "Experiencia y forma de trabajar")]
    public string Experience { get; set; } = string.Empty;

    public static readonly string[] OrientationOptions =
    {
        "Cognitivo-conductual",
        "Psicoanálisis / Psicodinámica",
        "Sistémica",
        "Humanista",
        "Gestalt",
        "Integrativa",
        "Terapia de Aceptación y Compromiso (ACT)",
        "EMDR",
        "Otro"
    };

    [Display(Name = "Foto de perfil (opcional)")]
    public IFormFile? ProfilePhoto { get; set; }

    [Required(ErrorMessage = "Debes adjuntar el frente de tu cédula")]
    [Display(Name = "Cédula de identidad - frente")]
    public IFormFile CedulaFront { get; set; } = null!;

    [Required(ErrorMessage = "Debes adjuntar el reverso de tu cédula")]
    [Display(Name = "Cédula de identidad - reverso")]
    public IFormFile CedulaBack { get; set; } = null!;

    [Required(ErrorMessage = "Debes adjuntar tu certificado del Ministerio de Salud")]
    [Display(Name = "Certificado del Ministerio de Salud (Superintendencia de Salud)")]
    public IFormFile CertificateFile { get; set; } = null!;

    [Required(ErrorMessage = "Ingresa el código de validación del certificado")]
    [Display(Name = "Código de validación del certificado")]
    public string CertificateValidationCode { get; set; } = string.Empty;

    [Display(Name = "Declaro contar con Inicio de Actividades vigente en el SII y entiendo que debo emitir mi boleta de honorarios a nombre del paciente y enviar una copia junto con mis datos bancarios a pagos@centralpsi.cl para recibir el pago de cada sesión dentro de 3 días hábiles")]
    public bool TaxComplianceAccepted { get; set; }

    public List<AvailabilitySlotInput> Availability { get; set; } = BuildDefaultAvailability();

    public static List<AvailabilitySlotInput> BuildDefaultAvailability()
    {
        return Enum.GetValues<DayOfWeek>()
            .OrderBy(d => (int)d == 0 ? 7 : (int)d) // Monday..Sunday
            .Select(d => new AvailabilitySlotInput { DayOfWeek = d, Enabled = false, StartTime = "09:00", EndTime = "18:00" })
            .ToList();
    }
}
