using ProgramMover.Models;
using System.Text.Json;
using System.Linq;

namespace ProgramMover.Agents;

/// <summary>
/// Orchestrator Agent - Coordinates all other agents and manages workflow
/// </summary>
public class OrchestratorAgent
{
    private readonly AppConfiguration _config;
    private readonly ScannerAgent _scanner;
    private readonly AnalyzerAgent _analyzer;
    private readonly PlannerAgent _planner;
    private readonly SecurityAgent _security;
    private readonly ExecutorAgent _executor;
    private readonly RollbackAgent _rollback;
    private readonly MonitorAgent _monitor;
    
    private readonly List<ExecutionLog> _logs = new();

    public OrchestratorAgent(AppConfiguration? config = null)
    {
        _config = config ?? new AppConfiguration();
        
        _scanner = new ScannerAgent();
        _analyzer = new AnalyzerAgent();
        _planner = new PlannerAgent(_config.TargetDrive);
        _security = new SecurityAgent();
        _executor = new ExecutorAgent(_config);
        _rollback = new RollbackAgent();
        _monitor = new MonitorAgent(_config.MonitoringHours);
    }

    public List<ExecutionLog> GetAllLogs()
    {
        var allLogs = new List<ExecutionLog>();
        allLogs.AddRange(_logs);
        allLogs.AddRange(_scanner.GetLogs());
        allLogs.AddRange(_analyzer.GetLogs());
        allLogs.AddRange(_planner.GetLogs());
        allLogs.AddRange(_security.GetLogs());
        allLogs.AddRange(_executor.GetLogs());
        allLogs.AddRange(_rollback.GetLogs());
        allLogs.AddRange(_monitor.GetLogs());
        return allLogs.OrderBy(l => l.Timestamp).ToList();
    }

    /// <summary>
    /// Performs initial security checks
    /// </summary>
    public SecurityCheckResult PerformSecurityChecks(List<AppEntry>? apps = null)
    {
        Log(LogLevel.Info, "Orchestrator", "Performing security checks...");

        var checkApps = apps ?? new List<AppEntry>();
        return _security.PerformSecurityChecks(_config.TargetDrive, checkApps, _config.CreateRestorePoint);
    }

    public RecoveryReport DetectAndFixPreviousRuns()
    {
        Log(LogLevel.Info, "Orchestrator", "Scanning for remnants of earlier runs...");

        var report = _rollback.InspectAndRecoverPreviousRuns();

        if (report.RestoredPaths.Any())
            Log(LogLevel.Warning, "Orchestrator", $"Recovered {report.RestoredPaths.Count} installations from previous attempts.");

        if (report.NeedsManualReview.Any())
            Log(LogLevel.Warning, "Orchestrator", $"{report.NeedsManualReview.Count} folders need manual review after prior runs.");

        if (report.Errors.Any())
            Log(LogLevel.Error, "Orchestrator", $"Errors while checking previous runs: {string.Join(", ", report.Errors.Take(3))}");

        return report;
    }

    /// <summary>
    /// Scans the system for installed applications
    /// </summary>
    public List<AppEntry> ScanSystem()
    {
        Log(LogLevel.Info, "Orchestrator", "Starting system scan...");
        
        var apps = _scanner.ScanSystem();
        
        Log(LogLevel.Info, "Orchestrator", $"System scan complete. Found {apps.Count} applications");
        
        return apps;
    }

    /// <summary>
    /// Analyzes scanned applications
    /// </summary>
    public List<AppEntry> AnalyzeApps(List<AppEntry> apps)
    {
        Log(LogLevel.Info, "Orchestrator", $"Analyzing {apps.Count} applications...");
        
        var analyzedApps = _analyzer.AnalyzeApps(apps);
        
        Log(LogLevel.Info, "Orchestrator", "Analysis complete");
        
        return analyzedApps;
    }

    /// <summary>
    /// Creates a migration plan for selected applications
    /// </summary>
    public MigrationPlan CreateMigrationPlan(List<AppEntry> apps, bool isDryRun = false)
    {
        Log(LogLevel.Info, "Orchestrator", $"Creating migration plan for {apps.Count} applications (DryRun: {isDryRun})");
        
        var plan = _planner.CreatePlan(apps, isDryRun);
        
        Log(LogLevel.Info, "Orchestrator", $"Migration plan created with {plan.Steps.Count} steps");
        
        return plan;
    }

