namespace CentralPsi.Web.Models.Entities;

/// <summary>Weekly recurring availability window used to generate bookable slots.</summary>
public class ProfessionalAvailability
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid ProfessionalId { get; set; }
    public Professional? Professional { get; set; }

    public DayOfWeek DayOfWeek { get; set; }
    public TimeSpan StartTime { get; set; }
    public TimeSpan EndTime { get; set; }
}
