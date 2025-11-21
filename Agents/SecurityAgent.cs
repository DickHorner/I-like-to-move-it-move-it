using ProgramMover.Models;
using System.Diagnostics;
using System.Security.Principal;

namespace ProgramMover.Agents;

/// <summary>
/// Security Agent - Handles security checks and permissions
/// </summary>
public class SecurityAgent
{
    private readonly List<ExecutionLog> _logs = new();

    public List<ExecutionLog> GetLogs() => _logs;

    /// <summary>
    /// Checks if the application is running with administrator privileges
    /// </summary>
    public bool IsAdministrator()
    {
        try
        {
            using var identity = WindowsIdentity.GetCurrent();
            var principal = new WindowsPrincipal(identity);
            var isAdmin = principal.IsInRole(WindowsBuiltInRole.Administrator);
            
            Log(isAdmin ? LogLevel.Info : LogLevel.Error, 
                "Security", 
                $"Administrator check: {isAdmin}");
            
            return isAdmin;
        }
        catch (Exception ex)
        {
            Log(LogLevel.Error, "Security", $"Error checking admin rights: {ex.Message}", exception: ex.ToString());
            return false;
        }
    }

    /// <summary>
    /// Validates the target drive is suitable for migration
    /// </summary>
    public (bool IsValid, string Message) ValidateTargetDrive(string targetDrive)
    {
        Log(LogLevel.Info, "Security", $"Validating target drive: {targetDrive}");

        try
        {
            // Check if drive exists
            var driveInfo = new DriveInfo(targetDrive);
            
            if (!driveInfo.IsReady)
            {
                return (false, $"Drive {targetDrive} is not ready or does not exist");
            }

            // Check if it's NTFS
            if (!driveInfo.DriveFormat.Equals("NTFS", StringComparison.OrdinalIgnoreCase))
            {
                Log(LogLevel.Warning, "Security", $"Drive {targetDrive} is {driveInfo.DriveFormat}, not NTFS");
                return (false, $"Drive {targetDrive} must be NTFS formatted (current: {driveInfo.DriveFormat})");
            }

            // Check available space
            var availableGB = driveInfo.AvailableFreeSpace / (1024.0 * 1024.0 * 1024.0);
            Log(LogLevel.Info, "Security", $"Drive {targetDrive} has {availableGB:F2} GB available");

            if (availableGB < 10)
            {
                return (false, $"Drive {targetDrive} has insufficient space: {availableGB:F2} GB available (minimum 10 GB required)");
            }

            // Check write permissions
            var testPath = Path.Combine(targetDrive + "\\", "mover_test_" + Guid.NewGuid().ToString() + ".tmp");
            try
            {
                File.WriteAllText(testPath, "test");
                File.Delete(testPath);
                Log(LogLevel.Info, "Security", $"Write test on {targetDrive} successful");
            }
            catch (Exception ex)
            {
                Log(LogLevel.Error, "Security", $"Write test failed on {targetDrive}: {ex.Message}");
                return (false, $"No write permission on drive {targetDrive}");
            }

            return (true, $"Drive {targetDrive} is valid ({availableGB:F2} GB available, NTFS)");
        }
        catch (Exception ex)
        {
            Log(LogLevel.Error, "Security", $"Error validating drive {targetDrive}: {ex.Message}", exception: ex.ToString());
            return (false, $"Error accessing drive {targetDrive}: {ex.Message}");
        }
    }