    /// <summary>
    /// Executes a migration plan
    /// </summary>
    public async Task<ExecutionResult> ExecuteMigration(MigrationPlan plan, IProgress<ExecutionProgress>? progress = null)
    {
        Log(LogLevel.Info, "Orchestrator", $"Executing migration plan {plan.Id} (DryRun: {plan.IsDryRun})");
        
        ExecutionResult result;
        
        try
        {
            result = await _executor.ExecutePlan(plan, progress);
            
            if (!result.Success.GetValueOrDefault())
            {
                Log(LogLevel.Error, "Orchestrator", $"Execution had failures: {result.Message}");
                
                // Only rollback if it's not a DryRun and we have failed steps for specific apps
                if (!plan.IsDryRun && result.FailedSteps.Any())
                {
                    Log(LogLevel.Warning, "Orchestrator", "Rolling back failed apps...");
                    var failedAppIds = result.FailedSteps.Select(s => s.AppId).Distinct().ToList();
                    var appStepsToRollback = result.SuccessfulSteps.Where(s => failedAppIds.Contains(s.AppId)).ToList();
                    
                    if (appStepsToRollback.Any())
                    {
                        var rollbackResult = await RollbackMigration(plan, appStepsToRollback);
                        
                        if (rollbackResult.Success)
                        {
                            Log(LogLevel.Info, "Orchestrator", "Rollback completed for failed apps");
                        }
                        else
                        {
                            Log(LogLevel.Error, "Orchestrator", "Rollback had issues - manual intervention may be required");
                        }
                    }
                }
            }
            else
            {
                Log(LogLevel.Info, "Orchestrator", $"Execution completed successfully: {result.Message}");
            }
        }
        catch (Exception ex)
        {
            Log(LogLevel.Error, "Orchestrator", $"Critical error during execution: {ex.Message}", exception: ex.ToString());
            
            result = new ExecutionResult
            {
                PlanId = plan.Id,
                Success = false,
                Message = $"Critical error: {ex.Message}",
                StartTime = DateTime.Now,
                EndTime = DateTime.Now
            };
        }
        
        return result;
    }

    /// <summary>
    /// Rolls back a migration
    /// </summary>
    public async Task<RollbackResult> RollbackMigration(MigrationPlan plan, List<MigrationStep> completedSteps)
    {
        Log(LogLevel.Info, "Orchestrator", $"Rolling back migration plan {plan.Id}");
        
        var result = await _rollback.RollbackPlan(plan, completedSteps);
        
        Log(LogLevel.Info, "Orchestrator", $"Rollback complete: {result.Message}");
        
        return result;
    }

    /// <summary>
    /// Monitors migrated applications
    /// </summary>
    public async Task<MonitoringResult> MonitorApps(List<AppEntry> apps)
    {
        Log(LogLevel.Info, "Orchestrator", $"Starting monitoring for {apps.Count} applications");
        
        var result = await _monitor.QuickHealthCheck(apps);
        
        Log(LogLevel.Info, "Orchestrator", $"Monitoring complete. Overall status: {result.OverallStatus}");
        
        return result;
    }

    /// <summary>
    /// Cleans up .old directories after successful migration
    /// </summary>
    public async Task<CleanupResult> CleanupOldDirectories(List<AppEntry> apps, bool confirm = false)
    {
        Log(LogLevel.Info, "Orchestrator", $"Cleaning up .old directories (Confirm: {confirm})");
        
        var result = await _rollback.CleanupOldDirectories(apps, confirm);
        
        Log(LogLevel.Info, "Orchestrator", $"Cleanup complete. Deleted: {result.DeletedDirectories.Count}, Pending: {result.PendingDirectories.Count}");
        
        return result;
    }

    /// <summary>
    /// Saves inventory to JSON file
    /// </summary>
    private void SaveInventory(List<AppEntry> apps, string filename)
    {
        try
        {
            var logDir = GetLogDirectory();
            var filePath = Path.Combine(logDir, filename);
            
            var options = new JsonSerializerOptions { WriteIndented = true };
            var json = JsonSerializer.Serialize(apps, options);
            
            File.WriteAllText(filePath, json);
            
            Log(LogLevel.Info, "Orchestrator", $"Saved inventory to: {filePath}");
        }
        catch (Exception ex)
        {
            Log(LogLevel.Error, "Orchestrator", $"Error saving inventory: {ex.Message}", exception: ex.ToString());
        }
    }

