/* ============================================================================
   C# & SQL Server exercise
   Setup script: creates a database, one table, and sample data.

   Run this once in SQL Server Management Studio (or Azure Data Studio, or
   sqlcmd) before you start. It is safe to re-run — it drops and recreates.

   If you do not have SQL Server, any of these are free and fine:
     - SQL Server 2022 Express          (Windows)
     - SQL Server LocalDB               (ships with Visual Studio)
     - mcr.microsoft.com/mssql/server   (Docker, works on Mac/Linux)
   ============================================================================ */

IF DB_ID('DeviceConfigDB') IS NULL
    CREATE DATABASE DeviceConfigDB;
GO

USE DeviceConfigDB;
GO

IF OBJECT_ID('dbo.DeviceConfig', 'U') IS NOT NULL
    DROP TABLE dbo.DeviceConfig;
GO

CREATE TABLE dbo.DeviceConfig
(
    DeviceId        INT           NOT NULL PRIMARY KEY,
    ZoneId          INT           NOT NULL,
    DeviceType      VARCHAR(10)   NOT NULL,
    DeviceName      VARCHAR(50)   NOT NULL,
    IpAddress       VARCHAR(20)       NULL,
    Port            INT               NULL,
    Channel         INT               NULL,
    Enabled         SMALLINT      NOT NULL,
    FirmwareVersion FLOAT             NULL,
    UpdateMode      VARCHAR(10)       NULL,
    PollIntervalMs  INT               NULL,
    LastSeenAt      DATETIME          NULL
);
GO

INSERT INTO dbo.DeviceConfig
    (DeviceId, ZoneId, DeviceType, DeviceName, IpAddress, Port, Channel,
     Enabled, FirmwareVersion, UpdateMode, PollIntervalMs, LastSeenAt)
VALUES
    ( 1,  1, 'RTR',  'Core Router',           '10.20.1.11', 8080,  0, 1, 1.05, 'POLL',   200, '2026-08-14 09:12:33.000'),
    ( 2,  2, 'RTR',  'Backup Router',         '10.20.1.12', 8080,  1, 1, 1.05, 'POLL',   200, '2026-08-14 09:12:33.000'),
    ( 3,  3, 'CAM',  'Lobby Camera 1',        '10.20.2.31', 8554,  2, 1, 1.02, 'EVENT',  100, '2026-08-20 17:45:02.000'),
    ( 4,  4, 'CAM',  'Lobby Camera 2',        NULL,         8554,  3, 1, 1.02, 'EVENT',  100, '2026-08-20 17:45:02.000'),
    ( 5,  5, 'DISP', 'Reception Display',     '10.20.3.51', 9000,  4, 0, 1.00, 'MANUAL', 500, '2026-07-02 11:03:17.000'),
    ( 6,  6, 'SENS', 'Server Room Sensor',    '10.20.4.61', 9100, -1, 1, 1.01, 'POLL',   250, '2026-09-01 08:00:00.000'),
    ( 7,  7, 'READ', 'Main Entrance Reader',  '10.20.5.71', 9200,  6, 1, 1.03, 'POLL',     0, '2026-09-03 14:22:41.000'),
    ( 8,  8, 'RTR',  'O''Connor Wing Router', '10.20.1.13', 8080,  7, 1, 1.05, 'POLL',   200, NULL),
    ( 9,  8, 'CAM',  'Rear Exit Camera',      '10.20.2.32', 8554,  8, 1, 0.99, NULL,     100, '2026-06-11 10:15:00.000'),
    (10, 10, 'DISP', 'Lobby Signage Board',   '10.20.3.52', 9000, 99, 1, 1.00, 'MANUAL', 500, '2026-09-10 16:38:55.000');
GO

SELECT COUNT(*) AS RowsLoaded FROM dbo.DeviceConfig;
GO
