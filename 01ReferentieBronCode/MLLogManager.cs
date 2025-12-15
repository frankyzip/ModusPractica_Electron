using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Windows.Controls;

namespace ModusPractica
{
    /// <summary>
    /// Represents a snapshot of log statistics for a specific time period.
    /// Used for historical analysis and trend monitoring in the dashboard.
    /// </summary>
    public class LogStatisticsSnapshot
    {
        public DateTime SnapshotDate { get; set; }
        public DateTime PeriodStart { get; set; }
        public DateTime PeriodEnd { get; set; }

        // Counts
        public int TotalLogs { get; set; }
        public int SchedulerInits { get; set; }
        public int MLPredictions { get; set; }
        public int TrainingSessions { get; set; }
        public int Errors { get; set; }
        public int Warnings { get; set; }

        // Predictions breakdown
        public int RawPredictions { get; set; }
        public int ClampedPredictions { get; set; }
        public int FallbacksUsed { get; set; }

        // Instance tracking
        public HashSet<string> UniqueSchedulerInstances { get; set; } = new HashSet<string>();
    }

    /// <summary>
    /// Central logging manager for the ModusPractica application.
    /// Handles application-wide logging with file persistence and UI display.
    /// Legacy name retained for backward compatibility.
    /// </summary>
    public class MLLogManager
    {
        private static MLLogManager? _instance;
        private List<string> _logEntries;
        private string? _logFilePath;
        private string? _statsArchivePath;
        private TextBox? _logTextBox;
        private bool _isInitialized = false;

        private const int MaxLogEntries = 5000; // hard cap for in-memory log entries
        private const int MaxUiLines = 1000;     // UI shows only the last N lines to keep TextBox lightweight
        private const int MaxStatsSnapshots = 100; // Keep last 100 snapshots (~50KB)

        private List<LogStatisticsSnapshot> _statsHistory = new List<LogStatisticsSnapshot>();

        public static MLLogManager Instance => _instance ??= new MLLogManager();

        private MLLogManager()
        {
            _logEntries = new List<string>();
        }

        public void InitializeForUser(string profileName)
        {
            string logFolder = DataPathProvider.GetLogsFolder(profileName);

            _logFilePath = Path.Combine(logFolder, "application_log.txt");
            _statsArchivePath = Path.Combine(logFolder, "log_statistics_archive.json");

            _logEntries.Clear();
            LoadLogFromFile();
            LoadStatsArchive();
            _isInitialized = true;
        }

        public void SetLogTextBox(TextBox textBox)
        {
            _logTextBox = textBox;
            // Reset incremental state so next update will render tail correctly
            _lastDisplayedIndex = 0;
            _logBufferTrimmed = true;
            UpdateLogDisplay();
        }

        // Track UI state for incremental updates
        private int _lastDisplayedIndex = 0;
        private bool _logBufferTrimmed = false;

        public void Log(string message, LogLevel level = LogLevel.Info)
        {
            if (!_isInitialized) return;

            string formattedMessage = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [{level}] {message}";
            _logEntries.Add(formattedMessage);

            if (_logEntries.Count > MaxLogEntries)
            {
                _logEntries.RemoveAt(0);
                _logBufferTrimmed = true; // signal that UI needs a full refresh with tail only
            }

            AppendToLogFile(formattedMessage);
            UpdateLogDisplay();
        }

        public void LogError(string message, Exception ex)
        {
            Log($"{message}: {ex.Message}", LogLevel.Error);
            if (ex.StackTrace != null)
            {
                Log($"StackTrace: {ex.StackTrace}", LogLevel.Debug);
            }
        }

