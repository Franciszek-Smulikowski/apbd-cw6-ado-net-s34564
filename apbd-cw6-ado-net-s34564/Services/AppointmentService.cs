using System.Data;
using System.Text;
using apbd_cw6_ado_net_s34564.DTOs;
using Microsoft.Data.SqlClient;

namespace apbd_cw6_ado_net_s34564.Services;

public class AppointmentService : IAppointmentService
{
    private readonly string _connectionString;

    public AppointmentService(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' is missing.");
    }

    public async Task<IEnumerable<AppointmentListDto>> GetAppointmentsAsync(
        string? status,
        string? patientLastName,
        CancellationToken cancellationToken)
    {
        var appointments = new List<AppointmentListDto>();
        var query = new StringBuilder("""
            SELECT
                a.IdAppointment,
                a.[Date],
                a.[Status],
                p.IdPatient,
                p.FirstName AS PatientFirstName,
                p.LastName AS PatientLastName,
                d.IdDoctor,
                d.FirstName AS DoctorFirstName,
                d.LastName AS DoctorLastName
            FROM [Appointment] a
            INNER JOIN [Patient] p ON p.IdPatient = a.IdPatient
            INNER JOIN [Doctor] d ON d.IdDoctor = a.IdDoctor
            WHERE 1 = 1
            """);

        await using var connection = new SqlConnection(_connectionString);
        await using var command = new SqlCommand
        {
            Connection = connection,
            CommandText = query.ToString()
        };

        if (!string.IsNullOrWhiteSpace(status))
        {
            query.AppendLine("AND a.[Status] = @Status");
            command.Parameters.Add("@Status", SqlDbType.NVarChar, 50).Value = status;
        }

        if (!string.IsNullOrWhiteSpace(patientLastName))
        {
            query.AppendLine("AND p.LastName LIKE @PatientLastName");
            command.Parameters.Add("@PatientLastName", SqlDbType.NVarChar, 100).Value = $"{patientLastName}%";
        }

        query.AppendLine("ORDER BY a.[Date], a.IdAppointment");
        command.CommandText = query.ToString();

        await connection.OpenAsync(cancellationToken);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            appointments.Add(new AppointmentListDto
            {
                IdAppointment = reader.GetInt32(reader.GetOrdinal("IdAppointment")),
                Date = reader.GetDateTime(reader.GetOrdinal("Date")),
                Status = reader.GetString(reader.GetOrdinal("Status")),
                IdPatient = reader.GetInt32(reader.GetOrdinal("IdPatient")),
                PatientFirstName = reader.GetString(reader.GetOrdinal("PatientFirstName")),
                PatientLastName = reader.GetString(reader.GetOrdinal("PatientLastName")),
                IdDoctor = reader.GetInt32(reader.GetOrdinal("IdDoctor")),
                DoctorFirstName = reader.GetString(reader.GetOrdinal("DoctorFirstName")),
                DoctorLastName = reader.GetString(reader.GetOrdinal("DoctorLastName"))
            });
        }

        return appointments;
    }

    public async Task<AppointmentDetailsDto?> GetAppointmentAsync(
        int idAppointment,
        CancellationToken cancellationToken)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        var appointment = await GetAppointmentDetailsAsync(connection, idAppointment, cancellationToken);
        if (appointment is null)
        {
            return null;
        }

        appointment.Services = await GetAppointmentServicesAsync(connection, idAppointment, cancellationToken);
        return appointment;
    }

    private static async Task<AppointmentDetailsDto?> GetAppointmentDetailsAsync(
        SqlConnection connection,
        int idAppointment,
        CancellationToken cancellationToken)
    {
        const string query = """
            SELECT
                a.IdAppointment,
                a.[Date],
                a.[Status],
                p.IdPatient,
                p.FirstName AS PatientFirstName,
                p.LastName AS PatientLastName,
                p.DateOfBirth,
                d.IdDoctor,
                d.FirstName AS DoctorFirstName,
                d.LastName AS DoctorLastName,
                d.Email
            FROM [Appointment] a
            INNER JOIN [Patient] p ON p.IdPatient = a.IdPatient
            INNER JOIN [Doctor] d ON d.IdDoctor = a.IdDoctor
            WHERE a.IdAppointment = @IdAppointment
            """;

        await using var command = new SqlCommand(query, connection);
        command.Parameters.Add("@IdAppointment", SqlDbType.Int).Value = idAppointment;

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return new AppointmentDetailsDto
        {
            IdAppointment = reader.GetInt32(reader.GetOrdinal("IdAppointment")),
            Date = reader.GetDateTime(reader.GetOrdinal("Date")),
            Status = reader.GetString(reader.GetOrdinal("Status")),
            Patient = new PatientDto
            {
                IdPatient = reader.GetInt32(reader.GetOrdinal("IdPatient")),
                FirstName = reader.GetString(reader.GetOrdinal("PatientFirstName")),
                LastName = reader.GetString(reader.GetOrdinal("PatientLastName")),
                DateOfBirth = GetNullableDateTime(reader, "DateOfBirth")
            },
            Doctor = new DoctorDto
            {
                IdDoctor = reader.GetInt32(reader.GetOrdinal("IdDoctor")),
                FirstName = reader.GetString(reader.GetOrdinal("DoctorFirstName")),
                LastName = reader.GetString(reader.GetOrdinal("DoctorLastName")),
                Email = GetNullableString(reader, "Email")
            }
        };
    }

    private static async Task<List<AppointmentServiceDto>> GetAppointmentServicesAsync(
        SqlConnection connection,
        int idAppointment,
        CancellationToken cancellationToken)
    {
        const string query = """
            SELECT
                s.IdService,
                s.[Name],
                aps.ServiceFee
            FROM [Appointment_Service] aps
            INNER JOIN [Service] s ON s.IdService = aps.IdService
            WHERE aps.IdAppointment = @IdAppointment
            ORDER BY s.[Name]
            """;

        var services = new List<AppointmentServiceDto>();
        await using var command = new SqlCommand(query, connection);
        command.Parameters.Add("@IdAppointment", SqlDbType.Int).Value = idAppointment;

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            services.Add(new AppointmentServiceDto
            {
                IdService = reader.GetInt32(reader.GetOrdinal("IdService")),
                Name = reader.GetString(reader.GetOrdinal("Name")),
                ServiceFee = GetNullableDecimal(reader, "ServiceFee")
            });
        }

        return services;
    }

    private static string? GetNullableString(SqlDataReader reader, string columnName)
    {
        var ordinal = reader.GetOrdinal(columnName);
        return reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);
    }

    private static DateTime? GetNullableDateTime(SqlDataReader reader, string columnName)
    {
        var ordinal = reader.GetOrdinal(columnName);
        return reader.IsDBNull(ordinal) ? null : reader.GetDateTime(ordinal);
    }

    private static decimal? GetNullableDecimal(SqlDataReader reader, string columnName)
    {
        var ordinal = reader.GetOrdinal(columnName);
        return reader.IsDBNull(ordinal) ? null : reader.GetDecimal(ordinal);
    }
}
