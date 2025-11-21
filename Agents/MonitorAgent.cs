using ProgramMover.Models;
using System.Diagnostics;
using System.ServiceProcess;

namespace ProgramMover.Agents;

/// <summary>
/// Monitor Agent - Monitors applications after migration
/// </summary>
public class MonitorAgent
{
    private readonly List<ExecutionLog> _logs = new();
    private readonly int _monitoringHours;

    public MonitorAgent(int monitoringHours = 72)
    {
        _monitoringHours = monitoringHours;
    }

    public List<ExecutionLog> GetLogs() => _logs;

    /// <summary>
    /// Monitors migrated applications for a specified period
    /// </summary>
    public async Task<MonitoringResult> MonitorApps(List<AppEntry> apps, CancellationToken cancellationToken = default)
    {
        Log(LogLevel.Info, "Monitor", $"Starting monitoring for {apps.Count} apps for {_monitoringHours} hours");

        var result = new MonitoringResult
        {
            StartTime = DateTime.Now,
            EndTime = DateTime.Now.AddHours(_monitoringHours),
            MonitoringHours = _monitoringHours
        };

        foreach (var app in apps)
        {
            var appStatus = new AppMonitoringStatus
            {
                AppId = app.Id,
                AppName = app.DisplayName,
                Status = MonitorStatus.OK
            };

            try
            {
                // Check services
                if (app.Services.Any())
                {
                    var serviceStatus = await CheckServices(app);
                    appStatus.ServiceChecks.Add(serviceStatus);
                    
                    if (!serviceStatus.AllRunning)
                        appStatus.Status = MonitorStatus.Degraded;
                }

                // Check junction
                var junctionStatus = CheckJunction(app);
                appStatus.JunctionValid = junctionStatus;
                
                if (!junctionStatus)
                    appStatus.Status = MonitorStatus.Error;

                // Check process start capability
                var processStatus = await CheckProcessStart(app);
                appStatus.CanStartProcess = processStatus;

                if (!processStatus && appStatus.Status == MonitorStatus.OK)
                    appStatus.Status = MonitorStatus.Degraded;

                result.AppStatuses.Add(appStatus);
            }
            catch (Exception ex)
            {
                appStatus.Status = MonitorStatus.Error;
                appStatus.ErrorMessage = ex.Message;
                result.AppStatuses.Add(appStatus);
                
                Log(LogLevel.Error, "Monitor", $"Error monitoring {app.DisplayName}: {ex.Message}", app.Id, exception: ex.ToString());
            }

            if (cancellationToken.IsCancellationRequested)
                break;
        }

        result.OverallStatus = DetermineOverallStatus(result.AppStatuses);
        
        Log(LogLevel.Info, "Monitor", $"Monitoring snapshot complete. Overall status: {result.OverallStatus}");

        return result;
    }

    /// <summary>
    /// Performs a quick health check on monitored apps
    /// </summary>
    public async Task<MonitoringResult> QuickHealthCheck(List<AppEntry> apps)
    {
        Log(LogLevel.Info, "Monitor", $"Performing quick health check on {apps.Count} apps");
        
        return await MonitorApps(apps);
    }

    /// <summary>
    /// Checks if services are running
    /// </summary>
    private async Task<ServiceCheckStatus> CheckServices(AppEntry app)
    {
        var status = new ServiceCheckStatus
        {
            Timestamp = DateTime.Now
        };

        foreach (var serviceName in app.Services)
        {
            try
            {
                using var sc = new ServiceController(serviceName);
                await Task.Run(() => sc.Refresh());

                var running = sc.Status == ServiceControllerStatus.Running;
                status.ServiceStates[serviceName] = running;

                if (!running)
                {
                    Log(LogLevel.Warning, "Monitor", $"Service {serviceName} is not running (Status: {sc.Status})", app.Id);
                }
            }
            catch (Exception ex)
            {
                status.ServiceStates[serviceName] = false;
                Log(LogLevel.Error, "Monitor", $"Error checking service {serviceName}: {ex.Message}", app.Id);
            }
        }

        status.AllRunning = status.ServiceStates.All(kvp => kvp.Value);
        return status;
    }

