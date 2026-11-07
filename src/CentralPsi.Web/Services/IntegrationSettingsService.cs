using CentralPsi.Web.Data;
using CentralPsi.Web.Models.Entities;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;

namespace CentralPsi.Web.Services;

public class IntegrationSettingsService : IIntegrationSettingsService
{
    // Bumping this purpose string would make every previously-stored value undecryptable (by design - that's
    // how Data Protection purpose strings work), so it's only ever meant to change deliberately, never casually.
    private const string Purpose = "CentralPsi.IntegrationSettings.v1";

    private readonly ApplicationDbContext _db;
    private readonly IDataProtector _protector;
    private readonly ILogger<IntegrationSettingsService> _logger;

    public IntegrationSettingsService(ApplicationDbContext db, IDataProtectionProvider dataProtectionProvider, ILogger<IntegrationSettingsService> logger)
    {
        _db = db;
        _protector = dataProtectionProvider.CreateProtector(Purpose);
        _logger = logger;
    }

    public async Task<string?> GetAsync(string key, CancellationToken ct = default)
    {
        var setting = await _db.IntegrationSettings.AsNoTracking().FirstOrDefaultAsync(s => s.Key == key, ct);
        if (setting is null) return null;

        try
        {
            return _protector.Unprotect(setting.EncryptedValue);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "No se pudo desencriptar la configuración {Key}; se tratará como si no existiera", key);
            return null;
        }
    }

    public async Task<DateTime?> GetUpdatedAtUtcAsync(string key, CancellationToken ct = default)
    {
        return await _db.IntegrationSettings.AsNoTracking()
            .Where(s => s.Key == key)
            .Select(s => (DateTime?)s.UpdatedAtUtc)
            .FirstOrDefaultAsync(ct);
    }

    public async Task SetAsync(string key, string value, CancellationToken ct = default)
    {
        var encrypted = _protector.Protect(value);
        var setting = await _db.IntegrationSettings.FirstOrDefaultAsync(s => s.Key == key, ct);
        if (setting is null)
        {
            _db.IntegrationSettings.Add(new IntegrationSetting { Key = key, EncryptedValue = encrypted, UpdatedAtUtc = DateTime.UtcNow });
        }
        else
        {
            setting.EncryptedValue = encrypted;
            setting.UpdatedAtUtc = DateTime.UtcNow;
        }
        await _db.SaveChangesAsync(ct);
    }
}
