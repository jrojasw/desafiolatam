using CentralPsi.Web.Models.Entities;

namespace CentralPsi.Web.Services;

public interface IRefundCalculationService
{
    (RefundTier Tier, decimal Amount, double HoursBefore) Calculate(DateTime appointmentStartUtc, decimal appointmentAmount, DateTime? nowUtc = null);
}
