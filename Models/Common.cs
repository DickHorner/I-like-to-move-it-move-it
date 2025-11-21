using System.Text.Json.Serialization;

namespace ProgramMover.Models;

/// <summary>
/// Represents a log entry for execution tracking
/// </summary>
public class ExecutionLog
{
    [JsonPropertyName("Timestamp")]
    public DateTime Timestamp { get; set; } = DateTime.Now;

    [JsonPropertyName("Level")]
    public LogLevel Level { get; set; }

    [JsonPropertyName("Category")]
    public string Category { get; set; } = string.Empty;

    [JsonPropertyName("Message")]
    public string Message { get; set; } = string.Empty;

    [JsonPropertyName("AppId")]
    public string? AppId { get; set; }

    [JsonPropertyName("StepId")]
    public string? StepId { get; set; }

    [JsonPropertyName("ExitCode")]
    public int? ExitCode { get; set; }

    [JsonPropertyName("Exception")]
    public string? Exception { get; set; }

    [JsonPropertyName("Data")]
    public Dictionary<string, object>? Data { get; set; }

    public override string ToString()
    {
        return $"[{Timestamp:yyyy-MM-dd HH:mm:ss.fff}] [{Level}] [{Category}] {Message}";
    }
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum LogLevel
{
    Trace,
    Debug,
    Info,
    Warning,
    Error,
    Critical
}

/// <summary>
/// Configuration for the application
/// </summary>
public class AppConfiguration
{
    [JsonPropertyName("TargetDrive")]
    public string TargetDrive { get; set; } = "D:";

    [JsonPropertyName("LogDirectory")]
    public string LogDirectory { get; set; } = @"D:\mover\logs";

    [JsonPropertyName("LogDirectoryFallback")]
    public string LogDirectoryFallback { get; set; } = @"C:\mover\logs";

    [JsonPropertyName("MonitoringHours")]
    public int MonitoringHours { get; set; } = 72;

    [JsonPropertyName("CreateRestorePoint")]
    public bool CreateRestorePoint { get; set; } = true;

    [JsonPropertyName("RobocopyRetries")]
    public int RobocopyRetries { get; set; } = 3;

    [JsonPropertyName("RobocopyWaitSeconds")]
    public int RobocopyWaitSeconds { get; set; } = 5;

    [JsonPropertyName("BackupConfirmed")]
    public bool BackupConfirmed { get; set; }
}

/// <summary>
/// Exit codes for the application
/// </summary>
public static class ExitCodes
{
    public const int Success = 0;
    public const int UserError = 1;
    public const int NotMoveable = 2;
    public const int CopyError = 3;
    public const int VerificationError = 4;
    public const int RollbackSuccess = 5;
    public const int RollbackFailed = 6;
}
