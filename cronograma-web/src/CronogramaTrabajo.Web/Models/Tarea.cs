using System.ComponentModel.DataAnnotations;

namespace CronogramaTrabajo.Web.Models;

public class Tarea : IValidatableObject
{
    public int Id { get; set; }

    [Required(ErrorMessage = "El título es obligatorio.")]
    [StringLength(150)]
    [Display(Name = "Título")]
    public string Titulo { get; set; } = string.Empty;

    [StringLength(1000)]
    [Display(Name = "Descripción")]
    public string? Descripcion { get; set; }

    [Required]
    [Display(Name = "Responsable")]
    [StringLength(100)]
    public string Responsable { get; set; } = string.Empty;

    [Required]
    [Display(Name = "Fecha de inicio")]
    [DataType(DataType.Date)]
    public DateTime FechaInicio { get; set; } = DateTime.Today;

    [Required]
    [Display(Name = "Fecha de término")]
    [DataType(DataType.Date)]
    public DateTime FechaFin { get; set; } = DateTime.Today.AddDays(1);

    [Display(Name = "Estado")]
    public EstadoTarea Estado { get; set; } = EstadoTarea.Pendiente;

    [Display(Name = "Prioridad")]
    public Prioridad Prioridad { get; set; } = Prioridad.Media;

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (FechaFin < FechaInicio)
        {
            yield return new ValidationResult(
                "La fecha de término no puede ser anterior a la fecha de inicio.",
                new[] { nameof(FechaFin) });
        }
    }
}
