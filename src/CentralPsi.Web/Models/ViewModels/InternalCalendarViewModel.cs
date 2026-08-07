using CentralPsi.Web.Models.Entities;

namespace CentralPsi.Web.Models.ViewModels;

public class InternalCalendarRow
{
    public Appointment Appointment { get; set; } = null!;
    public Professional Professional { get; set; } = null!;
    public DateTime StartLocal { get; set; }
    public bool IsPaid { get; set; }
}

public class InternalCalendarViewModel
{
    public DateTime FromLocal { get; set; }
    public DateTime ToLocal { get; set; }
    public List<InternalCalendarRow> Rows { get; set; } = new();
}