    /// <summary>
    /// Saves migration plan to JSON file
    /// </summary>
    private void SavePlan(MigrationPlan plan, string filename)
    {
        try
        {
            var logDir = GetLogDirectory();
            var filePath = Path.Combine(logDir, filename);
            
            var options = new JsonSerializerOptions { WriteIndented = true };
            var json = JsonSerializer.Serialize(plan, options);
            
            File.WriteAllText(filePath, json);
            
            Log(LogLevel.Info, "Orchestrator", $"Saved plan to: {filePath}");
        }
        catch (Exception ex)
        {
            Log(LogLevel.Error, "Orchestrator", $"Error saving plan: {ex.Message}", exception: ex.ToString());
        }
    }

    /// <summary>
    /// Saves execution report to JSON file
    /// </summary>
    private void SaveExecutionReport(ExecutionResult result, MigrationPlan plan, string filename)
    {
        try
        {
            var logDir = GetLogDirectory();
            var filePath = Path.Combine(logDir, filename);
            
            var report = new
            {
                result.PlanId,
                result.Success,
                result.Message,
                result.StartTime,
                result.EndTime,
                result.Duration,
                Plan = new
                {
                    plan.Id,
                    plan.TargetDrive,
                    AppCount = plan.Apps.Count,
                    StepCount = plan.Steps.Count,
                    plan.IsDryRun
                },
                SuccessfulSteps = result.SuccessfulSteps.Select(s => new
                {
                    s.Order,
                    s.StepType,
                    s.Description,
                    s.Status,
                    s.ExitCode
                }),
                FailedSteps = result.FailedSteps.Select(s => new
                {
                    s.Order,
                    s.StepType,
                    s.Description,
                    s.Status,
                    s.ErrorMessage,
                    s.ExitCode
                }),
                Logs = GetAllLogs()
            };
            
            var options = new JsonSerializerOptions { WriteIndented = true };
            var json = JsonSerializer.Serialize(report, options);
            
            File.WriteAllText(filePath, json);
            
            Log(LogLevel.Info, "Orchestrator", $"Saved execution report to: {filePath}");
        }
        catch (Exception ex)
        {
            Log(LogLevel.Error, "Orchestrator", $"Error saving execution report: {ex.Message}", exception: ex.ToString());
        }
    }

    /// <summary>
    /// Saves logs to JSONL file
    /// </summary>
    public void SaveLogs(string? filename = null)
    {
        try
        {
            var baseLogDir = GetLogDirectory();
            var runFolder = Path.Combine(baseLogDir, $"run_{DateTime.Now:yyyyMMdd_HHmmss}");
            
            if (!Directory.Exists(runFolder))
                Directory.CreateDirectory(runFolder);
            
            var logFile = filename ?? "migration.jsonl";
            var filePath = Path.Combine(runFolder, logFile);
            
            var logs = GetAllLogs();
            
            using var writer = new StreamWriter(filePath);
            foreach (var log in logs)
            {
                var json = JsonSerializer.Serialize(log);
                writer.WriteLine(json);
            }
            
            Log(LogLevel.Info, "Orchestrator", $"Saved {logs.Count} log entries to: {filePath}");
        }
        catch (Exception ex)
        {
            Log(LogLevel.Error, "Orchestrator", $"Error saving logs: {ex.Message}", exception: ex.ToString());
        }
    }

    /// <summary>
    /// Gets or creates the log directory
    /// </summary>
    private string GetLogDirectory()
    {
        var logDir = Directory.Exists(_config.LogDirectory.Substring(0, 2)) 
            ? _config.LogDirectory 
            : _config.LogDirectoryFallback;

        if (!Directory.Exists(logDir))
            Directory.CreateDirectory(logDir);

        return logDir;
    }

    private void Log(LogLevel level, string category, string message, string? exception = null)
    {
        if (!LoggingOptions.EnableDebugLogs && (level == LogLevel.Debug || level == LogLevel.Trace))
            return;

        _logs.Add(new ExecutionLog
        {
            Level = level,
            Category = category,
            Message = message,
            Exception = exception
        });
    }
}
