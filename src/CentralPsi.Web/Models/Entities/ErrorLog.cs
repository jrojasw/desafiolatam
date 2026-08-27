namespace CentralPsi.Web.Models.Entities;

/// <summary>Captures unhandled exceptions from production so they're diagnosable from the admin panel instead
/// of only through the hosting platform's rotating logs. Purely operational/diagnostic - not part of the
/// Ley 21.719 audit trail, so admins can clear old entries freely.</summary>
public class ErrorLog
{
    public long Id { get; set; }
    public DateTime OccurredAtUtc { get; set; } = DateTime.UtcNow;
    public string ExceptionType { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? StackTrace { get; set; }
    public string Path { get; set; } = string.Empty;
    public string Method { get; set; } = string.Empty;
    public string? QueryString { get; set; }
}
