using CentralPsi.Web.Models.Entities;

namespace CentralPsi.Web.Services;

public interface INotificationService
{
    Task SendProfessionalVerifiedAsync(Professional professional);
    Task SendProfessionalRejectedAsync(Professional professional, string reason);
    Task SendAppointmentConfirmedAsync(Appointment appointment, Professional professional);
    Task SendAttendanceConfirmationRequestAsync(Appointment appointment, Professional professional);
    Task SendCancellationRefundNoticeAsync(Appointment appointment, Professional professional, CancellationRequest request);
    Task SendFonasaConfirmationRequestAsync(Professional professional);
}
