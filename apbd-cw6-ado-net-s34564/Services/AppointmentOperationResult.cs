namespace apbd_cw6_ado_net_s34564.Services;

public enum AppointmentOperationStatus
{
    Success,
    BadRequest,
    NotFound,
    Conflict
}

public class AppointmentOperationResult
{
    public AppointmentOperationStatus Status { get; init; }
    public string? ErrorMessage { get; init; }

    public static AppointmentOperationResult Success()
    {
        return new AppointmentOperationResult { Status = AppointmentOperationStatus.Success };
    }

    public static AppointmentOperationResult BadRequest(string message)
    {
        return new AppointmentOperationResult
        {
            Status = AppointmentOperationStatus.BadRequest,
            ErrorMessage = message
        };
    }

    public static AppointmentOperationResult NotFound(string message)
    {
        return new AppointmentOperationResult
        {
            Status = AppointmentOperationStatus.NotFound,
            ErrorMessage = message
        };
    }

    public static AppointmentOperationResult Conflict(string message)
    {
        return new AppointmentOperationResult
        {
            Status = AppointmentOperationStatus.Conflict,
            ErrorMessage = message
        };
    }
}

public class AppointmentOperationResult<T> : AppointmentOperationResult
{
    public T? Value { get; init; }

    public static AppointmentOperationResult<T> Success(T value)
    {
        return new AppointmentOperationResult<T>
        {
            Status = AppointmentOperationStatus.Success,
            Value = value
        };
    }

    public new static AppointmentOperationResult<T> BadRequest(string message)
    {
        return new AppointmentOperationResult<T>
        {
            Status = AppointmentOperationStatus.BadRequest,
            ErrorMessage = message
        };
    }

    public new static AppointmentOperationResult<T> NotFound(string message)
    {
        return new AppointmentOperationResult<T>
        {
            Status = AppointmentOperationStatus.NotFound,
            ErrorMessage = message
        };
    }

    public new static AppointmentOperationResult<T> Conflict(string message)
    {
        return new AppointmentOperationResult<T>
        {
            Status = AppointmentOperationStatus.Conflict,
            ErrorMessage = message
        };
    }
}
