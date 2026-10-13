// =============================================================================
// DeviceCfg - reads device configuration from SQL Server and prints it.
//
// Usage:
//   DeviceCfg            -> prints ALL devices as a table
//   DeviceCfg 3          -> prints only the details of device 3
//
// Before running, set the connection string in an environment variable:
//   Windows (cmd):  set DEVICECFG_CONNECTION_STRING=Server=...;Database=...;...
//   Windows (PS):   $env:DEVICECFG_CONNECTION_STRING = "Server=...;Database=...;..."
//
// Required NuGet package:  Microsoft.Data.SqlClient
//
// Exit codes:  0 = success,  1 = something went wrong (so scripts can detect it)
// =============================================================================

using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using Microsoft.Data.SqlClient;

class Program
{
    // ---------------------------------------------------------------------
    // Settings you may want to change
    // ---------------------------------------------------------------------

    // The table we read from.
    const string TableName = "dbo.DeviceConfig";

    // A device not seen for more than this many days is reported as "stale".
    const int StaleAfterDays = 30;

    // Highest channel number accepted when we do not know the real limit.
    // 255 only catches obvious garbage. Add confirmed limits per type below.
    const int DefaultMaxChannel = 255;

    // Optional per-device-type channel limits. Only add a type here once you
    // have confirmed its real limit from the hardware documentation.
    // Example:  { "CAM", 31 },
    static readonly Dictionary<string, int> MaxChannelByType =
        new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
        };

    // The update modes we consider valid (upper/lower case does not matter).
    static readonly HashSet<string> AllowedModes =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "POLL", "EVENT", "MANUAL"
        };

    static int Main(string[] args)
    {
        // Step 1: get the connection string from the environment.
        string? connectionString =
            Environment.GetEnvironmentVariable("DEVICECFG_CONNECTION_STRING");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            Console.Error.WriteLine("Configuration error: DEVICECFG_CONNECTION_STRING is not set.");
            Console.Error.WriteLine("Set the environment variable before running the application.");
            return 1;
        }

        // Step 2: check the command-line arguments.
        // No argument = show all devices. One argument = show that device only.
        if (args.Length > 1)
        {
            Console.Error.WriteLine("Usage: DeviceCfg [DeviceId]");
            return 1;
        }

        int? requestedId = null; // "int?" means "a number, or nothing at all"

        if (args.Length == 1)
        {
            bool isNumber = int.TryParse(args[0], out int parsedId);

            if (!isNumber || parsedId <= 0)
            {
                Console.Error.WriteLine(
                    "Invalid DeviceId. Please provide a positive whole number, e.g. DeviceCfg 3.");
                return 1;
            }

            requestedId = parsedId;
        }

        // Step 3: read from the database and print. Anything that goes wrong
        // is caught below and shown as a friendly message.
        try
        {
            List<DeviceConfig> devices = ReadDevices(connectionString, requestedId);

            if (requestedId.HasValue && devices.Count == 0)
            {
                Console.Error.WriteLine($"Device with ID {requestedId.Value} was not found.");
                return 1;
            }

            if (devices.Count == 0)
            {
                Console.WriteLine("No device configurations were found.");
                return 0;
            }

            DateTime now = DateTime.Now;

            if (requestedId.HasValue)
            {
                PrintDetails(devices[0], now);
            }
            else
            {
                PrintTable(devices, now);
            }

            return 0;
        }
        catch (SqlException ex)
        {
            // Problems talking to SQL Server (server down, wrong password, ...)
            Console.Error.WriteLine("Database error: Could not read device configuration.");
            Console.Error.WriteLine(
                "Check that SQL Server is running, the database exists, " +
                "and the connection settings are correct.");
            Console.Error.WriteLine($"Details: {ex.Message}");
            return 1;
        }
        catch (InvalidOperationException ex)
        {
            // For example: a badly formatted connection string.
            Console.Error.WriteLine($"Application/database error: {ex.Message}");
            return 1;
        }
        catch (Exception ex)
        {
            // Safety net for anything we did not expect.
            Console.Error.WriteLine($"Unexpected error ({ex.GetType().Name}): {ex.Message}");
            return 1;
        }
    }

    // ---------------------------------------------------------------------
    // Database access
    // ---------------------------------------------------------------------

    // Reads devices from SQL Server. If deviceId has a value, only that
    // device is returned; otherwise all devices are returned.
    static List<DeviceConfig> ReadDevices(string connectionString, int? deviceId)
    {
        var devices = new List<DeviceConfig>();

        // "using" closes the connection/command/reader automatically when done.
        using var connection = new SqlConnection(connectionString);
        connection.Open();

        string sql = $@"
            SELECT DeviceId, ZoneId, DeviceType, DeviceName, IpAddress,
                   Port, Channel, Enabled, FirmwareVersion, UpdateMode,
                   PollIntervalMs, LastSeenAt
            FROM {TableName}";

        if (deviceId.HasValue)
        {
            sql += " WHERE DeviceId = @DeviceId";
        }

        sql += " ORDER BY DeviceId;";

        using var command = new SqlCommand(sql, connection);

        if (deviceId.HasValue)
        {
            // A parameter (instead of pasting the number into the text) is the
            // safe way to pass values to SQL.
            command.Parameters.Add("@DeviceId", SqlDbType.Int).Value = deviceId.Value;
        }

        using var reader = command.ExecuteReader();

        // Look up each column's position once, before the loop.
        int colId = reader.GetOrdinal("DeviceId");
        int colZone = reader.GetOrdinal("ZoneId");
        int colType = reader.GetOrdinal("DeviceType");
        int colName = reader.GetOrdinal("DeviceName");
        int colIp = reader.GetOrdinal("IpAddress");
        int colPort = reader.GetOrdinal("Port");
        int colChannel = reader.GetOrdinal("Channel");
        int colEnabled = reader.GetOrdinal("Enabled");
        int colFirmware = reader.GetOrdinal("FirmwareVersion");
        int colMode = reader.GetOrdinal("UpdateMode");
        int colPoll = reader.GetOrdinal("PollIntervalMs");
        int colSeen = reader.GetOrdinal("LastSeenAt");

        // Read one row at a time. Columns that are NOT NULL in the table can be
        // read directly; columns that allow NULL use the helper methods below.
        while (reader.Read())
        {
            var device = new DeviceConfig
            {
                DeviceId = reader.GetInt32(colId),
                ZoneId = reader.GetInt32(colZone),
                DeviceType = reader.GetString(colType),
                DeviceName = reader.GetString(colName),
                IpAddress = GetStringOrNull(reader, colIp),
                Port = GetIntOrNull(reader, colPort),
                Channel = GetIntOrNull(reader, colChannel),
                Enabled = reader.GetInt16(colEnabled),
                FirmwareVersion = GetDoubleOrNull(reader, colFirmware),
                UpdateMode = GetStringOrNull(reader, colMode),
                PollIntervalMs = GetIntOrNull(reader, colPoll),
                LastSeenAt = GetDateTimeOrNull(reader, colSeen)
            };

            devices.Add(device);
        }

        return devices;
    }

    // Helpers: return null when the database value is NULL.
    static string? GetStringOrNull(SqlDataReader reader, int column) =>
        reader.IsDBNull(column) ? null : reader.GetString(column);

    static int? GetIntOrNull(SqlDataReader reader, int column) =>
        reader.IsDBNull(column) ? null : reader.GetInt32(column);

    static double? GetDoubleOrNull(SqlDataReader reader, int column) =>
        reader.IsDBNull(column) ? null : reader.GetDouble(column);

    static DateTime? GetDateTimeOrNull(SqlDataReader reader, int column) =>
        reader.IsDBNull(column) ? null : reader.GetDateTime(column);

    // ---------------------------------------------------------------------
    // Printing: all devices as a table
    // ---------------------------------------------------------------------

    static void PrintTable(List<DeviceConfig> devices, DateTime now)
    {
        // The header text. The number after the comma is the column width
        // (negative = left-aligned, positive = right-aligned).
        string header =
            $"{"ID",-4} {"Zone",-5} {"Type",-6} {"Device Name",-23} " +
            $"{"IP Address",-16} {"Port",5} {"Channel",7} {"On",3} " +
            $"{"Firmware",8} {"Mode",-7} {"Poll ms",7} {"Last Seen",-19} {"Status",-10}";

        // The separator line is exactly as wide as the header.
        string separator = new string('-', header.Length);

        Console.WriteLine($"Device configuration from SQL Server ({devices.Count} rows)");
        Console.WriteLine();
        Console.WriteLine(separator);
        Console.WriteLine(header);
        Console.WriteLine(separator);

        // We collect devices with problems here, to list them after the table.
        var flaggedDevices = new List<DeviceConfig>();
        var flaggedIssues = new List<List<string>>();

        foreach (DeviceConfig device in devices)
        {
            List<string> issues = Validate(device, now);

            if (issues.Count > 0)
            {
                flaggedDevices.Add(device);
                flaggedIssues.Add(issues);
            }

            // Print the row. Long text is shortened with Shorten() so it can
            // never push the other columns out of line.
            Console.Write(
                $"{device.DeviceId,-4} {device.ZoneId,-5} " +
                $"{Shorten(device.DeviceType, 6),-6} {Shorten(device.DeviceName, 23),-23} " +
                $"{Shorten(device.IpAddress ?? "NULL", 16),-16} " +
                $"{FormatInt(device.Port),5} {FormatInt(device.Channel),7} " +
                $"{device.Enabled,3} {FormatDouble(device.FirmwareVersion),8} " +
                $"{Shorten(device.UpdateMode ?? "NULL", 7),-7} " +
                $"{FormatInt(device.PollIntervalMs),7} " +
                $"{FormatDate(device.LastSeenAt),-19} ");

            // The last column: green "OK" or yellow "! N issue(s)".
            if (issues.Count == 0)
            {
                WriteColoredLine("OK", ConsoleColor.Green);
            }
            else
            {
                string word = issues.Count == 1 ? "issue" : "issues";
                WriteColoredLine($"! {issues.Count} {word}", ConsoleColor.Yellow);
            }
        }

        Console.WriteLine(separator);

        // Warnings are listed separately, grouped by device, so the table
        // itself stays easy to read.
        if (flaggedDevices.Count > 0)
        {
            Console.WriteLine();
            WriteColoredLine("WARNINGS", ConsoleColor.Yellow);
            Console.WriteLine(new string('=', 60));

            for (int i = 0; i < flaggedDevices.Count; i++)
            {
                DeviceConfig device = flaggedDevices[i];

                Console.WriteLine();
                WriteColoredLine(
                    $"Device {device.DeviceId}: {device.DeviceName} ({device.DeviceType})",
                    ConsoleColor.Yellow);

                foreach (string issue in flaggedIssues[i])
                {
                    Console.WriteLine($"    - {issue}");
                }
            }
        }

        // One-line summary at the end.
        int okCount = devices.Count - flaggedDevices.Count;

        Console.WriteLine();
        Console.Write("Summary: ");
        WriteColored($"{okCount} OK", ConsoleColor.Green);
        Console.Write(", ");
        WriteColored(
            $"{flaggedDevices.Count} with warnings",
            flaggedDevices.Count > 0 ? ConsoleColor.Yellow : ConsoleColor.Green);
        Console.WriteLine($" (of {devices.Count} devices)");
        Console.WriteLine("Validation checks are basic checks, not proof that a device is operational.");
    }

    // ---------------------------------------------------------------------
    // Printing: one device in detail
    // ---------------------------------------------------------------------

    static void PrintDetails(DeviceConfig device, DateTime now)
    {
        Console.WriteLine($"Device configuration: {device.DeviceId}");
        Console.WriteLine(new string('-', 45));
        Console.WriteLine($"Device ID:       {device.DeviceId}");
        Console.WriteLine($"Zone ID:         {device.ZoneId}");
        Console.WriteLine($"Device Type:     {device.DeviceType}");
        Console.WriteLine($"Device Name:     {device.DeviceName}");
        Console.WriteLine($"IP Address:      {device.IpAddress ?? "NULL"}");
        Console.WriteLine($"Port:            {FormatInt(device.Port)}");
        Console.WriteLine($"Channel:         {FormatInt(device.Channel)}");
        Console.WriteLine($"Enabled:         {device.Enabled}");
        Console.WriteLine($"Firmware:        {FormatDouble(device.FirmwareVersion)}");
        Console.WriteLine($"Update Mode:     {device.UpdateMode ?? "NULL"}");
        Console.WriteLine($"Poll Interval:   {FormatInt(device.PollIntervalMs)} ms");
        Console.WriteLine($"Last Seen:       {FormatDate(device.LastSeenAt)}");

        List<string> issues = Validate(device, now);

        Console.WriteLine();

        if (issues.Count == 0)
        {
            WriteColoredLine("Validation: No issues detected by these checks.", ConsoleColor.Green);
        }
        else
        {
            WriteColoredLine($"Validation warnings ({issues.Count}):", ConsoleColor.Yellow);

            foreach (string issue in issues)
            {
                Console.WriteLine($"    - {issue}");
            }
        }
    }

    // ---------------------------------------------------------------------
    // Validation: returns a list of problems found (empty list = no problems)
    // ---------------------------------------------------------------------

    static List<string> Validate(DeviceConfig d, DateTime now)
    {
        var issues = new List<string>();

        // Zone, type and name
        if (d.ZoneId <= 0)
            issues.Add("Zone ID must be positive.");

        if (string.IsNullOrWhiteSpace(d.DeviceType))
            issues.Add("Device type is missing.");

        if (string.IsNullOrWhiteSpace(d.DeviceName))
            issues.Add("Device name is missing.");

        // IP address
        if (string.IsNullOrWhiteSpace(d.IpAddress))
        {
            issues.Add("IP address is missing.");
        }
        else if (!IsValidIPv4(d.IpAddress))
        {
            issues.Add("IP address is not a valid IPv4 address (expected e.g. 10.20.1.11).");
        }

        // Port
        if (d.Port is null || d.Port < 1 || d.Port > 65535)
            issues.Add("Port must be between 1 and 65535.");

        // Channel: must exist, not be negative, and not exceed the limit.
        if (d.Channel is null || d.Channel < 0)
        {
            issues.Add("Channel is missing or negative.");
        }
        else
        {
            // Use the limit for this device type if we have one,
            // otherwise fall back to the general limit.
            int maxChannel = DefaultMaxChannel;

            if (MaxChannelByType.TryGetValue(d.DeviceType, out int typeLimit))
            {
                maxChannel = typeLimit;
            }

            if (d.Channel > maxChannel)
            {
                issues.Add($"Channel {d.Channel} is above the maximum ({maxChannel}).");
            }
        }

        // Enabled flag (stored as a number: 0 = off, 1 = on)
        if (d.Enabled != 0 && d.Enabled != 1)
            issues.Add("Enabled must be 0 or 1.");

        // Firmware
        if (d.FirmwareVersion is null || d.FirmwareVersion <= 0)
            issues.Add("Firmware version is missing or not positive.");

        // Update mode
        if (string.IsNullOrWhiteSpace(d.UpdateMode))
        {
            issues.Add("Update mode is missing.");
        }
        else if (!AllowedModes.Contains(d.UpdateMode))
        {
            issues.Add($"Unknown update mode '{d.UpdateMode}' (allowed: POLL, EVENT, MANUAL).");
        }

        // Poll interval
        if (d.PollIntervalMs is null || d.PollIntervalMs <= 0)
            issues.Add("Poll interval must be positive.");

        // Last seen: missing, or too long ago
        if (d.LastSeenAt is null)
        {
            issues.Add("Last-seen timestamp is missing; device status is unknown.");
        }
        else
        {
            TimeSpan age = now - d.LastSeenAt.Value;

            if (age.TotalDays > StaleAfterDays)
            {
                issues.Add(
                    $"Device not seen for {(int)age.TotalDays} days (limit: {StaleAfterDays} days).");
            }
        }

        return issues;
    }

    // Checks for a proper IPv4 address: exactly four numbers from 0 to 255
    // separated by dots, like 10.20.1.11. (The built-in IPAddress.TryParse is
    // too relaxed: it also accepts things like "1" or "10.20".)
    static bool IsValidIPv4(string value)
    {
        string[] parts = value.Trim().Split('.');

        if (parts.Length != 4)
            return false;

        foreach (string part in parts)
        {
            // byte holds 0-255, so TryParse fails for 256, -1, "abc", "", ...
            if (!byte.TryParse(part, NumberStyles.None, CultureInfo.InvariantCulture, out _))
                return false;
        }

        return true;
    }

    // ---------------------------------------------------------------------
    // Small formatting helpers
    // ---------------------------------------------------------------------

    // Shows "NULL" when a number is missing.
    static string FormatInt(int? value) =>
        value.HasValue ? value.Value.ToString(CultureInfo.InvariantCulture) : "NULL";

    // Shows a decimal number with 2 digits, e.g. 1.05.
    static string FormatDouble(double? value) =>
        value.HasValue ? value.Value.ToString("0.00", CultureInfo.InvariantCulture) : "NULL";

    // Shows a date as 2026-08-14 09:12:33.
    static string FormatDate(DateTime? value) =>
        value.HasValue
            ? value.Value.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)
            : "NULL";

    // Cuts long text down to maxLength characters, ending with "...".
    static string Shorten(string text, int maxLength)
    {
        if (text.Length <= maxLength)
            return text;

        return text.Substring(0, maxLength - 3) + "...";
    }

    // ---------------------------------------------------------------------
    // Colored output (skipped automatically if output goes to a file/pipe)
    // ---------------------------------------------------------------------

    static void WriteColored(string text, ConsoleColor color)
    {
        if (Console.IsOutputRedirected)
        {
            Console.Write(text);
            return;
        }

        Console.ForegroundColor = color;
        Console.Write(text);
        Console.ResetColor();
    }

    static void WriteColoredLine(string text, ConsoleColor color)
    {
        WriteColored(text, color);
        Console.WriteLine();
    }
}

// =============================================================================
// DeviceConfig: one row of the dbo.DeviceConfig table.
// A "?" after a type (like int?) means the value can be empty (NULL).
// =============================================================================
class DeviceConfig
{
    public int DeviceId { get; set; }
    public int ZoneId { get; set; }
    public string DeviceType { get; set; } = "";
    public string DeviceName { get; set; } = "";
    public string? IpAddress { get; set; }
    public int? Port { get; set; }
    public int? Channel { get; set; }
    public short Enabled { get; set; }
    public double? FirmwareVersion { get; set; }
    public string? UpdateMode { get; set; }
    public int? PollIntervalMs { get; set; }
    public DateTime? LastSeenAt { get; set; }
}
