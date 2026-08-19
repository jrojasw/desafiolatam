namespace CentralPsi.Web.Services;

/// <summary>Writes immutable audit trail entries (Ley 21.719): who viewed or changed sensitive personal data,
/// and when. Entries are append-only - no admin action ever updates or deletes them.</summary>
public interface IAuditLogService
{
    Task LogAsync(string action, string entityType, string? entityId = null, string? details = null);
}
