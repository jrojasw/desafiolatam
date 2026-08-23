using System.Text;
using System.Xml.Linq;
using CentralPsi.Web.Data;
using CentralPsi.Web.Models.Entities;
using CentralPsi.Web.Options;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace CentralPsi.Web.Controllers;

/// <summary>Generates sitemap.xml from the live database on every request, instead of a hand-maintained
/// static file - a new verified professional or published news article shows up automatically, with no risk
/// of the sitemap silently going stale.</summary>
public class SitemapController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly AppOptions _appOptions;

    public SitemapController(ApplicationDbContext db, IOptions<AppOptions> appOptions)
    {
        _db = db;
        _appOptions = appOptions.Value;
    }

    [HttpGet("/sitemap.xml")]
    [HttpHead("/sitemap.xml")]
    public async Task<IActionResult> Index()
    {
        var baseUrl = _appOptions.BaseUrl.TrimEnd('/');
        XNamespace ns = "http://www.sitemaps.org/schemas/sitemap/0.9";

        var urls = new List<XElement>
        {
            UrlEntry(ns, $"{baseUrl}/", "weekly", "1.0"),
            UrlEntry(ns, $"{baseUrl}/profesionales", "weekly", "0.9"),
            UrlEntry(ns, $"{baseUrl}/profesionales/inscripcion", "monthly", "0.5"),
            UrlEntry(ns, $"{baseUrl}/Home/Contact", "yearly", "0.3"),
            UrlEntry(ns, $"{baseUrl}/Home/Terms", "yearly", "0.3"),
        };

        var professionalIds = await _db.Professionals
            .Where(p => p.Status == ProfessionalStatus.Verified)
            .Select(p => p.Id)
            .ToListAsync();
        urls.AddRange(professionalIds.Select(id => UrlEntry(ns, $"{baseUrl}/profesionales/{id}", "monthly", "0.7")));

        var newsIds = await _db.NewsArticles
            .Where(n => n.IsActive)
            .Select(n => n.Id)
            .ToListAsync();
        urls.AddRange(newsIds.Select(id => UrlEntry(ns, $"{baseUrl}/noticias/{id}", "monthly", "0.6")));

        var document = new XDocument(
            new XDeclaration("1.0", "UTF-8", null),
            new XElement(ns + "urlset", urls));
        return Content(document.Declaration + Environment.NewLine + document.Root, "application/xml", Encoding.UTF8);
    }

    private static XElement UrlEntry(XNamespace ns, string loc, string changeFreq, string priority) =>
        new(ns + "url",
            new XElement(ns + "loc", loc),
            new XElement(ns + "changefreq", changeFreq),
            new XElement(ns + "priority", priority));
}
