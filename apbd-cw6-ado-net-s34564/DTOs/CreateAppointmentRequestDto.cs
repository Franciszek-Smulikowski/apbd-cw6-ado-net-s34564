namespace apbd_cw6_ado_net_s34564.DTOs;

public class CreateAppointmentRequestDto
{
    public DateTime AppointmentDate { get; set; }
    public int IdPatient { get; set; }
    public int IdDoctor { get; set; }
    public string Reason { get; set; } = string.Empty;
}