        public void ClearLog()
        {
            if (!_isInitialized) return;

            // ARCHIVE STATISTICS BEFORE CLEARING
            if (_logEntries.Count > 0)
            {
                var snapshot = CreateSnapshot();
                _statsHistory.Add(snapshot);

                // Keep only last MaxStatsSnapshots (FIFO rolling window)
                if (_statsHistory.Count > MaxStatsSnapshots)
                {
                    _statsHistory.RemoveAt(0);
                }

                SaveStatsArchive();

                Log($"📊 Statistics saved: {snapshot.TotalLogs} logs from {snapshot.PeriodStart:g} to {snapshot.PeriodEnd:g}", LogLevel.Info);
            }

            _logEntries.Clear();
            try
            {
                if (_logFilePath != null)
                {
                    File.WriteAllText(_logFilePath, string.Empty);
                }
            }
            catch (Exception ex)
            {
                // Last resort logging for LogManager internal errors - cannot use self-logging here
                Console.WriteLine($"Error clearing log file: {ex.Message}");
            }
            UpdateLogDisplay();
        }

        private void UpdateLogDisplay()
        {
            if (_logTextBox != null)
            {
                _logTextBox.Dispatcher.Invoke(() =>
                {
                    // If buffer trimmed or first-time attach, rebuild UI with tail (MaxUiLines)
                    if (_logBufferTrimmed || _lastDisplayedIndex == 0)
                    {
                        var tail = _logEntries.Count <= MaxUiLines
                            ? _logEntries
                            : _logEntries.Skip(_logEntries.Count - MaxUiLines);

                        _logTextBox.SetCurrentValue(TextBox.TextProperty, string.Join(Environment.NewLine, tail));
                        _lastDisplayedIndex = _logEntries.Count;
                        _logBufferTrimmed = false;
                        _logTextBox.ScrollToEnd();
                        return;
                    }

                    // Incremental append of only new entries
                    if (_lastDisplayedIndex < _logEntries.Count)
                    {
                        var newEntries = _logEntries.Skip(_lastDisplayedIndex);
                        string appendText = string.Join(Environment.NewLine, newEntries);

                        if (string.IsNullOrEmpty(_logTextBox.Text))
                        {
                            // If TextBox was cleared externally, rebuild tail
                            var tail = _logEntries.Count <= MaxUiLines
                                ? _logEntries
                                : _logEntries.Skip(_logEntries.Count - MaxUiLines);
                            _logTextBox.SetCurrentValue(TextBox.TextProperty, string.Join(Environment.NewLine, tail));
                        }
                        else
                        {
                            // Prepend newline only when existing content present
                            if (!string.IsNullOrEmpty(appendText))
                            {
                                _logTextBox.AppendText(Environment.NewLine + appendText);
                            }
                        }

                        _lastDisplayedIndex = _logEntries.Count;
                        _logTextBox.ScrollToEnd();
                    }
                });
            }
        }

        private void AppendToLogFile(string logEntry)
        {
            if (!_isInitialized || _logFilePath == null) return;
            try
            {
                using (StreamWriter writer = File.AppendText(_logFilePath))
                {
                    writer.WriteLine(logEntry);
                }
            }
            catch (Exception ex)
            {
                // Last resort logging for LogManager internal errors - cannot use self-logging here
                Console.WriteLine($"Error writing to log file: {ex.Message}");
            }
        }

        private void LoadLogFromFile()
        {
            if (string.IsNullOrEmpty(_logFilePath) || !File.Exists(_logFilePath)) return;
            try
            {
                string[] lines = File.ReadAllLines(_logFilePath);
                _logEntries.AddRange(lines.Length > MaxLogEntries ? lines.Skip(lines.Length - MaxLogEntries) : lines);
            }
            catch (Exception ex)
            {
                // Last resort logging for LogManager internal errors - cannot use self-logging here
                Console.WriteLine($"Error loading log file: {ex.Message}");
            }
        }

        // --- Add these fields inside the LogManager class ---
        private readonly Dictionary<string, DateTime> _lastLogTimes = new();
        private readonly HashSet<string> _logOnceKeys = new();
        private readonly object _throttleLock = new();

        // --- Add these helper methods inside the class ---
        private bool ShouldLogNow(string key, TimeSpan minInterval)
        {
            lock (_throttleLock)
            {
                if (_lastLogTimes.TryGetValue(key, out var last) && DateTime.UtcNow - last < minInterval)
                    return false;
                _lastLogTimes[key] = DateTime.UtcNow;
                return true;
            }
        }

