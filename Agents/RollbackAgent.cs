using ProgramMover.Models;
using System.ServiceProcess;

namespace ProgramMover.Agents;

/// <summary>
/// Rollback Agent - Reverses migration steps
/// </summary>
public class RollbackAgent
{
    private readonly List<ExecutionLog> _logs = new();

    public List<ExecutionLog> GetLogs() => _logs;

    /// <summary>
    /// Rolls back a migration plan
    /// </summary>
    public async Task<RollbackResult> RollbackPlan(MigrationPlan plan, List<MigrationStep> completedSteps)
    {
        Log(LogLevel.Info, "Rollback", $"Starting rollback of plan {plan.Id}");

        var result = new RollbackResult
        {
            PlanId = plan.Id,
            StartTime = DateTime.Now
        };

        // Reverse the order of steps
        var reversedSteps = completedSteps
            .Where(s => s.Status == StepStatus.Completed || s.Status == StepStatus.Failed)
            .Where(s => s.CanRollback)
            .OrderByDescending(s => s.Order)
            .ToList();

        Log(LogLevel.Info, "Rollback", $"Rolling back {reversedSteps.Count} steps");

        foreach (var step in reversedSteps)
        {
            try
            {
                Log(LogLevel.Info, "Rollback", $"Rolling back step: {step.Description}", step.AppId, step.Id);

                await RollbackStep(step);
                
                step.Status = StepStatus.RolledBack;
                result.SuccessfulRollbacks.Add(step);
            }
            catch (Exception ex)
            {
                step.Status = StepStatus.Failed;
                result.FailedRollbacks.Add(step);
                
                Log(LogLevel.Error, "Rollback", $"Rollback failed for step: {step.Description} - {ex.Message}", step.AppId, step.Id, exception: ex.ToString());
            }
        }

        result.EndTime = DateTime.Now;
        result.Duration = result.EndTime - result.StartTime;
        result.Success = !result.FailedRollbacks.Any();
        result.Message = result.Success 
            ? $"Rollback successful - {result.SuccessfulRollbacks.Count} steps reversed"
            : $"Rollback completed with {result.FailedRollbacks.Count} failures";

        Log(LogLevel.Info, "Rollback", $"Rollback complete. Success: {result.Success}");

        return result;
    }

    /// <summary>
    /// Rolls back a single migration step
    /// </summary>
    private async Task RollbackStep(MigrationStep step)
    {
        switch (step.StepType)
        {
            case StepType.CreateJunction:
                await RollbackJunction(step);
                break;

            case StepType.RenameSource:
                await RollbackRename(step);
                break;

            case StepType.StopService:
                await RollbackStopService(step);
                break;

            case StepType.StartService:
                await RollbackStartService(step);
                break;

            case StepType.RobocopyFiles:
                Log(LogLevel.Info, "Rollback", $"Robocopy rollback: .old directory preserved for manual cleanup", step.AppId, step.Id);
                await Task.CompletedTask;
                break;

            default:
                Log(LogLevel.Debug, "Rollback", $"No rollback action needed for {step.StepType}", step.AppId, step.Id);
                await Task.CompletedTask;
                break;
        }
    }

    private async Task RollbackJunction(MigrationStep step)
    {
        if (string.IsNullOrEmpty(step.SourcePath))
            throw new ArgumentException("Source path is required");

        if (Directory.Exists(step.SourcePath))
        {
            // Check if it's a junction/symbolic link
            var attributes = File.GetAttributes(step.SourcePath);
            if ((attributes & FileAttributes.ReparsePoint) == FileAttributes.ReparsePoint)
            {
                // It's a junction - delete it
                Directory.Delete(step.SourcePath, false);
                Log(LogLevel.Info, "Rollback", $"Deleted junction: {step.SourcePath}", step.AppId, step.Id);
            }
            else
            {
                Log(LogLevel.Warning, "Rollback", $"Path exists but is not a junction: {step.SourcePath}", step.AppId, step.Id);
            }
        }

        await Task.CompletedTask;
    }

