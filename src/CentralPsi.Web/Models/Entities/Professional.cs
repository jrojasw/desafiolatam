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

    /// <summary>Declared by the professional at registration; shown publicly on their profile.</summary>
    public bool IsFonasaRegistered { get; set; }

    /// <summary>Set when an admin sends the "confirm your Fonasa status" email to a professional who registered
    /// before this field existed; cleared once they answer through the one-time link, so it can't be reused.</summary>
    public string? FonasaConfirmationToken { get; set; }
    public DateTime? FonasaConfirmationSentAtUtc { get; set; }
    public DateTime? FonasaConfirmedAtUtc { get; set; }

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

    /// <summary>When the professional confirmed having an active SII "Inicio de Actividades" and understanding
    /// they must send their boleta de honorarios to pagos@centralpsi.cl to get paid for each session.</summary>
    public DateTime? TaxComplianceAcceptedAtUtc { get; set; }

    // Bank details used to transfer the professional's share of each session's payment. Collected once at
    // registration (private - never shown on the public profile) instead of trying to parse them out of every
    // payment email, which is unreliable.
    public string BankName { get; set; } = string.Empty;
    public string BankAccountType { get; set; } = string.Empty;
    public string BankAccountNumber { get; set; } = string.Empty;
    public string BankAccountHolderName { get; set; } = string.Empty;
    public string BankAccountHolderRut { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? DeactivatedAt { get; set; }

    public List<ProfessionalAvailability> Availabilities { get; set; } = new();
    public List<Appointment> Appointments { get; set; } = new();
}
