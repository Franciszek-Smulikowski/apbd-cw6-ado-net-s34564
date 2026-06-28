namespace apbd_cw6_ado_net_s34564.DTOs;

public class UpdateAppointmentRequestDto
{
    public DateTime AppointmentDate { get; set; }
    public int IdPatient { get; set; }
    public int IdDoctor { get; set; }
    public string Status { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public string? InternalNotes { get; set; }
}
