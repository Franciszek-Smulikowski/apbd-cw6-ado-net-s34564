namespace apbd_cw6_ado_net_s34564.DTOs;

public class AppointmentDetailsDto
{
    public int IdAppointment { get; set; }
    public DateTime Date { get; set; }
    public string Status { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public string? InternalNotes { get; set; }
    public PatientDto Patient { get; set; } = new();
    public DoctorDto Doctor { get; set; } = new();
    public List<AppointmentServiceDto> Services { get; set; } = [];
}

public class PatientDto
{
    public int IdPatient { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public DateTime? DateOfBirth { get; set; }
}

public class DoctorDto
{
    public int IdDoctor { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string? Email { get; set; }
}

public class AppointmentServiceDto
{
    public int IdService { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal? ServiceFee { get; set; }
}
