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

    [HttpPost]
    public async Task<ActionResult<AppointmentDetailsDto>> CreateAppointment(
        CreateAppointmentRequestDto request,
        CancellationToken cancellationToken)
    {
        var result = await _appointmentService.CreateAppointmentAsync(request, cancellationToken);
        if (result.Status != AppointmentOperationStatus.Success)
        {
            return ToErrorResult(result);
        }

        return CreatedAtAction(
            nameof(GetAppointment),
            new { idAppointment = result.Value!.IdAppointment },
            result.Value);
    }

    [HttpPut("{idAppointment:int}")]
    public async Task<ActionResult<AppointmentDetailsDto>> UpdateAppointment(
        int idAppointment,
        UpdateAppointmentRequestDto request,
        CancellationToken cancellationToken)
    {
        var result = await _appointmentService.UpdateAppointmentAsync(idAppointment, request, cancellationToken);
        if (result.Status != AppointmentOperationStatus.Success)
        {
            return ToErrorResult(result);
        }

        return Ok(result.Value);
    }

    [HttpDelete("{idAppointment:int}")]
    public async Task<IActionResult> DeleteAppointment(
        int idAppointment,
        CancellationToken cancellationToken)
    {
        var result = await _appointmentService.DeleteAppointmentAsync(idAppointment, cancellationToken);
        if (result.Status != AppointmentOperationStatus.Success)
        {
            return ToErrorResult(result);
        }

        return NoContent();
    }

    private ActionResult ToErrorResult(AppointmentOperationResult result)
    {
        var error = new ErrorDto
        {
            Message = result.ErrorMessage ?? "An error occurred."
        };

        return result.Status switch
        {
            AppointmentOperationStatus.BadRequest => BadRequest(error),
            AppointmentOperationStatus.NotFound => NotFound(error),
            AppointmentOperationStatus.Conflict => Conflict(error),
            _ => BadRequest(error)
        };
    }
}
