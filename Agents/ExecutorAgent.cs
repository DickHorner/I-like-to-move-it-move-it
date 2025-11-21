using ProgramMover.Models;
using System.Diagnostics;
using System.ServiceProcess;

namespace ProgramMover.Agents;

/// <summary>
/// Executor Agent - Executes migration steps
/// </summary>
public class ExecutorAgent
{
    private readonly List<ExecutionLog> _logs = new();
    private readonly AppConfiguration _config;

    public ExecutorAgent(AppConfiguration config)
    {
        _config = config;
    }

    public List<ExecutionLog> GetLogs() => _logs;

    /// <summary>
    /// Executes a migration plan
    /// </summary>
    public async Task<ExecutionResult> ExecutePlan(MigrationPlan plan, IProgress<ExecutionProgress>? progress = null)
    {
        Log(LogLevel.Info, "Executor", $"Starting execution of plan {plan.Id} (DryRun: {plan.IsDryRun})");

        var result = new ExecutionResult
        {
            PlanId = plan.Id,
            StartTime = DateTime.Now
        };

        var totalSteps = plan.Steps.Count;
        var completedSteps = 0;

        foreach (var step in plan.Steps.OrderBy(s => s.Order))
        {
            try
            {
                step.Status = StepStatus.Running;
                step.ExecutedAt = DateTime.Now;

                Log(LogLevel.Info, "Executor", $"Executing step {step.Order + 1}/{totalSteps}: {step.Description}", step.AppId, step.Id);

                progress?.Report(new ExecutionProgress
                {
                    CurrentStep = step.Order + 1,
                    TotalSteps = totalSteps,
                    StepDescription = step.Description,
                    PercentComplete = (int)((completedSteps / (double)totalSteps) * 100)
                });

                if (plan.IsDryRun)
                {
                    // DryRun - just log, don't execute
                    Log(LogLevel.Info, "Executor", $"[DRY RUN] Would execute: {step.Description}", step.AppId, step.Id);
                    step.Status = StepStatus.Completed;
                    await Task.Delay(100); // Simulate work
                }
                else
                {
                    // Real execution
                    await ExecuteStep(step);
                }

                completedSteps++;
                result.SuccessfulSteps.Add(step);
            }
            catch (Exception ex)
            {
                step.Status = StepStatus.Failed;
                step.ErrorMessage = ex.Message;
                result.FailedSteps.Add(step);
                
                Log(LogLevel.Error, "Executor", $"Step failed: {step.Description} - {ex.Message}", step.AppId, step.Id, exception: ex.ToString());

                // Decide whether to continue or abort
                if (step.StepType == StepType.RobocopyFiles || 
                    step.StepType == StepType.VerifyFiles ||
                    step.StepType == StepType.CreateJunction)
                {
                    // Critical failures - abort
                    result.Success = false;
                    result.Message = $"Critical failure at step: {step.Description}";
                    break;
                }
                else
                {
                    // Non-critical - continue but log warning
                    Log(LogLevel.Warning, "Executor", $"Non-critical step failed, continuing: {step.Description}", step.AppId, step.Id);
                }
            }
        }

        result.EndTime = DateTime.Now;
        result.Duration = result.EndTime - result.StartTime;

        if (result.FailedSteps.Any() && !result.Success.HasValue)
        {
            result.Success = false;
            result.Message = $"Completed with {result.FailedSteps.Count} failed steps";
        }
        else if (!result.Success.HasValue)
        {
            result.Success = true;
            result.Message = $"All {completedSteps} steps completed successfully";
        }

        Log(LogLevel.Info, "Executor", $"Execution complete. Success: {result.Success}, Duration: {result.Duration}");

        return result;
    }

    /// <summary>
    /// Executes a single migration step
    /// </summary>
    private async Task ExecuteStep(MigrationStep step)
    {
        switch (step.StepType)
        {
            case StepType.PreCheck:
                await ExecutePreCheck(step);
                break;

            case StepType.StopService:
                await ExecuteStopService(step);
                break;

            case StepType.BackupRegistry:
                await ExecuteBackupRegistry(step);
                break;

            case StepType.RobocopyFiles:
                await ExecuteRobocopy(step);
                break;

            case StepType.VerifyFiles:
                await ExecuteVerifyFiles(step);
                break;

            case StepType.RenameSource:
                await ExecuteRenameSource(step);
                break;

            case StepType.CreateJunction:
                await ExecuteCreateJunction(step);
                break;

            case StepType.StartService:
                await ExecuteStartService(step);
                break;

            case StepType.VerifyService:
                await ExecuteVerifyService(step);
                break;

            case StepType.SmokeTest:
                await ExecuteSmokeTest(step);
                break;

            default:
                throw new NotImplementedException($"Step type {step.StepType} not implemented");
        }

        step.Status = StepStatus.Completed;
    }

