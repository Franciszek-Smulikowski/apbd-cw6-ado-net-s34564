using apbd_cw6_ado_net_s34564.DTOs;
using apbd_cw6_ado_net_s34564.Services;
using Microsoft.AspNetCore.Mvc;

namespace apbd_cw6_ado_net_s34564.Controllers;

[ApiController]
[Route("api/appointments")]
public class AppointmentsController : ControllerBase
{
    private readonly IAppointmentService _appointmentService;

    public AppointmentsController(IAppointmentService appointmentService)
    {
        _appointmentService = appointmentService;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<AppointmentListDto>>> GetAppointments(
        [FromQuery] string? status,
        [FromQuery] string? patientLastName,
        CancellationToken cancellationToken)
    {
        var appointments = await _appointmentService.GetAppointmentsAsync(
            status,
            patientLastName,
            cancellationToken);

        return Ok(appointments);
    }

    [HttpGet("{idAppointment:int}")]
    public async Task<ActionResult<AppointmentDetailsDto>> GetAppointment(
        int idAppointment,
        CancellationToken cancellationToken)
    {
        var appointment = await _appointmentService.GetAppointmentAsync(idAppointment, cancellationToken);
        if (appointment is null)
        {
            return NotFound(new ErrorDto
            {
                Message = $"Appointment with id {idAppointment} was not found."
            });
        }

        return Ok(appointment);
    }
}
