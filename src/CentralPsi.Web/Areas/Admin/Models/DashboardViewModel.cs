namespace CentralPsi.Web.Areas.Admin.Models;

public class DashboardViewModel
{
    public int PendingProfessionals { get; set; }
    public int VerifiedProfessionals { get; set; }
    public int UpcomingAppointments { get; set; }
    public int PendingRefunds { get; set; }
    public decimal RevenueThisMonth { get; set; }
}
