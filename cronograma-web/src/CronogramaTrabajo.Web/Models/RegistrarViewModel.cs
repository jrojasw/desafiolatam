using System.ComponentModel.DataAnnotations;

namespace CronogramaTrabajo.Web.Models;

public class RegistrarViewModel
{
    [Required(ErrorMessage = "El nombre es obligatorio.")]
    [Display(Name = "Nombre completo")]
    public string NombreCompleto { get; set; } = string.Empty;

    [Required(ErrorMessage = "El correo es obligatorio.")]
    [EmailAddress(ErrorMessage = "Ingresa un correo válido.")]
    [Display(Name = "Correo")]
    public string Correo { get; set; } = string.Empty;

    [Required(ErrorMessage = "La contraseña es obligatoria.")]
    [StringLength(100, MinimumLength = 6, ErrorMessage = "La contraseña debe tener al menos 6 caracteres.")]
    [DataType(DataType.Password)]
    [Display(Name = "Contraseña")]
    public string Contrasena { get; set; } = string.Empty;

    [DataType(DataType.Password)]
    [Display(Name = "Confirmar contraseña")]
    [Compare(nameof(Contrasena), ErrorMessage = "Las contraseñas no coinciden.")]
    public string ConfirmarContrasena { get; set; } = string.Empty;
}