    private async Task RollbackRename(MigrationStep step)
    {
        if (string.IsNullOrEmpty(step.SourcePath))
            throw new ArgumentException("Source path is required");

        var oldPath = step.SourcePath + ".old";

        if (!Directory.Exists(oldPath))
        {
            Log(LogLevel.Warning, "Rollback", $"Old directory not found: {oldPath}", step.AppId, step.Id);
            return;
        }

        // If the source path exists (as junction), remove it first
        if (Directory.Exists(step.SourcePath))
        {
            var attributes = File.GetAttributes(step.SourcePath);
            if ((attributes & FileAttributes.ReparsePoint) == FileAttributes.ReparsePoint)
            {
                Directory.Delete(step.SourcePath, false);
            }
        }

        // Rename .old back to original
        await Task.Run(() => Directory.Move(oldPath, step.SourcePath));
        
        Log(LogLevel.Info, "Rollback", $"Restored {oldPath} to {step.SourcePath}", step.AppId, step.Id);
    }

    private async Task RollbackStopService(MigrationStep step)
    {
        // If we stopped a service, start it back
        if (string.IsNullOrEmpty(step.ServiceName))
            return;

        try
        {
            using var sc = new ServiceController(step.ServiceName);
            if (sc.Status != ServiceControllerStatus.Running)
            {
                sc.Start();
                await Task.Run(() => sc.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(30)));
                Log(LogLevel.Info, "Rollback", $"Restarted service: {step.ServiceName}", step.AppId, step.Id);
            }
        }
        catch (Exception ex)
        {
            Log(LogLevel.Error, "Rollback", $"Failed to restart service {step.ServiceName}: {ex.Message}", step.AppId, step.Id);
        }
    }

    private async Task RollbackStartService(MigrationStep step)
    {
        // If we started a service, stop it back
        if (string.IsNullOrEmpty(step.ServiceName))
            return;

        try
        {
            using var sc = new ServiceController(step.ServiceName);
            if (sc.Status != ServiceControllerStatus.Stopped)
            {
                sc.Stop();
                await Task.Run(() => sc.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(30)));
                Log(LogLevel.Info, "Rollback", $"Stopped service: {step.ServiceName}", step.AppId, step.Id);
            }
        }
        catch (Exception ex)
        {
            Log(LogLevel.Error, "Rollback", $"Failed to stop service {step.ServiceName}: {ex.Message}", step.AppId, step.Id);
        }
    }

    /// <summary>
    /// Cleans up .old directories after successful monitoring period
    /// </summary>
    public async Task<CleanupResult> CleanupOldDirectories(List<AppEntry> apps, bool confirmDelete = false)
    {
        Log(LogLevel.Info, "Rollback", $"Cleaning up .old directories for {apps.Count} apps (Confirm: {confirmDelete})");

        var result = new CleanupResult();

        foreach (var app in apps)
        {
            if (string.IsNullOrEmpty(app.InstallLocation))
                continue;

            var oldPath = app.InstallLocation + ".old";

            if (!Directory.Exists(oldPath))
                continue;

            try
            {
                if (confirmDelete)
                {
                    await Task.Run(() => Directory.Delete(oldPath, true));
                    result.DeletedDirectories.Add(oldPath);
                    Log(LogLevel.Info, "Rollback", $"Deleted .old directory: {oldPath}", app.Id);
                }
                else
                {
                    result.PendingDirectories.Add(oldPath);
                    Log(LogLevel.Info, "Rollback", $"Found .old directory (not deleted): {oldPath}", app.Id);
                }
            }
            catch (Exception ex)
            {
                result.FailedDirectories.Add(oldPath);
                Log(LogLevel.Error, "Rollback", $"Failed to delete {oldPath}: {ex.Message}", app.Id, exception: ex.ToString());
            }
        }

        result.Success = !result.FailedDirectories.Any();
        
        return result;
    }

    private void Log(LogLevel level, string category, string message, string? appId = null, string? stepId = null, string? exception = null)
    {
        _logs.Add(new ExecutionLog
        {
            Level = level,
            Category = category,
            Message = message,
            AppId = appId,
            StepId = stepId,
            Exception = exception
        });
    }
}

public class RollbackResult
{
    public string PlanId { get; set; } = string.Empty;
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public TimeSpan Duration { get; set; }
    public List<MigrationStep> SuccessfulRollbacks { get; set; } = new();
    public List<MigrationStep> FailedRollbacks { get; set; } = new();
}

public class CleanupResult
{
    public bool Success { get; set; }
    public List<string> DeletedDirectories { get; set; } = new();
    public List<string> PendingDirectories { get; set; } = new();
    public List<string> FailedDirectories { get; set; } = new();
}
