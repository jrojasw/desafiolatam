using CentralPsi.Web.Models.Entities;

namespace CentralPsi.Web.Models.ViewModels;

public class ProfessionalListViewModel
{
    public List<Professional> Professionals { get; set; } = new();
}

public class ProfessionalDetailsViewModel
{
    public Professional Professional { get; set; } = null!;
    public List<AvailableSlotGroup> SlotGroups { get; set; } = new();
}

public class AvailableSlotGroup
{
    public DateTime DateLocal { get; set; }
    public List<TimeSlotOption> Slots { get; set; } = new();
}

public class TimeSlotOption
{
    public DateTime StartUtc { get; set; }
    public string DisplayTime { get; set; } = string.Empty;
}

public class ProfessionalFonasaConfirmationViewModel
{
    public Professional Professional { get; set; } = null!;
    public bool AlreadyAnswered { get; set; }
    public bool LinkInvalid { get; set; }
}
