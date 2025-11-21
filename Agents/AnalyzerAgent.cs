using ProgramMover.Models;

namespace ProgramMover.Agents;

/// <summary>
/// Analyzer Agent - Classifies applications based on moveability
/// </summary>
public class AnalyzerAgent
{
    private readonly List<ExecutionLog> _logs = new();

    public List<ExecutionLog> GetLogs() => _logs;

    /// <summary>
    /// Analyzes and scores applications for moveability
    /// </summary>
    public List<AppEntry> AnalyzeApps(List<AppEntry> apps)
    {
        Log(LogLevel.Info, "Analyzer", $"Analyzing {apps.Count} applications...");

        foreach (var app in apps)
        {
            try
            {
                AnalyzeApp(app);
            }
            catch (Exception ex)
            {
                Log(LogLevel.Error, "Analyzer", $"Error analyzing {app.DisplayName}: {ex.Message}", app.Id);
                app.Category = MoveCategory.NotMoveable;
                app.Score = 0;
            }
        }

        Log(LogLevel.Info, "Analyzer", $"Analysis complete. MoveableAuto: {apps.Count(a => a.Category == MoveCategory.MoveableAuto)}, " +
                                       $"MoveableCaution: {apps.Count(a => a.Category == MoveCategory.MoveableCaution)}, " +
                                       $"NotMoveable: {apps.Count(a => a.Category == MoveCategory.NotMoveable)}");

        return apps;
    }

    /// <summary>
    /// Analyzes a single application and assigns score and category
    /// </summary>
    private void AnalyzeApp(AppEntry app)
    {
        int score = 50; // Base score

        // Check InstallLocation
        if (!string.IsNullOrEmpty(app.InstallLocation))
        {
            score += 15;

            // Bonus for standard locations
            if (app.InstallLocation.StartsWith(@"C:\Program Files", StringComparison.OrdinalIgnoreCase))
                score += 10;
        }
        else
        {
            score -= 20;
            app.Category = MoveCategory.NotMoveable;
            app.SuggestedAction = "No install location found";
            return;
        }

        // Check for Windows directory - HARD BLOCK
        if (app.InstallLocation.StartsWith(@"C:\Windows", StringComparison.OrdinalIgnoreCase))
        {
            score = 0;
            app.Category = MoveCategory.NotMoveable;
            app.SuggestedAction = "System component - NEVER move";
            Log(LogLevel.Warning, "Analyzer", $"{app.DisplayName} is in Windows directory - blocked", app.Id);
            return;
        }

        // Check for System32 files - HARD BLOCK
        if (app.HasSysFiles)
        {
            score -= 30;
        }

        // Check Services
        if (app.Services.Any())
        {
            score -= 10; // Services need careful handling
            
            // Check for critical services
            var criticalServiceNames = new[] { "wuauserv", "bits", "eventlog", "dns", "dhcp", "w32time" };
            if (app.Services.Any(s => criticalServiceNames.Contains(s.ToLowerInvariant())))
            {
                score = 0;
                app.Category = MoveCategory.NotMoveable;
                app.SuggestedAction = "Critical system service";
                return;
            }
        }

        // Check Install Type
        switch (app.InstallType)
        {
            case InstallType.Portable:
                score += 20;
                break;
            case InstallType.EXE:
                score += 10;
                break;
            case InstallType.MSI:
                score -= 15; // MSI installations track paths
                break;
            case InstallType.Store:
                score = 0;
                app.Category = MoveCategory.NotMoveable;
                app.SuggestedAction = "Store app - use Windows Settings to move";
                return;
            case InstallType.Service:
                score -= 5;
                break;
        }

        // Check Publisher - Known good publishers
        if (!string.IsNullOrEmpty(app.Publisher))
        {
            var goodPublishers = new[] { "7-Zip", "Notepad++", "VideoLAN", "Mozilla", "Google" };
            if (goodPublishers.Any(p => app.Publisher.Contains(p, StringComparison.OrdinalIgnoreCase)))
                score += 10;

            // Microsoft products need caution
            if (app.Publisher.Contains("Microsoft", StringComparison.OrdinalIgnoreCase))
                score -= 15;
        }

        // Check Size
        if (app.TotalSizeBytes > 0)
        {
            var sizeGB = app.TotalSizeBytes / (1024.0 * 1024.0 * 1024.0);
            
            if (sizeGB < 1)
                score += 5; // Small apps are easier to move
            else if (sizeGB > 50)
                score -= 5; // Large apps take longer

            // Very large apps need caution
            if (sizeGB > 100)
                score -= 10;
        }

        // Check for known problematic software
        var problematicKeywords = new[] 
        { 
            "antivirus", "anti-virus", "anticheat", "anti-cheat", 
            "driver", "edr", "endpoint", "security", "firewall",
            "virtualbox", "vmware", "hyper-v"
        };

        if (problematicKeywords.Any(k => app.DisplayName.Contains(k, StringComparison.OrdinalIgnoreCase)))
        {
            score -= 25;
            Log(LogLevel.Warning, "Analyzer", $"{app.DisplayName} contains problematic keywords", app.Id);
        }

        // Assign final score
        app.Score = Math.Max(0, Math.Min(100, score));

        // Determine category based on score
        if (app.Score >= 75)
        {
            app.Category = MoveCategory.MoveableAuto;
            app.SuggestedAction = "Robocopy+Junction";
        }
        else if (app.Score >= 40)
        {
            app.Category = MoveCategory.MoveableCaution;
            app.SuggestedAction = "Manual verification recommended";
        }
        else
        {
            app.Category = MoveCategory.NotMoveable;
            app.SuggestedAction = "High risk - not recommended";
        }

        // Override for specific conditions
        if (app.InstallType == InstallType.MSI && app.Score < 60)
        {
            app.Category = MoveCategory.MoveableCaution;
            app.SuggestedAction = "MSI installation - consider reinstall instead";
        }

        if (app.Services.Any() && app.Category == MoveCategory.MoveableAuto)
        {
            app.Category = MoveCategory.MoveableCaution;
            app.SuggestedAction = "Has services - verify after move";
        }

        Log(LogLevel.Debug, "Analyzer", $"{app.DisplayName}: Score={app.Score}, Category={app.Category}", app.Id);
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
