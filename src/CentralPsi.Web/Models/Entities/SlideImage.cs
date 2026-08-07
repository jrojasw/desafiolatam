namespace CentralPsi.Web.Models.Entities;

/// <summary>Homepage hero carousel image, editable from the admin dashboard.</summary>
public class SlideImage
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string ImagePath { get; set; } = string.Empty;
    public string? Title { get; set; }
    public string? Subtitle { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
