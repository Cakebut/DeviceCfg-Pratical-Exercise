# DeviceCfg

A C# console application that reads device configuration data from a SQL Server database and displays it in a readable table.

The application also supports querying a specific device by ID and performs application-level validation to identify missing or nonsensical configuration values.

## 1. Requirements

A clean Windows machine should have the following installed:

- Windows 10/11
- Docker Desktop
- .NET SDK 10.0 or later
- SQL Server Management Studio (SSMS) — recommended for running the database setup script

Docker is used to run SQL Server locally, so a separate SQL Server installation is not required.

---

## 2. Project Structure

The important files are:

```text
DeviceCfg/
├── Program.cs
├── DeviceCfg.csproj
├── README.md
├── NOTES.md
├── AI_NOTES.md
└── 01-setup.sql
```

`01-setup.sql` creates the SQL Server database, table, and sample data used by the application.

> **Important:** The setup script drops and recreates `dbo.DeviceConfig`. Running it again resets the sample data.

---

# 3. Install .NET SDK

Install the .NET SDK from Microsoft's official .NET website.

After installation, open a **new PowerShell window** and verify:

```powershell
dotnet --version
```

Example:

```text
10.0.401
```

The application was developed and tested with .NET SDK 10.

---

# 4. Install and Start Docker Desktop

Install Docker Desktop and make sure Docker is running.

Verify Docker from PowerShell:

```powershell
docker --version
```

Then:

```powershell
docker ps
```

Docker should return a list of running containers.

---

# 5. Start SQL Server Using Docker

Create a SQL Server 2022 Developer Edition container:

```powershell
docker run -e "ACCEPT_EULA=Y" `
  -e "MSSQL_SA_PASSWORD=<YOUR_SQL_PASSWORD>" `
  -p 1433:1433 `
  --name deviceconfig-sql `
  -d mcr.microsoft.com/mssql/server:2022-latest
```

Replace `<YOUR_SQL_PASSWORD>` with a strong local development password.

For example:

```text
MSSQL_SA_PASSWORD=YourStrongPassword123!
```


Check that the container is running:

```powershell
docker ps
```

You should see something similar to:

```text
CONTAINER ID   IMAGE                                        PORTS
xxxxxxxxxxxx   mcr.microsoft.1433/tccom/mssql/server:2022-latest  0.0.0.0:1433->p
```

The container name should be:

```text
deviceconfig-sql
```

---

# 6. Verify SQL Server

You can verify that SQL Server has started successfully with:

```powershell
docker exec -it deviceconfig-sql /opt/mssql-tools18/bin/sqlcmd `
  -S localhost `
  -U sa `
  -P '<YOUR_SQL_PASSWORD>' `
  -C `
  -Q "SELECT @@VERSION AS SqlServerVersion;"
