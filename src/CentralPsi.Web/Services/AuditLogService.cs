using CentralPsi.Web.Data;
using CentralPsi.Web.Models.Entities;
using Microsoft.AspNetCore.Http;

namespace CentralPsi.Web.Services;

public class AuditLogService : IAuditLogService
{
    private readonly ApplicationDbContext _db;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<AuditLogService> _logger;

    public AuditLogService(ApplicationDbContext db, IHttpContextAccessor httpContextAccessor, ILogger<AuditLogService> logger)
    {
        _db = db;
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
    }

    public async Task LogAsync(string action, string entityType, string? entityId = null, string? details = null)
    {
        try
        {
            var http = _httpContextAccessor.HttpContext;
            var entry = new AuditLog
            {
                AdminEmail = http?.User?.Identity?.Name ?? "sistema",
                Action = action,
                EntityType = entityType,
                EntityId = entityId,
                Details = details,
                IpAddress = http?.Connection?.RemoteIpAddress?.ToString()
            };
            _db.AuditLogs.Add(entry);
            await _db.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            // Never let an audit-log failure block the underlying admin action.
            _logger.LogError(ex, "No se pudo escribir el log de auditoría para {Action} sobre {EntityType} {EntityId}", action, entityType, entityId);
        }
    }
}
