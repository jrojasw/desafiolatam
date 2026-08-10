using System.ComponentModel.DataAnnotations;

namespace CentralPsi.Web.Areas.Admin.Models;

public class SlideFormViewModel
{
    public Guid? Id { get; set; }

    [Display(Name = "Título")]
    public string? Title { get; set; }

    [Display(Name = "Subtítulo")]
    public string? Subtitle { get; set; }

    [Display(Name = "Texto del botón")]
    public string? ButtonText { get; set; }

    [Display(Name = "Enlace del botón")]
    public string? ButtonUrl { get; set; }

    [Display(Name = "Orden")]
    public int SortOrder { get; set; }

    [Display(Name = "Activo")]
    public bool IsActive { get; set; } = true;

    [Display(Name = "Imagen")]
    public IFormFile? Image { get; set; }

    public string? CurrentImagePath { get; set; }
}
