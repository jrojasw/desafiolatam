using System.ComponentModel.DataAnnotations;

namespace CronogramaTrabajo.Web.Models;

public class IniciarSesionViewModel
{
    [Required(ErrorMessage = "El correo es obligatorio.")]
    [EmailAddress(ErrorMessage = "Ingresa un correo válido.")]
    [Display(Name = "Correo")]
    public string Correo { get; set; } = string.Empty;

    [Required(ErrorMessage = "La contraseña es obligatoria.")]
    [DataType(DataType.Password)]
    [Display(Name = "Contraseña")]
    public string Contrasena { get; set; } = string.Empty;

    [Display(Name = "Mantener sesión iniciada")]
    public bool Recordarme { get; set; }
}
