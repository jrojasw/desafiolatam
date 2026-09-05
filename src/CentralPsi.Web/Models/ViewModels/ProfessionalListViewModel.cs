using System.ComponentModel.DataAnnotations;
using CentralPsi.Web.Models.Entities;

namespace CentralPsi.Web.Models.ViewModels;

public class ProfessionalListViewModel
{
    public List<Professional> Professionals { get; set; } = new();
}

public class ProfessionalDetailsViewModel
{
    public Professional Professional { get; set; } = null!;
    public List<AvailableSlotGroup> SlotGroups { get; set; } = new();
}

public class AvailableSlotGroup
{
    public DateTime DateLocal { get; set; }
    public List<TimeSlotOption> Slots { get; set; } = new();
}

public class TimeSlotOption
{
    public DateTime StartUtc { get; set; }
    public string DisplayTime { get; set; } = string.Empty;
}

public class ProfessionalDocumentResubmissionViewModel
{
    public Professional Professional { get; set; } = null!;
    public bool LinkInvalid { get; set; }

    [Display(Name = "Cédula de identidad - frente")]
    public IFormFile? CedulaFront { get; set; }

    [Display(Name = "Cédula de identidad - reverso")]
    public IFormFile? CedulaBack { get; set; }

    [Display(Name = "Certificado del Ministerio de Salud (Superintendencia de Salud)")]
    public IFormFile? CertificateFile { get; set; }

    [Display(Name = "Código de validación del certificado")]
    public string? CertificateValidationCode { get; set; }
}
