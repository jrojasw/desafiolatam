using System.ComponentModel.DataAnnotations;
using CentralPsi.Web.Models.Entities;

namespace CentralPsi.Web.Areas.Admin.Models;

public class NewsFormViewModel
{
    public Guid? Id { get; set; }

    [Required(ErrorMessage = "Ingresa un título")]
    [Display(Name = "Título")]
    public string Title { get; set; } = string.Empty;

    [Required(ErrorMessage = "Ingresa un resumen")]
    [Display(Name = "Resumen (se muestra en la tarjeta)")]
    public string Summary { get; set; } = string.Empty;

    [Required(ErrorMessage = "Ingresa el contenido")]
    [Display(Name = "Contenido completo")]
    public string Content { get; set; } = string.Empty;

    [Display(Name = "Categoría")]
    public NewsCategory Category { get; set; }

    [Display(Name = "Fuente (URL opcional)")]
    public string? SourceUrl { get; set; }

    [Display(Name = "Activo")]
    public bool IsActive { get; set; } = true;

    [Display(Name = "Imagen (opcional)")]
    public IFormFile? Image { get; set; }

    public string? CurrentImagePath { get; set; }
}