        /// <summary>
        /// Log a message at most once per specified interval (throttling).
        /// Typical use: noisy UI ticks, repeated "sync progress" dumps, etc.
        /// </summary>
        public void LogThrottled(string key, TimeSpan interval, string message, LogLevel level = LogLevel.Debug)
        {
            if (ShouldLogNow(key, interval))
                Log(message, level);
        }

        /// <summary>
        /// Log a message only once for the entire app lifetime (until restart).
        /// Typical use: "Alarm sound loaded", feature toggles, environment notes.
        /// </summary>
        public void LogOnce(string key, string message, LogLevel level = LogLevel.Info)
        {
            lock (_throttleLock)
            {
                if (_logOnceKeys.Contains(key)) return;
                _logOnceKeys.Add(key);
            }
            Log(message, level);
        }

        // Convenience wrappers for common noisy categories
        public void LogSyncProgress(string message)
            => LogThrottled("SyncProgress", TimeSpan.FromSeconds(10), message, LogLevel.Debug);

        public void LogMlStatusUpdated(string message = "ML status display updated successfully.")
            => LogThrottled("MlStatusUpdate", TimeSpan.FromSeconds(5), message, LogLevel.Debug);

        public void LogAlarmLoadedOnce(string message = "Alarm sound loaded successfully.")
            => LogOnce("AlarmSoundLoaded", message, LogLevel.Debug);

        // ============================================================================
        // STATISTICS ARCHIVE METHODS
        // ============================================================================

        /// <summary>
        /// Creates a snapshot of current log statistics.
        /// </summary>
        private LogStatisticsSnapshot CreateSnapshot()
        {
            var snapshot = new LogStatisticsSnapshot
            {
                SnapshotDate = DateTime.Now,
                TotalLogs = _logEntries.Count
            };

            // Parse timestamps to find period
            var timestamps = new List<DateTime>();
            foreach (var entry in _logEntries)
            {
                var match = Regex.Match(entry, @"\[(\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2})\]");
                if (match.Success && DateTime.TryParse(match.Groups[1].Value, out var timestamp))
                {
                    timestamps.Add(timestamp);
                }
            }

            if (timestamps.Any())
            {
                snapshot.PeriodStart = timestamps.Min();
                snapshot.PeriodEnd = timestamps.Max();
            }
            else
            {
                snapshot.PeriodStart = DateTime.Now;
                snapshot.PeriodEnd = DateTime.Now;
            }

            // Count categories by searching for specific patterns
            snapshot.SchedulerInits = _logEntries.Count(e => e.Contains("[SCHEDULER INIT]"));
            snapshot.MLPredictions = _logEntries.Count(e => e.Contains("[ML PREDICT"));
            snapshot.TrainingSessions = _logEntries.Count(e => e.Contains("Training started") || e.Contains("[TRAIN START]"));
            snapshot.Errors = _logEntries.Count(e => e.Contains("[Error]"));
            snapshot.Warnings = _logEntries.Count(e => e.Contains("[Warning]"));
            snapshot.RawPredictions = _logEntries.Count(e => e.Contains("[ML PREDICT RAW]"));
            snapshot.ClampedPredictions = _logEntries.Count(e => e.Contains("[ML PREDICT CLAMP]"));
            snapshot.FallbacksUsed = _logEntries.Count(e => e.Contains("onvoldoende geschiedenis") || e.Contains("model null"));

            // Extract unique scheduler instances
            foreach (var entry in _logEntries)
            {
                var match = Regex.Match(entry, @"instance (\d+)");
                if (match.Success)
                {
                    snapshot.UniqueSchedulerInstances.Add(match.Groups[1].Value);
                }
            }

            return snapshot;
        }

        /// <summary>
        /// Loads statistics archive from JSON file.
        /// </summary>
        private void LoadStatsArchive()
        {
            _statsHistory.Clear();

            if (string.IsNullOrEmpty(_statsArchivePath) || !File.Exists(_statsArchivePath))
                return;

            try
            {
                string json = File.ReadAllText(_statsArchivePath);
                var loaded = JsonSerializer.Deserialize<List<LogStatisticsSnapshot>>(json);
                if (loaded != null)
                {
                    _statsHistory = loaded;
                }
            }
            catch (Exception ex)
            {
                // Last resort logging for LogManager internal errors - cannot use self-logging here
                Console.WriteLine($"Error loading stats archive: {ex.Message}");
            }
        }

