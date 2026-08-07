using System.ComponentModel.DataAnnotations;
using CentralPsi.Web.Models.Entities;

namespace CentralPsi.Web.Models.ViewModels;

public class BookingStartViewModel
{
    public Guid ProfessionalId { get; set; }
    public string ProfessionalName { get; set; } = string.Empty;

    [Required]
    public DateTime StartUtc { get; set; }
    public DateTime StartLocal { get; set; }
    public decimal Amount { get; set; }

    [Required(ErrorMessage = "Ingresa tu nombre completo")]
    [Display(Name = "Nombre completo")]
    public string PatientFullName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Ingresa tu correo electrónico")]
    [EmailAddress]
    [Display(Name = "Correo electrónico")]
    public string PatientEmail { get; set; } = string.Empty;

    [Required(ErrorMessage = "Ingresa tu teléfono")]
    [Display(Name = "Teléfono")]
    public string PatientPhone { get; set; } = string.Empty;

    [Display(Name = "He leído y acepto los términos y condiciones, la política de reembolsos y que CentralPsi actúa solo como agendador")]
    public bool TermsAccepted { get; set; }
}

public class WebpayRedirectViewModel
{
    public string Token { get; set; } = string.Empty;
    public string RedirectUrl { get; set; } = string.Empty;
}

public class CancellationViewModel
{
    public Appointment Appointment { get; set; } = null!;
    public Professional Professional { get; set; } = null!;
    public DateTime StartLocal { get; set; }
    public RefundTier ProjectedTier { get; set; }
    public decimal ProjectedAmount { get; set; }
    public double HoursBefore { get; set; }
    public bool AlreadyCancelled { get; set; }
    [Display(Name = "Motivo de la cancelación (opcional)")]
    public string? Reason { get; set; }
}

public class AttendanceConfirmationViewModel
{
    public Appointment Appointment { get; set; } = null!;
    public Professional Professional { get; set; } = null!;
    public string Role { get; set; } = string.Empty; // "paciente" | "profesional"
    public bool AlreadyAnswered { get; set; }
}
