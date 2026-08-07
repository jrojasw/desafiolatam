using System.ComponentModel.DataAnnotations;

namespace CronogramaTrabajo.Web.Models;

public class RestablecerContrasenaViewModel
{
    [Required]
    public string UserId { get; set; } = string.Empty;

    [Required]
    public string Token { get; set; } = string.Empty;

    [Required(ErrorMessage = "La contraseña es obligatoria.")]
    [StringLength(100, MinimumLength = 6, ErrorMessage = "La contraseña debe tener al menos 6 caracteres.")]
    [DataType(DataType.Password)]
    [Display(Name = "Nueva contraseña")]
    public string NuevaContrasena { get; set; } = string.Empty;

    [DataType(DataType.Password)]
    [Display(Name = "Confirmar nueva contraseña")]
    [Compare(nameof(NuevaContrasena), ErrorMessage = "Las contraseñas no coinciden.")]
    public string ConfirmarNuevaContrasena { get; set; } = string.Empty;
}
