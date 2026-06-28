using apbd_cw6_ado_net_s34564.DTOs;

namespace apbd_cw6_ado_net_s34564.Services;

public interface IAppointmentService
{
    Task<IEnumerable<AppointmentListDto>> GetAppointmentsAsync(
        string? status,
        string? patientLastName,
        CancellationToken cancellationToken);

    Task<AppointmentDetailsDto?> GetAppointmentAsync(
        int idAppointment,
        CancellationToken cancellationToken);
}
