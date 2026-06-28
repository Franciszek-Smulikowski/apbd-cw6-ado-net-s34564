namespace apbd_cw6_ado_net_s34564.DTOs;

public class AppointmentListDto
{
    public int IdAppointment { get; set; }
    public DateTime Date { get; set; }
    public string Status { get; set; } = string.Empty;
    public int IdPatient { get; set; }
    public string PatientFirstName { get; set; } = string.Empty;
    public string PatientLastName { get; set; } = string.Empty;
    public int IdDoctor { get; set; }
    public string DoctorFirstName { get; set; } = string.Empty;
    public string DoctorLastName { get; set; } = string.Empty;
}