```

The command should return information about SQL Server 2022 Developer Edition.

If SQL Server has only just started, wait a few seconds and try again.

---

# 7. Create the Database and Sample Data

Open SQL Server Management Studio (SSMS).

Connect using:

```text
Server: localhost,1433
Authentication: SQL Server Authentication
Login: sa
Password: <YOUR_SQL_PASSWORD>
```

For local development, encryption can be configured as:

```text
Encryption: Mandatory
Trust server certificate: Checked
```

Open:

```text
01-setup.sql
```

Run the entire script.

The script creates:

```text
DeviceConfigDB
└── dbo.DeviceConfig
```

The table contains:

| Column | Type |
|---|---|
| DeviceId | INT |
| ZoneId | INT |
| DeviceType | VARCHAR(10) |
| DeviceName | VARCHAR(50) |
| IpAddress | VARCHAR(20) |
| Port | INT |
| Channel | INT |
| Enabled | SMALLINT |
| FirmwareVersion | FLOAT |
| UpdateMode | VARCHAR(10) |
| PollIntervalMs | INT |
| LastSeenAt | DATETIME |

The script also inserts 10 sample devices.

---

# 8. Sample Data and Deliberate Problems

The sample database intentionally contains imperfect data.

Some examples include:

| Device | Issue |
|---|---|
| Device 4 | Missing IP address |
| Device 6 | Invalid/negative channel |
| Device 7 | Polling interval is `0` |
| Device 8 | Missing `LastSeenAt` |
| Device 9 | Missing `UpdateMode` |
| Device 5 | Disabled device (`Enabled = 0`), which is valid |

These values are intentionally included to test application-level validation.

The application should not silently convert invalid or missing values into seemingly valid defaults.

---

# 9. Install the SQL Client Package

From the project directory:

```powershell
cd DeviceCfg
```

Install the Microsoft SQL Server client library:

```powershell
dotnet add package Microsoft.Data.SqlClient
```

Restore dependencies:

```powershell
dotnet restore
```

Build the application:

```powershell
dotnet build
```

A successful build should report:

```text
Build succeeded.
```

---

# 10. Configure the Database Connection

The application reads the database connection string from an environment variable rather than storing the database password directly in the source code.

In the **same PowerShell window that will run the application**, set:

```powershell
$env:DEVICECFG_CONNECTION_STRING = "Server=localhost,1433;Database=DeviceConfigDB;User Id=sa;Password=<YOUR_SQL_PASSWORD>;Encrypt=True;TrustServerCertificate=True;"
```

For example:

```powershell
$env:DEVICECFG_CONNECTION_STRING = "Server=localhost,1433;Database=DeviceConfigDB;User Id=sa;Password=YourStrongPassword123!;Encrypt=True;TrustServerCertificate=True;"
```

Verify that the variable exists:

```powershell
if ($env:DEVICECFG_CONNECTION_STRING) {
    "Connection string is set."
} else {
    "Connection string is missing."
}
```

Expected output:

```text
Connection string is set.
```

### Important

PowerShell environment variables created using `$env:` are normally limited to the current PowerShell session.

If a new PowerShell window is opened, set the variable again.

---

# 11. Run the Application

From the project directory:

```powershell
dotnet run
```

Without a device ID, the application displays all device configurations.

The output is presented as a readable table and includes a validation status for each device.

---

# 12. Query a Specific Device

Pass the device ID as a command-line argument.

For example:

```powershell
dotnet run -- 3
```

This requests:

```text
DeviceId = 3
```

The application should display only that device's configuration and its validation status.

---

# 13. Recommended Tests

The following commands exercise the main application behaviours.

### Display all devices

```powershell
dotnet run
```

### Valid device ID

```powershell
dotnet run -- 3
```

### Device containing a missing IP

```powershell
dotnet run -- 4
```

### Device containing an invalid channel

```powershell
dotnet run -- 6
```

### Device containing an invalid polling interval

```powershell
dotnet run -- 7
```

### Device containing a missing update mode

```powershell
dotnet run -- 9
```

### Non-existent device

```powershell
dotnet run -- 999
```

The application should clearly indicate that the requested device does not exist.

### Invalid device ID

```powershell
dotnet run -- abc
```

The application should report that the device ID is invalid.

### Negative device ID

```powershell
dotnet run -- -1
```

The application should reject the value rather than querying the database.

---

# 14. Testing Database Failure Handling

The application should report a clear database error if SQL Server is unavailable.

First stop the container:

```powershell
docker stop deviceconfig-sql
```

Then run:

```powershell
dotnet run
```

The application should report that it could not connect to the database rather than crashing with an unexplained exception.

Start SQL Server again:

```powershell
docker start deviceconfig-sql
```

Verify:

```powershell
docker ps
```

Then run the application again:

```powershell
dotnet run
```

---

# 15. Application Behaviour

The application performs the following main steps:

1. Reads the database connection string from `DEVICECFG_CONNECTION_STRING`.
2. Validates the command-line argument, if supplied.
3. Opens a connection to SQL Server.
4. Queries `dbo.DeviceConfig`.
5. Uses a parameterized query when a device ID is supplied.
6. Safely reads nullable database values.
7. Converts the database records into C# objects.
8. Validates configuration values.
9. Displays the results in a readable format.
10. Reports configuration, database, input, and data-validation problems clearly.

The application does not silently replace missing values with arbitrary defaults.

---

# 16. Validation Performed

The application checks for issues such as:

- Invalid or missing zone ID
- Missing device type
- Missing device name
- Invalid or missing IP address
- Invalid or missing port
- Negative or missing channel
- `Enabled` values other than `0` or `1`
- Invalid firmware values
- Missing or unrecognised update mode
- Missing or non-positive polling interval
- Missing `LastSeenAt`

Some validation rules are application assumptions because the supplied database schema does not define business limits for every field.

For example, a maximum channel number is not assumed unless such a rule is explicitly specified.

---

# 17. SQL Injection Protection

When querying a specific device, the application uses a SQL parameter rather than directly concatenating the command-line value into SQL.

Conceptually:

```sql
WHERE DeviceId = @DeviceId
```

with the value supplied separately as a SQL parameter.

This prevents the device ID argument from being treated as arbitrary SQL syntax.

---

# 18. Stopping the Environment

When finished, stop the SQL Server container:

```powershell
docker stop deviceconfig-sql
```

The container can be started again later with:

```powershell
docker start deviceconfig-sql
```

If the container is no longer needed, it can be removed with:

```powershell
docker rm deviceconfig-sql
```

If you remove the container, the SQL Server data stored inside that container will also be removed unless persistent storage has been configured.

---

# 19. Recreating the Database

If the sample data needs to be reset, run:

```text
01-setup.sql
```

again in SSMS.

> **Warning:** This script intentionally drops and recreates `dbo.DeviceConfig`, so any changes made to the sample table will be lost.

---

# 20. Running on Another Clean Machine

The basic setup sequence is:

```text
1. Install Docker Desktop
2. Install .NET SDK
3. Clone/copy the DeviceCfg project
4. Start the SQL Server Docker container
5. Run 01-setup.sql in SSMS
6. Set DEVICECFG_CONNECTION_STRING
7. Run dotnet restore
8. Run dotnet build
9. Run dotnet run
```

A typical command sequence is:

```powershell
cd DeviceCfg

