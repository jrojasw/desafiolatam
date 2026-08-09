using CentralPsi.Web.Options;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;

namespace CentralPsi.Web.Services;

public class SmtpEmailService : IEmailService
{
    private readonly SmtpOptions _options;
    private readonly ILogger<SmtpEmailService> _logger;

    public SmtpEmailService(IOptions<SmtpOptions> options, ILogger<SmtpEmailService> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task SendAsync(string toEmail, string toName, string subject, string htmlBody, IEnumerable<string>? cc = null)
    {
        if (_options.DryRun || string.IsNullOrWhiteSpace(_options.Host))
        {
            _logger.LogInformation(
                "[DryRun email] To: {To} <{ToEmail}> | CC: {Cc} | Subject: {Subject}\n{Body}",
                toName, toEmail, cc is null ? "" : string.Join(", ", cc), subject, htmlBody);
            return;
        }

        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(_options.FromName, _options.FromAddress));
        message.To.Add(new MailboxAddress(toName, toEmail));
        if (cc is not null)
        {
            foreach (var c in cc)
            {
                message.Cc.Add(MailboxAddress.Parse(c));
            }
        }

        message.Subject = subject;
        message.Body = new TextPart("html") { Text = htmlBody };

        using var client = new SmtpClient();
        // Port 465 is "implicit TLS" (SSL from the first byte); port 587 is "STARTTLS" (starts plaintext,
        // then upgrades). Using the wrong one for a given port fails the handshake, so pick based on the
        // port rather than a single UseSsl flag.
        var socketOptions = _options.Port switch
        {
            465 => SecureSocketOptions.SslOnConnect,
            587 => SecureSocketOptions.StartTls,
            _ => _options.UseSsl ? SecureSocketOptions.StartTls : SecureSocketOptions.Auto
        };
        await client.ConnectAsync(_options.Host, _options.Port, socketOptions);
        if (!string.IsNullOrEmpty(_options.User))
        {
            await client.AuthenticateAsync(_options.User, _options.Password);
        }

        await client.SendAsync(message);
        await client.DisconnectAsync(true);
    }
}
