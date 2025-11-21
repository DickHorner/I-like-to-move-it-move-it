using System.Text.Json.Serialization;

namespace ProgramMover.Models;

/// <summary>
/// Represents a migration plan for selected applications
/// </summary>
public class MigrationPlan
{
    [JsonPropertyName("Id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [JsonPropertyName("CreatedAt")]
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    [JsonPropertyName("TargetDrive")]
    public string TargetDrive { get; set; } = "D:";

    [JsonPropertyName("Apps")]
    public List<AppEntry> Apps { get; set; } = new();

    [JsonPropertyName("Steps")]
    public List<MigrationStep> Steps { get; set; } = new();

    [JsonPropertyName("EstimatedDurationMinutes")]
    public int EstimatedDurationMinutes { get; set; }

    [JsonPropertyName("TotalSizeBytes")]
    public long TotalSizeBytes { get; set; }

    [JsonPropertyName("IsDryRun")]
    public bool IsDryRun { get; set; }
}

/// <summary>
/// Represents a single step in the migration plan
/// </summary>
public class MigrationStep
{
    [JsonPropertyName("Id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [JsonPropertyName("AppId")]
    public string AppId { get; set; } = string.Empty;

    [JsonPropertyName("AppName")]
    public string AppName { get; set; } = string.Empty;

    [JsonPropertyName("StepType")]
    public StepType StepType { get; set; }

    [JsonPropertyName("Description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("SourcePath")]
    public string? SourcePath { get; set; }

    [JsonPropertyName("TargetPath")]
    public string? TargetPath { get; set; }

    [JsonPropertyName("ServiceName")]
    public string? ServiceName { get; set; }

    [JsonPropertyName("TimeoutSeconds")]
    public int TimeoutSeconds { get; set; } = 300;

    [JsonPropertyName("Order")]
    public int Order { get; set; }

    [JsonPropertyName("CanRollback")]
    public bool CanRollback { get; set; } = true;

    [JsonPropertyName("Status")]
    public StepStatus Status { get; set; } = StepStatus.Pending;

    [JsonPropertyName("ExecutedAt")]
    public DateTime? ExecutedAt { get; set; }

    [JsonPropertyName("ErrorMessage")]
    public string? ErrorMessage { get; set; }

    [JsonPropertyName("ExitCode")]
    public int? ExitCode { get; set; }
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum StepType
{
    PreCheck,
    StopService,
    BackupRegistry,
    RobocopyFiles,
    VerifyFiles,
    RenameSource,
    CreateJunction,
    StartService,
    VerifyService,
    SmokeTest,
    Cleanup
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum StepStatus
{
    Pending,
    Running,
    Completed,
    Failed,
    Skipped,
    RolledBack
}