    /// <summary>
    /// Creates a system restore point (if possible)
    /// </summary>
    public (bool Success, string Message) CreateRestorePoint(string description = "ProgramMover - Before Migration")
    {
        Log(LogLevel.Info, "Security", "Attempting to create system restore point...");

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-Command \"Checkpoint-Computer -Description '{description}' -RestorePointType 'MODIFY_SETTINGS'\"",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using var process = Process.Start(psi);
            if (process == null)
            {
                Log(LogLevel.Warning, "Security", "Failed to start PowerShell for restore point");
                return (false, "Failed to create restore point");
            }

            var output = process.StandardOutput.ReadToEnd();
            var error = process.StandardError.ReadToEnd();
            process.WaitForExit();

            if (process.ExitCode == 0)
            {
                Log(LogLevel.Info, "Security", "System restore point created successfully");
                return (true, "System restore point created");
            }
            else
            {
                Log(LogLevel.Warning, "Security", $"Restore point creation failed: {error}");
                return (false, $"Restore point creation failed (not critical): {error}");
            }
        }
        catch (Exception ex)
        {
            Log(LogLevel.Warning, "Security", $"Error creating restore point: {ex.Message}");
            return (false, $"Could not create restore point: {ex.Message}");
        }
    }

    /// <summary>
    /// Checks for problematic software (Anti-Cheat, EDR, etc.)
    /// </summary>
    public (bool HasProblematic, List<string> Problems) CheckForProblematicSoftware(List<AppEntry> apps)
    {
        Log(LogLevel.Info, "Security", "Checking for problematic software...");

        var problems = new List<string>();
        var problematicKeywords = new Dictionary<string, string>
        {
            { "antivirus", "Antivirus software" },
            { "anti-virus", "Antivirus software" },
            { "anticheat", "Anti-cheat software" },
            { "anti-cheat", "Anti-cheat software" },
            { "edr", "Endpoint Detection and Response" },
            { "endpoint", "Endpoint security" },
            { "battleye", "BattlEye Anti-Cheat" },
            { "easyanticheat", "Easy Anti-Cheat" },
            { "vanguard", "Riot Vanguard" },
            { "punkbuster", "PunkBuster" },
            { "crowdstrike", "CrowdStrike EDR" },
            { "carbon black", "Carbon Black EDR" },
            { "sentinel", "SentinelOne EDR" },
            { "defender", "Windows Defender" }
        };

        foreach (var app in apps)
        {
            foreach (var (keyword, description) in problematicKeywords)
            {
                if (app.DisplayName.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
                    (app.Publisher?.Contains(keyword, StringComparison.OrdinalIgnoreCase) ?? false))
                {
                    var problem = $"{app.DisplayName} ({description})";
                    problems.Add(problem);
                    Log(LogLevel.Warning, "Security", $"Problematic software detected: {problem}");
                }
            }
        }

        return (problems.Any(), problems);
    }

    /// <summary>
    /// Checks for running processes that might interfere
    /// </summary>
    public (bool HasConflicts, List<string> Conflicts) CheckForRunningConflicts(List<AppEntry> apps)
    {
        Log(LogLevel.Info, "Security", "Checking for running processes...");

        var conflicts = new List<string>();

        try
        {
            var runningProcesses = Process.GetProcesses();

            foreach (var app in apps)
            {
                if (string.IsNullOrEmpty(app.InstallLocation)) continue;

                // Check if any process is running from the install location
                foreach (var process in runningProcesses)
                {
                    try
                    {
                        var processPath = process.MainModule?.FileName;
                        if (string.IsNullOrEmpty(processPath)) continue;

                        if (processPath.StartsWith(app.InstallLocation, StringComparison.OrdinalIgnoreCase))
                        {
                            var conflict = $"{app.DisplayName}: Process '{process.ProcessName}' is running";
                            conflicts.Add(conflict);
                            Log(LogLevel.Warning, "Security", conflict);
                        }
                    }
                    catch
                    {
                        // Access denied or process exited
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Log(LogLevel.Error, "Security", $"Error checking processes: {ex.Message}", exception: ex.ToString());
        }

        return (conflicts.Any(), conflicts);
    }

    /// <summary>
    /// Performs all pre-flight security checks
    /// </summary>
    public SecurityCheckResult PerformSecurityChecks(string targetDrive, List<AppEntry> apps, bool createRestorePoint = true)
    {
        Log(LogLevel.Info, "Security", "Starting comprehensive security checks...");

        var result = new SecurityCheckResult();

        // Check 1: Admin rights
        result.IsAdministrator = IsAdministrator();
        if (!result.IsAdministrator)
        {
            result.Errors.Add("Administrator privileges required. Please run as Administrator.");
            return result;
        }

        // Check 2: Target drive
        var (driveValid, driveMessage) = ValidateTargetDrive(targetDrive);
        result.TargetDriveValid = driveValid;
        if (!driveValid)
        {
            result.Errors.Add(driveMessage);
        }
        else
        {
            result.Messages.Add(driveMessage);
        }

        // Check 3: Restore point
        if (createRestorePoint)
        {
            var (restoreSuccess, restoreMessage) = CreateRestorePoint();
            result.RestorePointCreated = restoreSuccess;
            if (!restoreSuccess)
            {
                result.Warnings.Add(restoreMessage);
            }
            else
            {
                result.Messages.Add(restoreMessage);
            }
        }

        // Check 4: Problematic software
        var (hasProblematic, problems) = CheckForProblematicSoftware(apps);
        if (hasProblematic)
        {
            result.Warnings.AddRange(problems.Select(p => $"Warning: {p} detected - proceed with caution"));
        }

        // Check 5: Running conflicts
        var (hasConflicts, conflicts) = CheckForRunningConflicts(apps);
        if (hasConflicts)
        {
            result.Warnings.AddRange(conflicts.Select(c => $"Active process: {c} - should be closed before migration"));
        }

        result.IsValid = result.IsAdministrator && result.TargetDriveValid && !result.Errors.Any();

        Log(LogLevel.Info, "Security", $"Security checks complete. Valid: {result.IsValid}, Warnings: {result.Warnings.Count}, Errors: {result.Errors.Count}");

        return result;
    }

    private void Log(LogLevel level, string category, string message, string? exception = null)
    {
        _logs.Add(new ExecutionLog
        {
            Level = level,
            Category = category,
            Message = message,
            Exception = exception
        });
    }
}

/// <summary>
/// Result of security checks
/// </summary>
public class SecurityCheckResult
{
    public bool IsValid { get; set; }
    public bool IsAdministrator { get; set; }
    public bool TargetDriveValid { get; set; }
    public bool RestorePointCreated { get; set; }
    public List<string> Messages { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
    public List<string> Errors { get; set; } = new();
}
