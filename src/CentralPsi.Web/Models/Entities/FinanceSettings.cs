namespace CentralPsi.Web.Models.Entities;

/// <summary>
/// Singleton settings row (always Id=1) for the finance panel - just the estimated tax rate applied to the
/// gross margin for now, editable by the admin directly in the panel since it depends on a tax structure
/// (SII Inicio de Actividades) that isn't finalized yet and will change.
/// </summary>
public class FinanceSettings
{
    public int Id { get; set; } = 1;

    /// <summary>Percentage (0-100) applied to gross margin to estimate taxes owed - explicitly labeled as an
    /// estimate in the UI, not a real tax calculation.</summary>
    public decimal TaxRatePercent { get; set; } = 0m;
}
