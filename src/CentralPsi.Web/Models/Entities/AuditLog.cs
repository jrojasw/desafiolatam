namespace CentralPsi.Web.Models.Entities;

/// <summary>Immutable trail of who accessed or changed sensitive personal data (private documents, payment
/// receipts, anonymization/deletion requests) - required under Ley 21.719. Never updated or deleted once
/// written, not even by an admin, so entries are only ever inserted through <see cref="Services.IAuditLogService"/>.</summary>
public class AuditLog
{
    public long Id { get; set; }
    public DateTime OccurredAtUtc { get; set; } = DateTime.UtcNow;
    public string AdminEmail { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string EntityType { get; set; } = string.Empty;
    public string? EntityId { get; set; }
    public string? Details { get; set; }
    public string? IpAddress { get; set; }
}
