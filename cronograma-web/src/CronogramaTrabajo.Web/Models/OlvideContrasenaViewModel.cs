using System.ComponentModel.DataAnnotations;

namespace CronogramaTrabajo.Web.Models;

public class OlvideContrasenaViewModel
{
    [Required(ErrorMessage = "El correo es obligatorio.")]
    [EmailAddress(ErrorMessage = "Ingresa un correo válido.")]
    [Display(Name = "Correo")]
    public string Correo { get; set; } = string.Empty;
}