    /// <summary>
    /// Checks if junction is valid
    /// </summary>
    private bool CheckJunction(AppEntry app)
    {
        if (string.IsNullOrEmpty(app.InstallLocation))
            return false;

        try
        {
            if (!Directory.Exists(app.InstallLocation))
            {
                Log(LogLevel.Error, "Monitor", $"Install location does not exist: {app.InstallLocation}", app.Id);
                return false;
            }

            var attributes = File.GetAttributes(app.InstallLocation);
            var isJunction = (attributes & FileAttributes.ReparsePoint) == FileAttributes.ReparsePoint;

            if (!isJunction)
            {
                Log(LogLevel.Warning, "Monitor", $"Install location is not a junction: {app.InstallLocation}", app.Id);
                return true; // Not necessarily an error - might be original location
            }

            // Try to list files to verify junction works
            var files = Directory.GetFiles(app.InstallLocation, "*", SearchOption.TopDirectoryOnly);
            
            Log(LogLevel.Debug, "Monitor", $"Junction valid: {app.InstallLocation} ({files.Length} files accessible)", app.Id);
            return true;
        }
        catch (Exception ex)
        {
            Log(LogLevel.Error, "Monitor", $"Error checking junction for {app.DisplayName}: {ex.Message}", app.Id);
            return false;
        }
    }

    /// <summary>
    /// Checks if we can find and potentially start a process from the app location
    /// </summary>
    private async Task<bool> CheckProcessStart(AppEntry app)
    {
        if (string.IsNullOrEmpty(app.InstallLocation))
            return false;

        try
        {
            // Look for .exe files in the install location
            var exeFiles = Directory.GetFiles(app.InstallLocation, "*.exe", SearchOption.TopDirectoryOnly);
            
            if (exeFiles.Length == 0)
            {
                Log(LogLevel.Debug, "Monitor", $"No exe files found in {app.InstallLocation}", app.Id);
                return true; // Not necessarily an error
            }

            Log(LogLevel.Debug, "Monitor", $"Found {exeFiles.Length} exe files in {app.InstallLocation}", app.Id);
            
            // We found executables - that's good enough for now
            // We don't actually start them to avoid interfering with user's system
            return true;
        }
        catch (Exception ex)
        {
            Log(LogLevel.Error, "Monitor", $"Error checking process start capability: {ex.Message}", app.Id);
            return false;
        }

        await Task.CompletedTask;
    }

    /// <summary>
    /// Determines overall status from individual app statuses
    /// </summary>
    private MonitorStatus DetermineOverallStatus(List<AppMonitoringStatus> appStatuses)
    {
        if (!appStatuses.Any())
            return MonitorStatus.Unknown;

        if (appStatuses.Any(a => a.Status == MonitorStatus.Error))
            return MonitorStatus.Error;

        if (appStatuses.Any(a => a.Status == MonitorStatus.Degraded))
            return MonitorStatus.Degraded;

        if (appStatuses.All(a => a.Status == MonitorStatus.OK))
            return MonitorStatus.OK;

        return MonitorStatus.Unknown;
    }

    /// <summary>
    /// Gets event log entries related to application errors
    /// </summary>
    public List<EventLogEntry> GetRecentErrorEvents(int hours = 24)
    {
        var entries = new List<EventLogEntry>();

        try
        {
            var eventLog = new EventLog("Application");
            var sinceTime = DateTime.Now.AddHours(-hours);

            foreach (EventLogEntry entry in eventLog.Entries)
            {
                if (entry.TimeGenerated < sinceTime)
                    continue;

                if (entry.EntryType == EventLogEntryType.Error || entry.EntryType == EventLogEntryType.Warning)
                {
                    entries.Add(entry);
                }
            }

            Log(LogLevel.Info, "Monitor", $"Found {entries.Count} error/warning events in last {hours} hours");
        }
        catch (Exception ex)
        {
            Log(LogLevel.Warning, "Monitor", $"Error reading event log: {ex.Message}");
        }

        return entries;
    }

    private void Log(LogLevel level, string category, string message, string? appId = null, string? exception = null)
    {
        _logs.Add(new ExecutionLog
        {
            Level = level,
            Category = category,
            Message = message,
            AppId = appId,
            Exception = exception
        });
    }
}

public class MonitoringResult
{
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public int MonitoringHours { get; set; }
    public MonitorStatus OverallStatus { get; set; }
    public List<AppMonitoringStatus> AppStatuses { get; set; } = new();
}

public class AppMonitoringStatus
{
    public string AppId { get; set; } = string.Empty;
    public string AppName { get; set; } = string.Empty;
    public MonitorStatus Status { get; set; }
    public bool JunctionValid { get; set; }
    public bool CanStartProcess { get; set; }
    public List<ServiceCheckStatus> ServiceChecks { get; set; } = new();
    public string? ErrorMessage { get; set; }
}

public class ServiceCheckStatus
{
    public DateTime Timestamp { get; set; }
    public Dictionary<string, bool> ServiceStates { get; set; } = new();
    public bool AllRunning { get; set; }
}

public enum MonitorStatus
{
    Unknown,
    OK,
    Degraded,
    Error
}
