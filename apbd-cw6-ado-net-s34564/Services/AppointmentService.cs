using System.Data;
using System.Text;
using apbd_cw6_ado_net_s34564.DTOs;
using Microsoft.Data.SqlClient;

namespace apbd_cw6_ado_net_s34564.Services;

public class AppointmentService : IAppointmentService
{
    private static readonly HashSet<string> AllowedStatuses = new(StringComparer.OrdinalIgnoreCase)
    {
        "Scheduled",
        "Completed",
        "Cancelled"
    };

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
                a.AppointmentDate AS [Date],
                a.[Status],
                p.IdPatient,
                p.FirstName AS PatientFirstName,
                p.LastName AS PatientLastName,
                d.IdDoctor,
                d.FirstName AS DoctorFirstName,
                d.LastName AS DoctorLastName
            FROM [Appointments] a
            INNER JOIN [Patients] p ON p.IdPatient = a.IdPatient
            INNER JOIN [Doctors] d ON d.IdDoctor = a.IdDoctor
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

        query.AppendLine("ORDER BY a.AppointmentDate, a.IdAppointment");
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

        return appointment;
    }

    public async Task<AppointmentOperationResult<AppointmentDetailsDto>> CreateAppointmentAsync(
        CreateAppointmentRequestDto request,
        CancellationToken cancellationToken)
    {
        var validationError = ValidateCreateRequest(request);
        if (validationError is not null)
        {
            return AppointmentOperationResult<AppointmentDetailsDto>.BadRequest(validationError);
        }

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        if (!await PatientIsActiveAsync(connection, request.IdPatient, cancellationToken))
        {
            return AppointmentOperationResult<AppointmentDetailsDto>.BadRequest("Patient does not exist or is not active.");
        }

        if (!await DoctorIsActiveAsync(connection, request.IdDoctor, cancellationToken))
        {
            return AppointmentOperationResult<AppointmentDetailsDto>.BadRequest("Doctor does not exist or is not active.");
        }

        if (await DoctorHasScheduledAppointmentAsync(connection, request.IdDoctor, request.AppointmentDate, null, cancellationToken))
        {
            return AppointmentOperationResult<AppointmentDetailsDto>.Conflict("Doctor already has a scheduled appointment at this time.");
        }

        const string query = """
            INSERT INTO [Appointments] (IdPatient, IdDoctor, AppointmentDate, [Status], Reason)
            OUTPUT INSERTED.IdAppointment
            VALUES (@IdPatient, @IdDoctor, @Date, @Status, @Reason)
            """;

        await using var command = new SqlCommand(query, connection);
        command.Parameters.Add("@IdPatient", SqlDbType.Int).Value = request.IdPatient;
        command.Parameters.Add("@IdDoctor", SqlDbType.Int).Value = request.IdDoctor;
        command.Parameters.Add("@Date", SqlDbType.DateTime2).Value = request.AppointmentDate;
        command.Parameters.Add("@Status", SqlDbType.NVarChar, 50).Value = "Scheduled";
        command.Parameters.Add("@Reason", SqlDbType.NVarChar, 250).Value = request.Reason.Trim();

        var newId = Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken));
        var appointment = await GetAppointmentDetailsAsync(connection, newId, cancellationToken);

        return AppointmentOperationResult<AppointmentDetailsDto>.Success(appointment!);
    }

    public async Task<AppointmentOperationResult<AppointmentDetailsDto>> UpdateAppointmentAsync(
        int idAppointment,
        UpdateAppointmentRequestDto request,
        CancellationToken cancellationToken)
    {
        var validationError = ValidateUpdateRequest(request);
        if (validationError is not null)
        {
            return AppointmentOperationResult<AppointmentDetailsDto>.BadRequest(validationError);
        }

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        var currentAppointment = await GetCurrentAppointmentStateAsync(connection, idAppointment, cancellationToken);
        if (currentAppointment is null)
        {
            return AppointmentOperationResult<AppointmentDetailsDto>.NotFound($"Appointment with id {idAppointment} was not found.");
        }

        if (!await PatientIsActiveAsync(connection, request.IdPatient, cancellationToken))
        {
            return AppointmentOperationResult<AppointmentDetailsDto>.BadRequest("Patient does not exist or is not active.");
        }

        if (!await DoctorIsActiveAsync(connection, request.IdDoctor, cancellationToken))
        {
            return AppointmentOperationResult<AppointmentDetailsDto>.BadRequest("Doctor does not exist or is not active.");
        }

        var dateChanged = currentAppointment.Date != request.AppointmentDate;
        if (dateChanged
            && (currentAppointment.Status.Equals("Completed", StringComparison.OrdinalIgnoreCase)
                || request.Status.Equals("Completed", StringComparison.OrdinalIgnoreCase)))
        {
            return AppointmentOperationResult<AppointmentDetailsDto>.Conflict("Completed appointments cannot have their date changed.");
        }

        if (dateChanged
            && request.Status.Equals("Scheduled", StringComparison.OrdinalIgnoreCase)
            && await DoctorHasScheduledAppointmentAsync(connection, request.IdDoctor, request.AppointmentDate, idAppointment, cancellationToken))
        {
            return AppointmentOperationResult<AppointmentDetailsDto>.Conflict("Doctor already has a scheduled appointment at this time.");
        }

        const string query = """
            UPDATE [Appointments]
            SET IdPatient = @IdPatient,
                IdDoctor = @IdDoctor,
                AppointmentDate = @Date,
                [Status] = @Status,
                Reason = @Reason,
                InternalNotes = @InternalNotes
            WHERE IdAppointment = @IdAppointment
            """;

        await using var command = new SqlCommand(query, connection);
        command.Parameters.Add("@IdAppointment", SqlDbType.Int).Value = idAppointment;
        command.Parameters.Add("@IdPatient", SqlDbType.Int).Value = request.IdPatient;
        command.Parameters.Add("@IdDoctor", SqlDbType.Int).Value = request.IdDoctor;
        command.Parameters.Add("@Date", SqlDbType.DateTime2).Value = request.AppointmentDate;
        command.Parameters.Add("@Status", SqlDbType.NVarChar, 50).Value = NormalizeStatus(request.Status);
        command.Parameters.Add("@Reason", SqlDbType.NVarChar, 250).Value = request.Reason.Trim();
        command.Parameters.Add("@InternalNotes", SqlDbType.NVarChar, 500).Value =
            string.IsNullOrWhiteSpace(request.InternalNotes) ? DBNull.Value : request.InternalNotes.Trim();

        await command.ExecuteNonQueryAsync(cancellationToken);

        var appointment = await GetAppointmentDetailsAsync(connection, idAppointment, cancellationToken);
        return AppointmentOperationResult<AppointmentDetailsDto>.Success(appointment!);
    }

    public async Task<AppointmentOperationResult> DeleteAppointmentAsync(
        int idAppointment,
        CancellationToken cancellationToken)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        var currentAppointment = await GetCurrentAppointmentStateAsync(connection, idAppointment, cancellationToken);
        if (currentAppointment is null)
        {
            return AppointmentOperationResult.NotFound($"Appointment with id {idAppointment} was not found.");
        }

        if (currentAppointment.Status.Equals("Completed", StringComparison.OrdinalIgnoreCase))
        {
            return AppointmentOperationResult.Conflict("Completed appointments cannot be deleted.");
        }

        const string deleteAppointmentQuery = """
            DELETE FROM [Appointments]
            WHERE IdAppointment = @IdAppointment
            """;

        await using var deleteAppointmentCommand = new SqlCommand(deleteAppointmentQuery, connection);
        deleteAppointmentCommand.Parameters.Add("@IdAppointment", SqlDbType.Int).Value = idAppointment;
        await deleteAppointmentCommand.ExecuteNonQueryAsync(cancellationToken);

        return AppointmentOperationResult.Success();
    }

    private static string? ValidateCreateRequest(CreateAppointmentRequestDto request)
    {
        if (request.AppointmentDate < DateTime.Now)
        {
            return "Appointment date cannot be in the past.";
        }

        return ValidateReason(request.Reason);
    }

    private static string? ValidateUpdateRequest(UpdateAppointmentRequestDto request)
    {
        if (!AllowedStatuses.Contains(request.Status))
        {
            return "Status must be one of: Scheduled, Completed, Cancelled.";
        }

        var reasonError = ValidateReason(request.Reason);
        if (reasonError is not null)
        {
            return reasonError;
        }

        if (request.InternalNotes is not null && request.InternalNotes.Length > 500)
        {
            return "InternalNotes cannot be longer than 500 characters.";
        }

        return null;
    }

    private static string? ValidateReason(string? reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            return "Reason is required.";
        }

        if (reason.Length > 250)
        {
            return "Reason cannot be longer than 250 characters.";
        }

        return null;
    }

    private static string NormalizeStatus(string status)
    {
        return AllowedStatuses.First(allowedStatus => allowedStatus.Equals(status, StringComparison.OrdinalIgnoreCase));
    }

    private static async Task<bool> PatientIsActiveAsync(
        SqlConnection connection,
        int idPatient,
        CancellationToken cancellationToken)
    {
        const string query = """
            SELECT COUNT(1)
            FROM [Patients]
            WHERE IdPatient = @IdPatient AND IsActive = 1
            """;

        await using var command = new SqlCommand(query, connection);
        command.Parameters.Add("@IdPatient", SqlDbType.Int).Value = idPatient;

        return Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken)) > 0;
    }

    private static async Task<bool> DoctorIsActiveAsync(
        SqlConnection connection,
        int idDoctor,
        CancellationToken cancellationToken)
    {
        const string query = """
            SELECT COUNT(1)
            FROM [Doctors]
            WHERE IdDoctor = @IdDoctor AND IsActive = 1
            """;

        await using var command = new SqlCommand(query, connection);
        command.Parameters.Add("@IdDoctor", SqlDbType.Int).Value = idDoctor;

        return Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken)) > 0;
    }

    private static async Task<bool> DoctorHasScheduledAppointmentAsync(
        SqlConnection connection,
        int idDoctor,
        DateTime appointmentDate,
        int? ignoredAppointmentId,
        CancellationToken cancellationToken)
    {
        var query = new StringBuilder("""
            SELECT COUNT(1)
            FROM [Appointments]
            WHERE IdDoctor = @IdDoctor
              AND AppointmentDate = @Date
              AND [Status] = @Status
            """);

        await using var command = new SqlCommand
        {
            Connection = connection
        };

        command.Parameters.Add("@IdDoctor", SqlDbType.Int).Value = idDoctor;
        command.Parameters.Add("@Date", SqlDbType.DateTime2).Value = appointmentDate;
        command.Parameters.Add("@Status", SqlDbType.NVarChar, 50).Value = "Scheduled";

        if (ignoredAppointmentId is not null)
        {
            query.AppendLine();
            query.AppendLine("AND IdAppointment <> @IgnoredAppointmentId");
            command.Parameters.Add("@IgnoredAppointmentId", SqlDbType.Int).Value = ignoredAppointmentId.Value;
        }

        command.CommandText = query.ToString();
        return Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken)) > 0;
    }

    private static async Task<CurrentAppointmentState?> GetCurrentAppointmentStateAsync(
        SqlConnection connection,
        int idAppointment,
        CancellationToken cancellationToken)
    {
        const string query = """
            SELECT AppointmentDate AS [Date], [Status]
            FROM [Appointments]
            WHERE IdAppointment = @IdAppointment
            """;

        await using var command = new SqlCommand(query, connection);
        command.Parameters.Add("@IdAppointment", SqlDbType.Int).Value = idAppointment;

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return new CurrentAppointmentState(
            reader.GetDateTime(reader.GetOrdinal("Date")),
            reader.GetString(reader.GetOrdinal("Status")));
    }

    private static async Task<AppointmentDetailsDto?> GetAppointmentDetailsAsync(
        SqlConnection connection,
        int idAppointment,
        CancellationToken cancellationToken)
    {
        const string query = """
            SELECT
                a.IdAppointment,
                a.AppointmentDate AS [Date],
                a.[Status],
                a.Reason,
                a.InternalNotes,
                p.IdPatient,
                p.FirstName AS PatientFirstName,
                p.LastName AS PatientLastName,
                p.DateOfBirth,
                d.IdDoctor,
                d.FirstName AS DoctorFirstName,
                d.LastName AS DoctorLastName,
                d.LicenseNumber
            FROM [Appointments] a
            INNER JOIN [Patients] p ON p.IdPatient = a.IdPatient
            INNER JOIN [Doctors] d ON d.IdDoctor = a.IdDoctor
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
            Reason = reader.GetString(reader.GetOrdinal("Reason")),
            InternalNotes = GetNullableString(reader, "InternalNotes"),
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
                LicenseNumber = reader.GetString(reader.GetOrdinal("LicenseNumber"))
            }
        };
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

    private sealed record CurrentAppointmentState(DateTime Date, string Status);
}
