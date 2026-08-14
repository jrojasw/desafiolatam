using System.Text.RegularExpressions;
using CentralPsi.Web.Data;
using CentralPsi.Web.Models.Entities;
using CentralPsi.Web.Options;
using MailKit;
using MailKit.Net.Imap;
using MailKit.Search;
using MailKit.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MimeKit;

namespace CentralPsi.Web.Services;

/// <summary>
/// Read-only IMAP sync for pagos@centralpsi.cl: fetches messages (and their PDF/image attachments - the
/// boletas de honorarios professionals send) into the database so they show up in the admin panel instead of
/// requiring a cPanel webmail login. Never marks messages read, deletes, or moves anything on the server.
/// </summary>
public class PaymentInboxSyncService : IPaymentInboxSyncService
{
    private const int MaxMessagesPerSync = 100;

    private readonly ApplicationDbContext _db;
    private readonly PaymentsInboxOptions _options;
    private readonly IFileStorageService _fileStorage;
    private readonly ILogger<PaymentInboxSyncService> _logger;

    public PaymentInboxSyncService(
        ApplicationDbContext db,
        IOptions<PaymentsInboxOptions> options,
        IFileStorageService fileStorage,
        ILogger<PaymentInboxSyncService> logger)
    {
        _db = db;
        _options = options.Value;
        _fileStorage = fileStorage;
        _logger = logger;
    }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(_options.Password);

    public async Task<int> SyncAsync(CancellationToken ct = default)
    {
        if (!IsConfigured)
        {
            _logger.LogWarning("PaymentsInbox no está configurado (falta PaymentsInbox:Password); se omite la sincronización.");
            return 0;
        }

        using var client = new ImapClient { Timeout = 20000 };
        await client.ConnectAsync(_options.Host, _options.Port, SecureSocketOptions.SslOnConnect, ct);
        await client.AuthenticateAsync(_options.User, _options.Password, ct);

        var inbox = client.Inbox;
        await inbox.OpenAsync(FolderAccess.ReadOnly, ct);

        var allUids = await inbox.SearchAsync(SearchQuery.All, ct);
        var alreadyImported = await _db.PaymentInboxMessages.Select(m => m.ImapUid).ToListAsync(ct);
        var alreadyImportedSet = alreadyImported.ToHashSet();

        var newUids = allUids
            .Where(uid => !alreadyImportedSet.Contains(uid.ToString()))
            .OrderByDescending(uid => uid.Id)
            .Take(MaxMessagesPerSync)
            .ToList();

        var imported = 0;
        foreach (var uid in newUids)
        {
            try
            {
                var mime = await inbox.GetMessageAsync(uid, ct);
                var record = await ImportMessageAsync(uid.ToString(), mime, ct);
                _db.PaymentInboxMessages.Add(record);
                imported++;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error importando el correo IMAP UID {Uid} de pagos@centralpsi.cl", uid);
            }
        }

        if (imported > 0)
        {
            await _db.SaveChangesAsync(ct);
        }

        await client.DisconnectAsync(true, ct);
        return imported;
    }

    private async Task<PaymentInboxMessage> ImportMessageAsync(string imapUid, MimeMessage mime, CancellationToken ct)
    {
        var from = mime.From.Mailboxes.FirstOrDefault();
        var record = new PaymentInboxMessage
        {
            ImapUid = imapUid,
            FromAddress = from?.Address ?? string.Empty,
            FromName = from?.Name ?? string.Empty,
            Subject = mime.Subject ?? string.Empty,
            BodyPreview = BuildPreview(mime),
            ReceivedAtUtc = mime.Date.UtcDateTime
        };

        foreach (var attachment in mime.Attachments)
        {
            if (attachment is not MimePart { Content: not null } part || string.IsNullOrWhiteSpace(part.FileName))
            {
                continue;
            }

            try
            {
                await using var content = new MemoryStream();
                await part.Content.DecodeToAsync(content, ct);
                content.Position = 0;
                var storedPath = await _fileStorage.SavePrivateStreamAsync(content, part.FileName, "boletas-recibidas");
                record.Attachments.Add(new PaymentInboxAttachment
                {
                    FileName = part.FileName,
                    StoredPath = storedPath
                });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "No se pudo guardar el adjunto {FileName} del correo {Uid}", part.FileName, imapUid);
            }
        }

        return record;
    }

    private static string BuildPreview(MimeMessage mime)
    {
        var text = mime.TextBody ?? StripHtml(mime.HtmlBody) ?? string.Empty;
        text = text.Trim();
        return text.Length > 500 ? text[..500] + "…" : text;
    }

    private static string? StripHtml(string? html) =>
        html is null ? null : Regex.Replace(html, "<[^>]+>", " ").Trim();
}