        /// <summary>
        /// Saves statistics archive to JSON file.
        /// </summary>
        private void SaveStatsArchive()
        {
            if (string.IsNullOrEmpty(_statsArchivePath))
                return;

            try
            {
                var options = new JsonSerializerOptions { WriteIndented = true };
                string json = JsonSerializer.Serialize(_statsHistory, options);
                File.WriteAllText(_statsArchivePath, json);
            }
            catch (Exception ex)
            {
                // Last resort logging for LogManager internal errors - cannot use self-logging here
                Console.WriteLine($"Error saving stats archive: {ex.Message}");
            }
        }

        /// <summary>
        /// Gets the statistics history (last 100 snapshots by default).
        /// For dashboard display and recent trend analysis.
        /// </summary>
        public List<LogStatisticsSnapshot> GetStatsHistory()
        {
            return new List<LogStatisticsSnapshot>(_statsHistory);
        }

        /// <summary>
        /// Gets the FULL statistics history without limit.
        /// Use this method explicitly when you need complete historical analysis.
        /// Warning: May return large dataset if archive has grown.
        /// </summary>
        public List<LogStatisticsSnapshot> GetFullStatsHistory()
        {
            return new List<LogStatisticsSnapshot>(_statsHistory);
        }

        /// <summary>
        /// Gets statistics for the current log session (not yet persisted to a summary file).
        /// </summary>
        public LogStatisticsSnapshot GetCurrentStats()
        {
            return CreateSnapshot();
        }

        /// <summary>
        /// Gets recent statistics (last N snapshots).
        /// </summary>
        public List<LogStatisticsSnapshot> GetRecentStats(int count)
        {
            return _statsHistory.TakeLast(count).ToList();
        }

        /// <summary>
        /// Gets statistics within a date range.
        /// </summary>
        public List<LogStatisticsSnapshot> GetStatsByDateRange(DateTime startDate, DateTime endDate)
        {
            return _statsHistory
                .Where(s => s.SnapshotDate >= startDate && s.SnapshotDate <= endDate)
                .ToList();
        }

        /// <summary>
        /// Exports statistics to CSV format for external analysis.
        /// </summary>
        public void ExportStatsToCSV(string filePath)
        {
            try
            {
                var csv = new System.Text.StringBuilder();
                csv.AppendLine("SnapshotDate,PeriodStart,PeriodEnd,TotalLogs,SchedulerInits,MLPredictions,TrainingSessions,Errors,Warnings,RawPredictions,ClampedPredictions,FallbacksUsed,UniqueInstances");

                foreach (var snapshot in _statsHistory)
                {
                    csv.AppendLine($"{snapshot.SnapshotDate:yyyy-MM-dd HH:mm:ss}," +
                                  $"{snapshot.PeriodStart:yyyy-MM-dd HH:mm:ss}," +
                                  $"{snapshot.PeriodEnd:yyyy-MM-dd HH:mm:ss}," +
                                  $"{snapshot.TotalLogs}," +
                                  $"{snapshot.SchedulerInits}," +
                                  $"{snapshot.MLPredictions}," +
                                  $"{snapshot.TrainingSessions}," +
                                  $"{snapshot.Errors}," +
                                  $"{snapshot.Warnings}," +
                                  $"{snapshot.RawPredictions}," +
                                  $"{snapshot.ClampedPredictions}," +
                                  $"{snapshot.FallbacksUsed}," +
                                  $"{snapshot.UniqueSchedulerInstances.Count}");
                }

                File.WriteAllText(filePath, csv.ToString());
            }
            catch (Exception ex)
            {
                // Last resort logging for LogManager internal errors - cannot use self-logging here
                Console.WriteLine($"Error exporting stats to CSV: {ex.Message}");
            }
        }
    }

    public enum LogLevel
    {
        Debug,
        Info,
        Warning,
        Error
    }
}