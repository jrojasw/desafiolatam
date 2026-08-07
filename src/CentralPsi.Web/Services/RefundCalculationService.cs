using CentralPsi.Web.Models.Entities;
using CentralPsi.Web.Options;
using Microsoft.Extensions.Options;

namespace CentralPsi.Web.Services;

/// <summary>
/// Refund policy: 100% if cancelled 12+ hours ahead, 50% if cancelled between 12 and 0 hours ahead,
/// not eligible for automatic calculation once the appointment time has passed (admin reviews manually).
/// Refunds are always processed manually by the finance team, within a maximum of 4 business days.
/// </summary>
public class RefundCalculationService : IRefundCalculationService
{
    private readonly AppOptions _options;

    public RefundCalculationService(IOptions<AppOptions> options)
    {
        _options = options.Value;
    }

    public (RefundTier Tier, decimal Amount, double HoursBefore) Calculate(DateTime appointmentStartUtc, decimal appointmentAmount, DateTime? nowUtc = null)
    {
        var now = nowUtc ?? DateTime.UtcNow;
        var hoursBefore = (appointmentStartUtc - now).TotalHours;

        if (hoursBefore >= _options.FullRefundThresholdHours)
        {
            return (RefundTier.Full100, appointmentAmount, hoursBefore);
        }

        if (hoursBefore > 0)
        {
            return (RefundTier.Partial50, Math.Round(appointmentAmount * 0.5m, 0), hoursBefore);
        }

        return (RefundTier.NotEligible, 0m, hoursBefore);
    }
}
