namespace CentralPsi.Web.Models.Entities;

public enum NewsCategory
{
    Noticia = 0,
    Consejo = 1,
    Tip = 2,
    EstudioCientifico = 3
}

public static class NewsCategoryExtensions
{
    public static string ToDisplayName(this NewsCategory category) => category switch
    {
        NewsCategory.Noticia => "Noticia",
        NewsCategory.Consejo => "Consejo",
        NewsCategory.Tip => "Tip",
        NewsCategory.EstudioCientifico => "Estudio científico",
        _ => category.ToString()
    };
}

/// <summary>Mental health news/tips/studies card shown on the homepage, editable from the admin dashboard.</summary>
public class NewsArticle
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Title { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string? ImagePath { get; set; }
    public string? SourceUrl { get; set; }
    public NewsCategory Category { get; set; } = NewsCategory.Noticia;

    public bool IsActive { get; set; } = true;
    public DateTime PublishedAtUtc { get; set; } = DateTime.UtcNow;
}
