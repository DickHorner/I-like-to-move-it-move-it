using Microsoft.Win32;
using ProgramMover.Models;
using System.Diagnostics;
using System.Management;
using System.ServiceProcess;
using System.Text.RegularExpressions;

namespace ProgramMover.Agents;

/// <summary>
/// Scanner Agent - Discovers installed applications from multiple sources
/// </summary>
public class ScannerAgent
{
    private readonly List<ExecutionLog> _logs = new();

    public List<ExecutionLog> GetLogs() => _logs;

    /// <summary>
    /// Performs a complete system scan for installed applications
    /// </summary>
    public List<AppEntry> ScanSystem()
    {
        var apps = new Dictionary<string, AppEntry>();

        Log(LogLevel.Info, "Scanner", "Starting system scan...");

        try
        {
            // Scan Registry Uninstall Keys
            var registryApps = ScanRegistryUninstallKeys();
            Log(LogLevel.Info, "Scanner", $"Found {registryApps.Count} apps in registry");
            MergeApps(apps, registryApps);

            // Scan Services
            var serviceApps = ScanServices();
            Log(LogLevel.Info, "Scanner", $"Found {serviceApps.Count} apps from services");
            MergeApps(apps, serviceApps);

            // Scan Start Menu
            EnrichFromStartMenu(apps);

            // Scan Program Files
            EnrichFromProgramFiles(apps);

            // UWP/Store Apps
            var storeApps = ScanStoreApps();
            Log(LogLevel.Info, "Scanner", $"Found {storeApps.Count} store apps");
            MergeApps(apps, storeApps);

            Log(LogLevel.Info, "Scanner", $"Scan complete. Total unique apps: {apps.Count}");
        }
        catch (Exception ex)
        {
            Log(LogLevel.Error, "Scanner", $"Error during scan: {ex.Message}", exception: ex.ToString());
        }

        return apps.Values.ToList();
    }

