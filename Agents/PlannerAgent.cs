using ProgramMover.Models;

namespace ProgramMover.Agents;

/// <summary>
/// Planner Agent - Creates detailed migration plans
/// </summary>
public class PlannerAgent
{
    private readonly List<ExecutionLog> _logs = new();
    private readonly string _targetDrive;

    public PlannerAgent(string targetDrive = "D:")
    {
        _targetDrive = targetDrive;
    }

    public List<ExecutionLog> GetLogs() => _logs;

    /// <summary>
    /// Creates a migration plan for selected applications
    /// </summary>
    public MigrationPlan CreatePlan(List<AppEntry> apps, bool isDryRun = false)
    {
        Log(LogLevel.Info, "Planner", $"Creating migration plan for {apps.Count} applications...");

        var plan = new MigrationPlan
        {
            TargetDrive = _targetDrive,
            Apps = apps,
            IsDryRun = isDryRun,
            TotalSizeBytes = apps.Sum(a => a.TotalSizeBytes)
        };

        int stepOrder = 0;

        foreach (var app in apps)
        {
            try
            {
                if (string.IsNullOrEmpty(app.InstallLocation))
                {
                    Log(LogLevel.Warning, "Planner", $"Skipping {app.DisplayName} - no install location", app.Id);
                    continue;
                }

                var appSteps = CreateStepsForApp(app, ref stepOrder);
                plan.Steps.AddRange(appSteps);
            }
            catch (Exception ex)
            {
                Log(LogLevel.Error, "Planner", $"Error planning {app.DisplayName}: {ex.Message}", app.Id);
            }
        }

        // Estimate duration (rough: 1 minute per GB + overhead)
        plan.EstimatedDurationMinutes = (int)(plan.TotalSizeBytes / (1024.0 * 1024.0 * 1024.0)) + (apps.Count * 2);

        Log(LogLevel.Info, "Planner", $"Plan created with {plan.Steps.Count} steps, estimated duration: {plan.EstimatedDurationMinutes} minutes");

        return plan;
    }

    /// <summary>
    /// Creates migration steps for a single application
    /// </summary>
    private List<MigrationStep> CreateStepsForApp(AppEntry app, ref int stepOrder)
    {
        var steps = new List<MigrationStep>();
        var targetPath = CalculateTargetPath(app);

        // Step 1: Pre-check
        steps.Add(new MigrationStep
        {
            AppId = app.Id,
            AppName = app.DisplayName,
            StepType = StepType.PreCheck,
            Description = $"Pre-flight checks for {app.DisplayName}",
            SourcePath = app.InstallLocation,
            TargetPath = targetPath,
            Order = stepOrder++,
            TimeoutSeconds = 30
        });

        // Step 2: Stop Services (if any)
        foreach (var service in app.Services)
        {
            steps.Add(new MigrationStep
            {
                AppId = app.Id,
                AppName = app.DisplayName,
                StepType = StepType.StopService,
                Description = $"Stop service: {service}",
                ServiceName = service,
                Order = stepOrder++,
                TimeoutSeconds = 60
            });
        }

        // Step 3: Backup Registry (for MSI)
        if (app.InstallType == InstallType.MSI && !string.IsNullOrEmpty(app.RegistryPath))
        {
            steps.Add(new MigrationStep
            {
                AppId = app.Id,
                AppName = app.DisplayName,
                StepType = StepType.BackupRegistry,
                Description = $"Backup registry keys",
                SourcePath = app.RegistryPath,
                Order = stepOrder++,
                TimeoutSeconds = 30
            });
        }

        // Step 4: Robocopy Files
        steps.Add(new MigrationStep
        {
            AppId = app.Id,
            AppName = app.DisplayName,
            StepType = StepType.RobocopyFiles,
            Description = $"Copy files from {app.InstallLocation} to {targetPath}",
            SourcePath = app.InstallLocation,
            TargetPath = targetPath,
            Order = stepOrder++,
            TimeoutSeconds = Math.Max(300, (int)(app.TotalSizeBytes / (1024 * 1024 * 10))) // 10 MB/s estimate
        });

        // Step 5: Verify Files
        steps.Add(new MigrationStep
        {
            AppId = app.Id,
            AppName = app.DisplayName,
            StepType = StepType.VerifyFiles,
            Description = $"Verify file counts and sizes",
            SourcePath = app.InstallLocation,
            TargetPath = targetPath,
            Order = stepOrder++,
            TimeoutSeconds = 120
        });

        // Step 6: Rename Source to .old
        steps.Add(new MigrationStep
        {
            AppId = app.Id,
            AppName = app.DisplayName,
            StepType = StepType.RenameSource,
            Description = $"Rename source directory to .old",
            SourcePath = app.InstallLocation,
            Order = stepOrder++,
            TimeoutSeconds = 30
        });

        // Step 7: Create Junction
        steps.Add(new MigrationStep
        {
            AppId = app.Id,
            AppName = app.DisplayName,
            StepType = StepType.CreateJunction,
            Description = $"Create junction from {app.InstallLocation} to {targetPath}",
            SourcePath = app.InstallLocation,
            TargetPath = targetPath,
            Order = stepOrder++,
            TimeoutSeconds = 30
        });

        // Step 8: Start Services (if any)
        foreach (var service in app.Services)
        {
            steps.Add(new MigrationStep
            {
                AppId = app.Id,
                AppName = app.DisplayName,
                StepType = StepType.StartService,
                Description = $"Start service: {service}",
                ServiceName = service,
                Order = stepOrder++,
                TimeoutSeconds = 60
            });
        }

        // Step 9: Verify Service (if any)
        if (app.Services.Any())
        {
            steps.Add(new MigrationStep
            {
                AppId = app.Id,
                AppName = app.DisplayName,
                StepType = StepType.VerifyService,
                Description = $"Verify services are running",
                Order = stepOrder++,
                TimeoutSeconds = 30
            });
        }

        // Step 10: Smoke Test
        steps.Add(new MigrationStep
        {
            AppId = app.Id,
            AppName = app.DisplayName,
            StepType = StepType.SmokeTest,
            Description = $"Smoke test - verify basic functionality",
            SourcePath = app.InstallLocation,
            Order = stepOrder++,
            TimeoutSeconds = 60
        });

        return steps;
    }

    /// <summary>
    /// Calculates the target path on the destination drive
    /// </summary>
    private string CalculateTargetPath(AppEntry app)
    {
        if (string.IsNullOrEmpty(app.InstallLocation))
            throw new ArgumentException("App has no install location");

        // Extract the path after C:\
        var path = app.InstallLocation;
        
        // Remove drive letter (C:)
        if (path.Length > 2 && path[1] == ':')
            path = path.Substring(2);

        // Trim leading slashes
        path = path.TrimStart('\\', '/');

        // Construct target path
        return Path.Combine(_targetDrive + "\\", path);
    }

    private void Log(LogLevel level, string category, string message, string? appId = null)
    {
        _logs.Add(new ExecutionLog
        {
            Level = level,
            Category = category,
            Message = message,
            AppId = appId
        });
    }
}
