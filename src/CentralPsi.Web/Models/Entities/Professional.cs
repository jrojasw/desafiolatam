namespace CentralPsi.Web.Models.Entities;

public enum ProfessionalStatus
{
    PendingVerification = 0,
    Verified = 1,
    Rejected = 2,
    Inactive = 3
}

public class Professional
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string? Rut { get; set; }

    public string? Specialty { get; set; }
    public string Orientation { get; set; } = string.Empty;
    public string Experience { get; set; } = string.Empty;
    public string? ProfilePhotoPath { get; set; }

    // Documents (stored outside wwwroot, never public - served through authenticated admin action only)
    public string CedulaFrontPath { get; set; } = string.Empty;
    public string CedulaBackPath { get; set; } = string.Empty;
    public string CertificateFilePath { get; set; } = string.Empty;

    // Superintendencia de Salud certificate validation
    public string CertificateValidationCode { get; set; } = string.Empty;
    public string? CertificateQrRawData { get; set; }
    public DateTime? CertificateVerifiedAt { get; set; }
    public string? CertificateVerificationNotes { get; set; }

    public ProfessionalStatus Status { get; set; } = ProfessionalStatus.PendingVerification;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? DeactivatedAt { get; set; }

    public List<ProfessionalAvailability> Availabilities { get; set; } = new();
    public List<Appointment> Appointments { get; set; } = new();
}