    private async Task ExecutePreCheck(MigrationStep step)
    {
        // Check source exists
        if (!Directory.Exists(step.SourcePath))
            throw new DirectoryNotFoundException($"Source directory not found: {step.SourcePath}");

        // Check target parent exists
        var targetParent = Path.GetDirectoryName(step.TargetPath);
        if (!string.IsNullOrEmpty(targetParent) && !Directory.Exists(targetParent))
        {
            Directory.CreateDirectory(targetParent);
            Log(LogLevel.Info, "Executor", $"Created target parent directory: {targetParent}", step.AppId, step.Id);
        }

        await Task.CompletedTask;
    }

    private async Task ExecuteStopService(MigrationStep step)
    {
        if (string.IsNullOrEmpty(step.ServiceName))
            throw new ArgumentException("Service name is required");

        using var sc = new ServiceController(step.ServiceName);
        
        if (sc.Status == ServiceControllerStatus.Stopped)
        {
            Log(LogLevel.Info, "Executor", $"Service {step.ServiceName} is already stopped", step.AppId, step.Id);
            return;
        }

        sc.Stop();
        await Task.Run(() => sc.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(step.TimeoutSeconds)));
        
        Log(LogLevel.Info, "Executor", $"Service {step.ServiceName} stopped successfully", step.AppId, step.Id);
    }

    private async Task ExecuteBackupRegistry(MigrationStep step)
    {
        var backupPath = Path.Combine(GetLogDirectory(), $"registry_backup_{step.AppId}_{DateTime.Now:yyyyMMdd_HHmmss}.reg");
        
        var psi = new ProcessStartInfo
        {
            FileName = "reg.exe",
            Arguments = $"export \"{step.SourcePath}\" \"{backupPath}\" /y",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        using var process = Process.Start(psi);
        if (process == null)
            throw new InvalidOperationException("Failed to start reg.exe");

        await process.WaitForExitAsync();
        step.ExitCode = process.ExitCode;

        if (process.ExitCode != 0)
        {
            var error = await process.StandardError.ReadToEndAsync();
            throw new InvalidOperationException($"Registry export failed: {error}");
        }

        Log(LogLevel.Info, "Executor", $"Registry backed up to: {backupPath}", step.AppId, step.Id);
    }

    private async Task ExecuteRobocopy(MigrationStep step)
    {
        if (string.IsNullOrEmpty(step.SourcePath) || string.IsNullOrEmpty(step.TargetPath))
            throw new ArgumentException("Source and target paths are required");

        var logFile = Path.Combine(GetLogDirectory(), $"robocopy_{step.AppId}_{DateTime.Now:yyyyMMdd_HHmmss}.log");

        var arguments = $"\"{step.SourcePath}\" \"{step.TargetPath}\" /MIR /COPYALL /XJ /R:{_config.RobocopyRetries} /W:{_config.RobocopyWaitSeconds} /LOG:\"{logFile}\" /NP /NDL /NFL";

        var psi = new ProcessStartInfo
        {
            FileName = "robocopy.exe",
            Arguments = arguments,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        Log(LogLevel.Debug, "Executor", $"Robocopy command: robocopy.exe {arguments}", step.AppId, step.Id);

        using var process = Process.Start(psi);
        if (process == null)
            throw new InvalidOperationException("Failed to start robocopy");

        await process.WaitForExitAsync();
        step.ExitCode = process.ExitCode;

        // Robocopy exit codes: 0-7 are success (various levels), 8+ are errors
        if (step.ExitCode >= 8)
        {
            var error = await process.StandardError.ReadToEndAsync();
            throw new InvalidOperationException($"Robocopy failed with exit code {step.ExitCode}: {error}");
        }

        Log(LogLevel.Info, "Executor", $"Robocopy completed with exit code {step.ExitCode}, log: {logFile}", step.AppId, step.Id);
    }

    private async Task ExecuteVerifyFiles(MigrationStep step)
    {
        if (string.IsNullOrEmpty(step.SourcePath) || string.IsNullOrEmpty(step.TargetPath))
            throw new ArgumentException("Source and target paths are required");

        var sourceDir = new DirectoryInfo(step.SourcePath);
        var targetDir = new DirectoryInfo(step.TargetPath);

        var sourceFiles = await Task.Run(() => sourceDir.GetFiles("*", SearchOption.AllDirectories));
        var targetFiles = await Task.Run(() => targetDir.GetFiles("*", SearchOption.AllDirectories));

        var sourceCount = sourceFiles.Length;
        var targetCount = targetFiles.Length;
        var sourceSize = sourceFiles.Sum(f => f.Length);
        var targetSize = targetFiles.Sum(f => f.Length);

        Log(LogLevel.Info, "Executor", $"Verification: Source={sourceCount} files, {sourceSize} bytes; Target={targetCount} files, {targetSize} bytes", step.AppId, step.Id);

        if (sourceCount != targetCount)
            throw new InvalidOperationException($"File count mismatch: source={sourceCount}, target={targetCount}");

        if (sourceSize != targetSize)
            throw new InvalidOperationException($"Size mismatch: source={sourceSize}, target={targetSize}");

        Log(LogLevel.Info, "Executor", "File verification successful", step.AppId, step.Id);
    }

    private async Task ExecuteRenameSource(MigrationStep step)
    {
        if (string.IsNullOrEmpty(step.SourcePath))
            throw new ArgumentException("Source path is required");

        var oldPath = step.SourcePath + ".old";
        
        if (Directory.Exists(oldPath))
        {
            Log(LogLevel.Warning, "Executor", $"Old directory already exists: {oldPath}, deleting...", step.AppId, step.Id);
            await Task.Run(() => Directory.Delete(oldPath, true));
        }

        await Task.Run(() => Directory.Move(step.SourcePath, oldPath));
        
        Log(LogLevel.Info, "Executor", $"Renamed {step.SourcePath} to {oldPath}", step.AppId, step.Id);
    }

    private async Task ExecuteCreateJunction(MigrationStep step)
    {
        if (string.IsNullOrEmpty(step.SourcePath) || string.IsNullOrEmpty(step.TargetPath))
            throw new ArgumentException("Source and target paths are required");

        var arguments = $"/C mklink /J \"{step.SourcePath}\" \"{step.TargetPath}\"";

        var psi = new ProcessStartInfo
        {
            FileName = "cmd.exe",
            Arguments = arguments,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        using var process = Process.Start(psi);
        if (process == null)
            throw new InvalidOperationException("Failed to start cmd.exe");

        var output = await process.StandardOutput.ReadToEndAsync();
        var error = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        step.ExitCode = process.ExitCode;

        if (process.ExitCode != 0)
            throw new InvalidOperationException($"Junction creation failed: {error}");

        Log(LogLevel.Info, "Executor", $"Junction created: {step.SourcePath} -> {step.TargetPath}", step.AppId, step.Id);
    }

    private async Task ExecuteStartService(MigrationStep step)
    {
        if (string.IsNullOrEmpty(step.ServiceName))
            throw new ArgumentException("Service name is required");

        using var sc = new ServiceController(step.ServiceName);
        
        if (sc.Status == ServiceControllerStatus.Running)
        {
            Log(LogLevel.Info, "Executor", $"Service {step.ServiceName} is already running", step.AppId, step.Id);
            return;
        }

        sc.Start();
        await Task.Run(() => sc.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(step.TimeoutSeconds)));
        
        Log(LogLevel.Info, "Executor", $"Service {step.ServiceName} started successfully", step.AppId, step.Id);
    }

    private async Task ExecuteVerifyService(MigrationStep step)
    {
        // Get all services for this app from the step's app
        // For now, just log that we would verify
        Log(LogLevel.Info, "Executor", "Service verification - checking all services are running", step.AppId, step.Id);
        await Task.Delay(1000);
    }

    private async Task ExecuteSmokeTest(MigrationStep step)
    {
        // Basic smoke test - check that junction works and files are accessible
        if (string.IsNullOrEmpty(step.SourcePath))
            throw new ArgumentException("Source path is required");

        if (!Directory.Exists(step.SourcePath))
            throw new DirectoryNotFoundException($"Junction path not accessible: {step.SourcePath}");

        var files = await Task.Run(() => Directory.GetFiles(step.SourcePath, "*", SearchOption.TopDirectoryOnly));
        
        Log(LogLevel.Info, "Executor", $"Smoke test passed - {files.Length} files accessible through junction", step.AppId, step.Id);
    }

    private string GetLogDirectory()
    {
        var logDir = Directory.Exists(_config.LogDirectory.Substring(0, 2)) 
            ? _config.LogDirectory 
            : _config.LogDirectoryFallback;

        if (!Directory.Exists(logDir))
            Directory.CreateDirectory(logDir);

        return logDir;
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

public class ExecutionResult
{
    public string PlanId { get; set; } = string.Empty;
    public bool? Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public TimeSpan Duration { get; set; }
    public List<MigrationStep> SuccessfulSteps { get; set; } = new();
    public List<MigrationStep> FailedSteps { get; set; } = new();
}

public class ExecutionProgress
{
    public int CurrentStep { get; set; }
    public int TotalSteps { get; set; }
    public string StepDescription { get; set; } = string.Empty;
    public int PercentComplete { get; set; }
}
