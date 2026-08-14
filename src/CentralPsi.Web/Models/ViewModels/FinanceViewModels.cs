namespace CentralPsi.Web.Models.ViewModels;

public class FinanceSummaryViewModel
{
    public string Range { get; set; } = "mes";
    public decimal TaxRatePercent { get; set; }

    public int SessionCount { get; set; }
    public decimal TotalRevenue { get; set; }
    public decimal TotalProfessionalPayouts { get; set; }
    public decimal TotalRefunds { get; set; }
    public decimal GrossMargin => TotalRevenue - TotalProfessionalPayouts - TotalRefunds;
    public decimal EstimatedTax => GrossMargin > 0 ? Math.Round(GrossMargin * TaxRatePercent / 100m, 0) : 0m;
    public decimal EstimatedNetProfit => GrossMargin - EstimatedTax;

    public List<FinanceDayPoint> DailySeries { get; set; } = new();
    public List<FinanceMonthPoint> MonthlySeries { get; set; } = new();
}

public class FinanceDayPoint
{
    public DateTime DateLocal { get; set; }
    public decimal Revenue { get; set; }
    public decimal Payouts { get; set; }
    public decimal Refunds { get; set; }
    public decimal Margin => Revenue - Payouts - Refunds;
}

public class FinanceMonthPoint
{
    public int Year { get; set; }
    public int Month { get; set; }
    public decimal Revenue { get; set; }
    public decimal Payouts { get; set; }
    public decimal Refunds { get; set; }
    public decimal Margin => Revenue - Payouts - Refunds;
    public decimal TaxRatePercent { get; set; }
    public decimal EstimatedTax => Margin > 0 ? Math.Round(Margin * TaxRatePercent / 100m, 0) : 0m;
    public decimal EstimatedNetProfit => Margin - EstimatedTax;
    public string Label => new DateTime(Year, Month, 1).ToString("MMM yyyy", new System.Globalization.CultureInfo("es-CL"));
}