dotnet restore
dotnet build

$env:DEVICECFG_CONNECTION_STRING = "Server=localhost,1433;Database=DeviceConfigDB;User Id=sa;Password=<YOUR_SQL_PASSWORD>;Encrypt=True;TrustServerCertificate=True;"

dotnet run
```

To query a particular device:

```powershell
dotnet run -- 3
```

---

# 21. Optional Release Build

A release build can be created with:

```powershell
dotnet publish -c Release -r win-x64 --self-contained false
```

The application can then be found under the project's `bin` directory.

The target machine will still require the appropriate .NET runtime because the build is not self-contained.

---

# 22. Troubleshooting

### `dotnet` is not recognized

The .NET SDK is either not installed or the terminal was opened before installation.

Install the .NET SDK and open a new PowerShell window.

Verify:

```powershell
dotnet --version
```

---

### `DEVICECFG_CONNECTION_STRING is not set`

Set the environment variable in the current PowerShell session:

```powershell
$env:DEVICECFG_CONNECTION_STRING = "Server=localhost,1433;Database=DeviceConfigDB;User Id=sa;Password=<YOUR_SQL_PASSWORD>;Encrypt=True;TrustServerCertificate=True;"
```

Then:

```powershell
dotnet run
```

---

### SQL Server connection fails

Check that the Docker container is running:

```powershell
docker ps
```

If it is stopped:

```powershell
docker start deviceconfig-sql
```

If the container does not exist, recreate it using the Docker command in Section 5.

---

### SQL Server login fails

Verify that the password in the environment variable matches the password used when the Docker container was created.

You can also test the login directly:

```powershell
docker exec -it deviceconfig-sql /opt/mssql-tools18/bin/sqlcmd `
  -S localhost `
  -U sa `
  -P '<YOUR_SQL_PASSWORD>' `
  -C `
  -Q "SELECT 1;"
```

---

### Database or table does not exist

Run:

```text
01-setup.sql
```

again in SSMS.

The application expects:

```text
Database: DeviceConfigDB
Table: dbo.DeviceConfig
```

---

### Port 1433 is already in use

Check which Docker containers are using the port:

```powershell
docker ps
```

If another SQL Server container is already using port 1433, either stop it or use a different host port and update the application's connection string accordingly.

For example:

```powershell
-p 1434:1433
```

would require:

```text
Server=localhost,1434;
```

in the connection string.

---

# 23. Security Notes

This project is intended for a local technical exercise.

The database connection string contains credentials, so:

- Do not commit the connection string to source control.
- Do not commit `.env` files containing credentials.
- Do not use the local development `sa` password for production systems.
- Prefer a dedicated least-privilege database account in a production environment.
- `TrustServerCertificate=True` is used for the local Docker SQL Server setup and should not automatically be copied to a production environment.

The password used during local development should be replaced with your own password when setting up the project on another machine.

---

# 24. Expected Environment

The project was developed using:

```text
Operating System: Windows
.NET SDK: 10.0
Database: SQL Server 2022 Developer Edition
Database Hosting: Docker
Database Client: Microsoft.Data.SqlClient
IDE: Visual Studio Code
Database Management: SQL Server Management Studio
```

The application itself is a C# console application and does not require Visual Studio.