using System.Text.Json.Serialization;

namespace ProgramMover.Models;

/// <summary>
/// Represents an installed application discovered during scanning
/// </summary>
public class AppEntry
{
    [JsonPropertyName("Id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [JsonPropertyName("DisplayName")]
    public string DisplayName { get; set; } = string.Empty;

    [JsonPropertyName("Publisher")]
    public string? Publisher { get; set; }

    [JsonPropertyName("InstallLocation")]
    public string? InstallLocation { get; set; }

    [JsonPropertyName("UninstallString")]
    public string? UninstallString { get; set; }

    [JsonPropertyName("InstallType")]
    public InstallType InstallType { get; set; } = InstallType.Unknown;

    [JsonPropertyName("Services")]
    public List<string> Services { get; set; } = new();

    [JsonPropertyName("HasSysFiles")]
    public bool HasSysFiles { get; set; }

    [JsonPropertyName("FilesCount")]
    public int FilesCount { get; set; }

    [JsonPropertyName("TotalSizeBytes")]
    public long TotalSizeBytes { get; set; }

    [JsonPropertyName("Score")]
    public int Score { get; set; }

    [JsonPropertyName("Category")]
    public MoveCategory Category { get; set; } = MoveCategory.Unknown;

    [JsonPropertyName("SuggestedAction")]
    public string SuggestedAction { get; set; } = "Unknown";

    [JsonPropertyName("LastScan")]
    public DateTime LastScan { get; set; } = DateTime.Now;

    [JsonPropertyName("EstimatedSize")]
    public string? EstimatedSize { get; set; }

    [JsonPropertyName("Version")]
    public string? Version { get; set; }

    [JsonPropertyName("RegistryPath")]
    public string? RegistryPath { get; set; }
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum InstallType
{
    Unknown,
    MSI,
    EXE,
    Portable,
    Store,
    Service
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum MoveCategory
{
    Unknown,
    MoveableAuto,
    MoveableCaution,
    NotMoveable
}
