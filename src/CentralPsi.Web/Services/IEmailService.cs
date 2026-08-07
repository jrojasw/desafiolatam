namespace CentralPsi.Web.Services;

public interface IEmailService
{
    Task SendAsync(string toEmail, string toName, string subject, string htmlBody, IEnumerable<string>? cc = null);
}
