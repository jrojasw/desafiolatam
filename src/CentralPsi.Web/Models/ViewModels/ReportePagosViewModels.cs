namespace CentralPsi.Web.Models.ViewModels;

/// <summary>One row of the "Citas y pagos" report table - deliberately carries no patient-identifying field
/// (name/email/phone), only the booking/payment facts needed to audit the funnel.</summary>
public class ReportePagosRow
{
    public string AppointmentIdShort { get; set; } = string.Empty;
    public DateTime FechaCreacionUtc { get; set; }
    public DateTime FechaSesionUtc { get; set; }
    public string EstadoCita { get; set; } = string.Empty;
    public decimal MontoCita { get; set; }
    public string? Pasarela { get; set; }
    public string? EstadoPago { get; set; }
    public decimal? MontoPago { get; set; }
    public int? CodigoRespuesta { get; set; }
    public DateTime? FechaPagoUtc { get; set; }
    public string? RespuestaCrudaParcial { get; set; }

    /// <summary>True when FechaCreacionUtc looks unreliable (default/too-old value) and a different date
    /// (payment or session) had to be used to decide whether this row belongs in the selected range.</summary>
    public bool FechaSospechosa { get; set; }
}

public class ReportePagosSummaryRow
{
    public string EstadoCita { get; set; } = string.Empty;
    public string? EstadoPago { get; set; }
    public string? Pasarela { get; set; }
    public int Total { get; set; }
}

public class ReportePagosViewModel
{
    public int Days { get; set; }
    public List<ReportePagosRow> Rows { get; set; } = new();
    public List<ReportePagosSummaryRow> Summary { get; set; } = new();
    public int TestAppointmentsCount { get; set; }
    public int FechasSospechosasCount { get; set; }
}