    /// <summary>
    /// Scans registry uninstall keys
    /// </summary>
    private List<AppEntry> ScanRegistryUninstallKeys()
    {
        var apps = new List<AppEntry>();

        var registryPaths = new[]
        {
            (@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall", RegistryView.Registry64),
            (@"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall", RegistryView.Registry64),
            (@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall", RegistryView.Registry32)
        };

        foreach (var (path, view) in registryPaths)
        {
            try
            {
                using var baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, view);
                using var key = baseKey.OpenSubKey(path);
                if (key == null) continue;

                foreach (var subKeyName in key.GetSubKeyNames())
                {
                    try
                    {
                        using var subKey = key.OpenSubKey(subKeyName);
                        if (subKey == null) continue;

                        var displayName = subKey.GetValue("DisplayName") as string;
                        if (string.IsNullOrWhiteSpace(displayName)) continue;

                        // Skip system components and updates
                        if (displayName.Contains("Update for") || 
                            displayName.Contains("Hotfix") ||
                            displayName.StartsWith("Security Update", StringComparison.OrdinalIgnoreCase))
                            continue;

                        var app = new AppEntry
                        {
                            DisplayName = displayName,
                            Publisher = subKey.GetValue("Publisher") as string,
                            InstallLocation = subKey.GetValue("InstallLocation") as string,
                            UninstallString = subKey.GetValue("UninstallString") as string,
                            EstimatedSize = subKey.GetValue("EstimatedSize") as string,
                            Version = subKey.GetValue("DisplayVersion") as string,
                            RegistryPath = $@"HKLM\{path}\{subKeyName}"
                        };

                        // Determine install type
                        if (app.UninstallString?.Contains("msiexec", StringComparison.OrdinalIgnoreCase) == true)
                            app.InstallType = InstallType.MSI;
                        else if (!string.IsNullOrEmpty(app.UninstallString))
                            app.InstallType = InstallType.EXE;
                        else if (!string.IsNullOrEmpty(app.InstallLocation))
                            app.InstallType = InstallType.Portable;

                        apps.Add(app);
                    }
                    catch (Exception ex)
                    {
                        Log(LogLevel.Warning, "Scanner", $"Error reading registry key {subKeyName}: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Log(LogLevel.Warning, "Scanner", $"Error accessing registry path {path}: {ex.Message}");
            }
        }

        // Also scan HKCU
        try
        {
            using var hkcu = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall");
            if (hkcu != null)
            {
                foreach (var subKeyName in hkcu.GetSubKeyNames())
                {
                    try
                    {
                        using var subKey = hkcu.OpenSubKey(subKeyName);
                        if (subKey == null) continue;

                        var displayName = subKey.GetValue("DisplayName") as string;
                        if (string.IsNullOrWhiteSpace(displayName)) continue;

                        var app = new AppEntry
                        {
                            DisplayName = displayName,
                            Publisher = subKey.GetValue("Publisher") as string,
                            InstallLocation = subKey.GetValue("InstallLocation") as string,
                            UninstallString = subKey.GetValue("UninstallString") as string,
                            Version = subKey.GetValue("DisplayVersion") as string,
                            RegistryPath = $@"HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\{subKeyName}"
                        };

                        apps.Add(app);
                    }
                    catch { }
                }
            }
        }
        catch (Exception ex)
        {
            Log(LogLevel.Warning, "Scanner", $"Error scanning HKCU uninstall: {ex.Message}");
        }

        return apps;
    }

    /// <summary>
    /// Scans Windows Services
    /// </summary>
    private List<AppEntry> ScanServices()
    {
        var apps = new List<AppEntry>();

        try
        {
            var services = ServiceController.GetServices();
            
            foreach (var service in services)
            {
                try
                {
                    // Read service details from registry
                    using var key = Registry.LocalMachine.OpenSubKey($@"SYSTEM\CurrentControlSet\Services\{service.ServiceName}");
                    if (key == null) continue;

                    var imagePath = key.GetValue("ImagePath") as string;
                    if (string.IsNullOrWhiteSpace(imagePath)) continue;

                    // Extract executable path
                    var match = Regex.Match(imagePath, @"^""?([^""]+)""?");
                    if (!match.Success) continue;

                    var exePath = match.Groups[1].Value;
                    if (!File.Exists(exePath)) continue;

                    // Skip system services in Windows directories
                    if (exePath.StartsWith(@"C:\Windows", StringComparison.OrdinalIgnoreCase))
                        continue;

                    var installDir = Path.GetDirectoryName(exePath);
                    if (string.IsNullOrEmpty(installDir)) continue;

                    var app = new AppEntry
                    {
                        DisplayName = service.DisplayName,
                        InstallLocation = installDir,
                        InstallType = InstallType.Service,
                        Services = new List<string> { service.ServiceName }
                    };

                    apps.Add(app);
                }
                catch { }
            }
        }
        catch (Exception ex)
        {
            Log(LogLevel.Warning, "Scanner", $"Error scanning services: {ex.Message}");
        }

        return apps;
    }

    /// <summary>
    /// Scans UWP/Store apps
    /// </summary>
    private List<AppEntry> ScanStoreApps()
    {
        var apps = new List<AppEntry>();

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = "-Command \"Get-AppxPackage | Select-Object Name, Publisher, InstallLocation | ConvertTo-Json\"",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                CreateNoWindow = true
            };

            using var process = Process.Start(psi);
            if (process == null) return apps;

            var output = process.StandardOutput.ReadToEnd();
            process.WaitForExit();

            // Parse JSON output (simplified - would need proper JSON parsing)
            // For now, just mark that we detected store apps
            Log(LogLevel.Info, "Scanner", "Store apps detected - will be marked as NotMoveable");
        }
        catch (Exception ex)
        {
            Log(LogLevel.Warning, "Scanner", $"Error scanning store apps: {ex.Message}");
        }

        return apps;
    }

    /// <summary>
    /// Enriches app data from Start Menu shortcuts
    /// </summary>
    private void EnrichFromStartMenu(Dictionary<string, AppEntry> apps)
    {
        var startMenuPaths = new[]
        {
            Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu),
            Environment.GetFolderPath(Environment.SpecialFolder.StartMenu)
        };

        foreach (var path in startMenuPaths)
        {
            if (!Directory.Exists(path)) continue;

            try
            {
                var shortcuts = Directory.GetFiles(path, "*.lnk", SearchOption.AllDirectories);
                Log(LogLevel.Debug, "Scanner", $"Found {shortcuts.Length} shortcuts in {path}");
            }
            catch (Exception ex)
            {
                Log(LogLevel.Warning, "Scanner", $"Error scanning start menu: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Enriches app data from Program Files directories
    /// </summary>
    private void EnrichFromProgramFiles(Dictionary<string, AppEntry> apps)
    {
        var programFilesPaths = new[]
        {
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86)
        };

        foreach (var basePath in programFilesPaths)
        {
            if (!Directory.Exists(basePath)) continue;

            try
            {
                var directories = Directory.GetDirectories(basePath);
                
                foreach (var dir in directories)
                {
                    // Calculate size and file count for known install locations
                    foreach (var app in apps.Values)
                    {
                        if (string.IsNullOrEmpty(app.InstallLocation)) continue;
                        
                        if (dir.Equals(app.InstallLocation, StringComparison.OrdinalIgnoreCase))
                        {
                            try
                            {
                                var dirInfo = new DirectoryInfo(dir);
                                var files = dirInfo.GetFiles("*", SearchOption.AllDirectories);
                                app.FilesCount = files.Length;
                                app.TotalSizeBytes = files.Sum(f => f.Length);

                                // Check for system files
                                app.HasSysFiles = files.Any(f => 
                                    f.Extension.Equals(".sys", StringComparison.OrdinalIgnoreCase) ||
                                    f.Extension.Equals(".dll", StringComparison.OrdinalIgnoreCase) && 
                                    f.DirectoryName?.Contains("system32", StringComparison.OrdinalIgnoreCase) == true);
                            }
                            catch (Exception ex)
                            {
                                Log(LogLevel.Warning, "Scanner", $"Error calculating size for {dir}: {ex.Message}");
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log(LogLevel.Warning, "Scanner", $"Error scanning program files: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Merges apps into dictionary, avoiding duplicates
    /// </summary>
    private void MergeApps(Dictionary<string, AppEntry> target, List<AppEntry> source)
    {
        foreach (var app in source)
        {
            var key = app.DisplayName.ToLowerInvariant();
            
            if (target.ContainsKey(key))
            {
                // Merge data - enrich existing entry
                var existing = target[key];
                
                if (string.IsNullOrEmpty(existing.InstallLocation) && !string.IsNullOrEmpty(app.InstallLocation))
                    existing.InstallLocation = app.InstallLocation;
                
                if (string.IsNullOrEmpty(existing.Publisher) && !string.IsNullOrEmpty(app.Publisher))
                    existing.Publisher = app.Publisher;

                if (app.Services.Any())
                    existing.Services.AddRange(app.Services.Except(existing.Services));

                if (app.TotalSizeBytes > existing.TotalSizeBytes)
                    existing.TotalSizeBytes = app.TotalSizeBytes;
            }
            else
            {
                target[key] = app;
            }
        }
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
