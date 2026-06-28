namespace apbd_cw6_ado_net_s34564.DTOs;

public class UpdateAppointmentRequestDto
{
    public DateTime Date { get; set; }
    public string Status { get; set; } = string.Empty;
    public int IdPatient { get; set; }
    public int IdDoctor { get; set; }
    public List<int> ServiceIds { get; set; } = [];
}
