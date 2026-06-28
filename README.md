# apbd-cw6-ado-net-s34564

ASP.NET Core Web API for APBD lab 6.

The application manages clinic appointments stored in a SQL Server database named `ClinicAdoNet`.
Database access is implemented with ADO.NET and `Microsoft.Data.SqlClient`. Entity Framework is not used.

## Requirements

* .NET 9 SDK
* SQL Server, SQL Server Express, or LocalDB

## Database

Before running the application, create and seed the database using:

```powershell
sqlcmd -S .\SQLEXPRESS -E -i Database\01_create_and_seed_clinic.sql
```

If you use LocalDB, change the server name accordingly.

## Configuration

The default connection string is located in `appsettings.json`:

```json
"ConnectionStrings": {
  "DefaultConnection": "Server=.\\SQLEXPRESS;Database=ClinicAdoNet;Trusted_Connection=True;TrustServerCertificate=True;"
}
```

## Running

Build the project:

```powershell
dotnet build
```

Run the API:

```powershell
dotnet run --project .\apbd-cw6-ado-net-s34564\apbd-cw6-ado-net-s34564.csproj --launch-profile http
```

Example HTTP requests are available in:

```text
apbd-cw6-ado-net-s34564/Appointments.http
```
