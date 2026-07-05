using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using LeakGuard.Data;
using LeakGuard.Models;
using System.Text.RegularExpressions;
using System.Collections.Concurrent;
using DocumentFormat.OpenXml.Packaging;
using UglyToad.PdfPig;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using LeakGuard.Hubs;

namespace LeakGuard.Services
{
    public class EndpointScannerService : BackgroundService
    {
        private readonly ILogger<EndpointScannerService> _logger;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IHubContext<IncidentHub> _hubContext;
        public static System.Collections.Concurrent.ConcurrentQueue<string> SimulatorQueue = new();

        private readonly ConcurrentDictionary<string, FileSystemWatcher> _watchers = new(StringComparer.OrdinalIgnoreCase);
        private readonly ConcurrentDictionary<string, DateTime> _recentlyProcessed = new();
        
        private readonly string QuarantineDir = Path.Combine(Directory.GetCurrentDirectory(), "App_Data", "LeakGuard_Quarantine");
        private readonly string TestMonitorDir = Path.Combine(Directory.GetCurrentDirectory(), "App_Data", "Monitored_Test");

        private static readonly HashSet<string> _excludedFolderNames = new(StringComparer.OrdinalIgnoreCase)
        {
            "Windows", "Program Files", "Program Files (x86)", "ProgramData",
            "$Recycle.Bin", "System Volume Information", "Recovery",
            "node_modules", ".git", "bin", "obj", ".vs", "AppData", "LeakGuard_Quarantine",
            ".claude"
        };

        private static readonly HashSet<string> _binaryExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".exe", ".dll", ".sys", ".bin", ".iso", ".img", ".mp4", ".mp3",
            ".avi", ".mkv", ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".zip",
            ".rar", ".7z", ".tar", ".gz", ".msi", ".pdb", ".class", ".pyc"
        };

        public EndpointScannerService(ILogger<EndpointScannerService> logger, IServiceScopeFactory scopeFactory, IHubContext<IncidentHub> hubContext)
        {
            _logger = logger;
            _scopeFactory = scopeFactory;
            _hubContext = hubContext;

            if (!Directory.Exists(QuarantineDir))
                Directory.CreateDirectory(QuarantineDir);

            if (!Directory.Exists(TestMonitorDir))
                Directory.CreateDirectory(TestMonitorDir);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("LeakGuard Endpoint Scanner Service is starting.");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await EnsureDefaultDataAsync();
                    SetupWatchers();

                    // Process any simulated files (bypassing FileSystemWatcher which is blocked on MonsterASP)
                    while (SimulatorQueue.TryDequeue(out string simulatedFile))
                    {
                        await ProcessFileAsync(simulatedFile);
                    }

                    await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
                    SetupWatchers();

                    var cutoff = DateTime.UtcNow.AddSeconds(-30);
                    foreach (var key in _recentlyProcessed.Keys.ToList())
                        if (_recentlyProcessed.TryGetValue(key, out var t) && t < cutoff)
                            _recentlyProcessed.TryRemove(key, out _);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in EndpointScannerService background loop.");
                    await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken); // Wait before retrying
                }
            }

            foreach (var watcher in _watchers.Values) watcher.Dispose();
        }

        private async Task EnsureDefaultDataAsync()
        {
            using var scope = _scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<LeakGuardDbContext>();

            if (!dbContext.Endpoints.Any())
                dbContext.Endpoints.Add(new LeakGuard.Models.Endpoint { Hostname = Environment.MachineName, IPAddress = "127.0.0.1" });

            if (!dbContext.ActionTypes.Any())
            {
                dbContext.ActionTypes.Add(new ActionType { ActionName = "Alerted" });
                dbContext.ActionTypes.Add(new ActionType { ActionName = "Quarantined" });
                dbContext.ActionTypes.Add(new ActionType { ActionName = "Blocked" });
                await dbContext.SaveChangesAsync();
            }

            if (!dbContext.RuleTypes.Any())
            {
                dbContext.RuleTypes.Add(new RuleType { TypeName = "Keyword" });
                dbContext.RuleTypes.Add(new RuleType { TypeName = "Regex" });
                await dbContext.SaveChangesAsync();
            }

            if (!dbContext.Rules.Any())
            {
                var keywordType = dbContext.RuleTypes.First(rt => rt.TypeName == "Keyword");
                var regexType = dbContext.RuleTypes.First(rt => rt.TypeName == "Regex");
                var alertAction = dbContext.ActionTypes.First(a => a.ActionName == "Alerted");
                var quarantineAction = dbContext.ActionTypes.First(a => a.ActionName == "Quarantined");

                dbContext.Rules.Add(new Rule 
                { 
                    RuleName = "Confidential Document", 
                    RuleTypeID = keywordType.RuleTypeID, 
                    MatchValue = "CONFIDENTIAL", 
                    ActionTypeID = quarantineAction.ActionTypeID,
                    IsActive = true 
                });

                dbContext.Rules.Add(new Rule 
                { 
                    RuleName = "Credit Card Numbers", 
                    RuleTypeID = regexType.RuleTypeID, 
                    MatchValue = @"\b(?:\d[ -]*?){13,16}\b", 
                    ActionTypeID = alertAction.ActionTypeID,
                    IsActive = true 
                });

                dbContext.Rules.Add(new Rule 
                { 
                    RuleName = "Social Security Number", 
                    RuleTypeID = regexType.RuleTypeID, 
                    MatchValue = @"\b\d{3}-\d{2}-\d{4}\b", 
                    ActionTypeID = alertAction.ActionTypeID,
                    IsActive = true 
                });

                await dbContext.SaveChangesAsync();
            }

            // Seed Cloud Test Directory
            var testDirPath = Path.Combine(Directory.GetCurrentDirectory(), "App_Data", "Monitored_Test");
            if (!dbContext.MonitoredDirectories.Any(d => d.DirectoryPath == testDirPath))
            {
                dbContext.MonitoredDirectories.Add(new MonitoredDirectory { DirectoryPath = testDirPath, Status = "Enabled" });
                await dbContext.SaveChangesAsync();
            }
        }

        private void SetupWatchers()
        {
            using var scope = _scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<LeakGuardDbContext>();

            var activeDirectories = dbContext.MonitoredDirectories
                .Where(d => d.Status == "Enabled")
                .ToList();

            var currentPaths = activeDirectories.Select(d => d.DirectoryPath).ToHashSet(StringComparer.OrdinalIgnoreCase);
            foreach (var path in _watchers.Keys.Where(p => !currentPaths.Contains(p)).ToList())
            {
                if (_watchers.TryRemove(path, out var w)) { w.Dispose(); _logger.LogInformation($"Stopped watching: {path}"); }
            }

            foreach (var dir in activeDirectories)
            {
                if (_watchers.ContainsKey(dir.DirectoryPath)) continue;

                if (!Directory.Exists(dir.DirectoryPath)) continue;

                var watcher = new FileSystemWatcher(dir.DirectoryPath)
                {
                    IncludeSubdirectories = true,
                    NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite,
                    InternalBufferSize = 65536
                };

                watcher.Created += async (s, e) => await ProcessFileAsync(e.FullPath);
                watcher.Changed += async (s, e) => await ProcessFileAsync(e.FullPath);

                watcher.EnableRaisingEvents = true;
                _watchers.TryAdd(dir.DirectoryPath, watcher);
            }
        }

        private bool ShouldSkip(string filePath)
        {
            var ext = Path.GetExtension(filePath);
            if (!string.IsNullOrEmpty(ext) && _binaryExtensions.Contains(ext)) return true;

            var parts = filePath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            foreach (var part in parts)
                if (_excludedFolderNames.Contains(part)) return true;

            try { if (new FileInfo(filePath).Length > 10 * 1024 * 1024) return true; }
            catch { return true; }

            return false;
        }

        private async Task<string> ExtractTextAsync(string filePath)
        {
            var ext = Path.GetExtension(filePath).ToLowerInvariant();
            
            if (ext == ".pdf")
            {
                return await Task.Run(() =>
                {
                    try
                    {
                        using var pdf = PdfDocument.Open(filePath);
                        return string.Join(" ", pdf.GetPages().Select(p => p.Text));
                    }
                    catch { return string.Empty; }
                });
            }
            
            if (ext == ".docx")
            {
                return await Task.Run(() =>
                {
                    try
                    {
                        using var doc = WordprocessingDocument.Open(filePath, false);
                        return doc.MainDocumentPart?.Document.Body?.InnerText ?? string.Empty;
                    }
                    catch { return string.Empty; }
                });
            }

            // Default text reader for txt, csv, log, etc
            using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var reader = new StreamReader(stream);
            return await reader.ReadToEndAsync();
        }

        private async Task ProcessFileAsync(string filePath)
        {
            try
            {
                // SMB copies fire several Created/Changed events for one file, so
                // ignore repeat events for the same path within this window.
                var now = DateTime.UtcNow;
                if (_recentlyProcessed.TryGetValue(filePath, out var lastProcessed) && (now - lastProcessed).TotalSeconds < 15)
                    return;
                _recentlyProcessed[filePath] = now;

                await Task.Delay(500);

                if (!File.Exists(filePath) || ShouldSkip(filePath)) return;

                string content = await ExtractTextAsync(filePath) ?? "";
                // We no longer skip empty files because extension rules should still be checked!

                using var scope = _scopeFactory.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<LeakGuardDbContext>();

                var activeRules = dbContext.Rules.Include(r => r.ActionType).Where(r => r.IsActive).ToList();
                if (!activeRules.Any()) return;

                var currentEndpoint = dbContext.Endpoints.FirstOrDefault(e => e.Hostname == Environment.MachineName)
                                      ?? dbContext.Endpoints.First();

                foreach (var rule in activeRules)
                {
                    bool isMatch = false;
                    string matchedText = "";

                    if (rule.MatchValue.StartsWith("."))
                    {
                        var extensions = rule.MatchValue.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                        var fileExt = Path.GetExtension(filePath);
                        foreach (var ext in extensions)
                        {
                            if (string.Equals(fileExt, ext, StringComparison.OrdinalIgnoreCase))
                            {
                                isMatch = true;
                                matchedText = $"Restricted File Type: {ext}";
                                break;
                            }
                        }
                    }
                    else
                    {
                        try
                        {
                            var match = Regex.Match(content, rule.MatchValue, RegexOptions.IgnoreCase, TimeSpan.FromSeconds(1));
                            if (match.Success) { isMatch = true; matchedText = match.Value; }
                        }
                        catch
                        {
                            if (content.Contains(rule.MatchValue, StringComparison.OrdinalIgnoreCase))
                            {
                                isMatch = true;
                                matchedText = rule.MatchValue;
                            }
                        }
                    }

                    if (isMatch)
                    {
                        var ruleAction = rule.ActionType?.ActionName ?? "Alerted";
                        var actionTypeObj = dbContext.ActionTypes.FirstOrDefault(a => a.ActionName == ruleAction);
                        
                        // Execute Enforcement Action with Retry for locked files
                        bool actionSuccess = false;
                        int retries = 5;
                        while (retries > 0 && !actionSuccess)
                        {
                            try
                            {
                                if (ruleAction == "Quarantined")
                                {
                                    var safeName = $"{DateTime.UtcNow:yyyyMMddHHmmss}_{Path.GetFileName(filePath)}";
                                    var dest = Path.Combine(QuarantineDir, safeName);
                                    File.Move(filePath, dest);
                                    actionSuccess = true;
                                }
                                else if (ruleAction == "Blocked")
                                {
                                    File.Delete(filePath);
                                    actionSuccess = true;
                                }
                                else
                                {
                                    actionSuccess = true; // Alerted
                                }
                            }
                            catch (IOException)
                            {
                                retries--;
                                if (retries > 0) await Task.Delay(1000); // Wait 1s and retry if locked
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(ex, $"Failed to execute {ruleAction} on file: {filePath}");
                                break;
                            }
                        }

                        if (!actionSuccess && ruleAction != "Alerted")
                        {
                            _logger.LogWarning($"Could not apply {ruleAction} to {filePath} after retries. File may still be locked.");
                        }

                        var incident = new Incident
                        {
                            FilePath = filePath,
                            MatchedText = matchedText.Length > 200 ? matchedText[..200] : matchedText,
                            RuleID = rule.RuleID,
                            Timestamp = DateTime.UtcNow,
                            EndpointID = currentEndpoint.EndpointID,
                            ActionTypeID = actionTypeObj?.ActionTypeID ?? 1
                        };

                        dbContext.Incidents.Add(incident);
                        await dbContext.SaveChangesAsync();

                        // Notify Dashboard via SignalR
                        await _hubContext.Clients.All.SendAsync("ReceiveIncident", new {
                            IncidentId = incident.IncidentID,
                            Hostname = currentEndpoint.Hostname,
                            RuleName = rule.RuleName,
                            MatchValue = rule.MatchValue,
                            MatchedText = incident.MatchedText,
                            Action = ruleAction,
                            FilePath = filePath,
                            Timestamp = incident.Timestamp
                        });

                        _logger.LogWarning($"[LEAK {ruleAction.ToUpper()}] Rule: '{rule.RuleName}' | File: {filePath}");
                        break; 
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error processing file '{filePath}': {ex.Message}");
            }
        }
    }
}
