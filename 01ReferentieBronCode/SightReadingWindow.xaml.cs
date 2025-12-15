using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Threading;

namespace ModusPractica
{
    public partial class SightReadingWindow : Window
    {
        private readonly DispatcherTimer _timer;
        private DateTime _startTime;
        private TimeSpan _pausedAccumulated;
        private bool _running;
        private bool _isPaused;

        // Totals per level (1..10) - loaded from file
        private readonly Dictionary<int, TimeSpan> _levelTotals = new();
        private string _levelTotalsFilePath;

        public SightReadingWindow()
        {
            InitializeComponent();

            // Setup file path for storing level totals
            string appDataPath = DataPathProvider.GetSightReadingFolder(ActiveUserSession.ProfileName);
            _levelTotalsFilePath = Path.Combine(appDataPath, "sight_reading_totals.json");

            // Load saved level totals
            LoadLevelTotals();

            _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _timer.Tick += (_, __) => UpdateElapsed();

            // Defer button update until controls are loaded
            Loaded += (s, e) => RefreshLevelButtons();
        }

        private void LoadLevelTotals()
        {
            // Initialize with zeros
            for (int i = 1; i <= 10; i++)
                _levelTotals[i] = TimeSpan.Zero;

            // Try to load from file
            try
            {
                if (File.Exists(_levelTotalsFilePath))
                {
                    string json = File.ReadAllText(_levelTotalsFilePath);
                    var loaded = JsonSerializer.Deserialize<Dictionary<int, long>>(json);

                    if (loaded != null)
                    {
                        foreach (var kvp in loaded)
                        {
                            if (kvp.Key >= 1 && kvp.Key <= 10)
                            {
                                _levelTotals[kvp.Key] = TimeSpan.FromTicks(kvp.Value);
                            }
                        }
                        MLLogManager.Instance.Log($"Successfully loaded sight reading totals from {_levelTotalsFilePath}", LogLevel.Info);
                    }
                }
                else
                {
                    MLLogManager.Instance.Log($"No saved totals file found at {_levelTotalsFilePath}, attempting to rebuild from history", LogLevel.Info);
                    RebuildTotalsFromHistory();
                }
            }
            catch (Exception ex)
            {
                MLLogManager.Instance.LogError("Failed to load sight reading totals, attempting to rebuild from history", ex);
                RebuildTotalsFromHistory();
            }
        }

        private void RebuildTotalsFromHistory()
        {
            try
            {
                var allHistory = PracticeHistoryManager.Instance.GetAllHistory();
                var sightReadingHistory = allHistory
                    .Where(h => h.MusicPieceTitle != null && h.MusicPieceTitle.StartsWith("Sight Reading - Level "))
                    .ToList();

                foreach (var entry in sightReadingHistory)
                {
                    // Extract level number from title "Sight Reading - Level X"
                    var titleParts = entry.MusicPieceTitle.Split(' ');
                    if (titleParts.Length >= 4 && int.TryParse(titleParts[3], out int level))
                    {
                        if (level >= 1 && level <= 10)
                        {
                            _levelTotals[level] += entry.Duration;
                        }
                    }
                }

                // Save the rebuilt totals
                SaveLevelTotals();
                MLLogManager.Instance.Log($"Rebuilt sight reading totals from {sightReadingHistory.Count} history entries", LogLevel.Info);
            }
            catch (Exception ex)
            {
                MLLogManager.Instance.LogError("Failed to rebuild sight reading totals from history", ex);
            }
        }

        private void SaveLevelTotals()
        {
            try
            {
                // Convert TimeSpan to ticks for JSON serialization
                var toSave = new Dictionary<int, long>();
                foreach (var kvp in _levelTotals)
                {
                    toSave[kvp.Key] = kvp.Value.Ticks;
                }

                string json = JsonSerializer.Serialize(toSave, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_levelTotalsFilePath, json);
                MLLogManager.Instance.Log($"Saved sight reading totals to {_levelTotalsFilePath}", LogLevel.Info);
            }
            catch (Exception ex)
            {
                MLLogManager.Instance.LogError("Failed to save sight reading totals", ex);
            }
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            // Save any running timer before closing
            if (_running || _pausedAccumulated > TimeSpan.Zero)
            {
                var level = GetSelectedLevel();
                if (level.HasValue)
                {
                    if (_running)
                    {
                        _timer.Stop();
                        var elapsed = DateTime.Now - _startTime;
                        _pausedAccumulated += elapsed;
                    }

                    if (_pausedAccumulated > TimeSpan.Zero)
                    {
                        _levelTotals[level.Value] += _pausedAccumulated;
                        SavePracticeSession(level.Value, _pausedAccumulated);
                        SaveLevelTotals();
                    }
                }
            }
        }

        private int? GetSelectedLevel()
        {
            if (BtnLevel1.IsChecked == true) return 1;
            if (BtnLevel2.IsChecked == true) return 2;
            if (BtnLevel3.IsChecked == true) return 3;
            if (BtnLevel4.IsChecked == true) return 4;
            if (BtnLevel5.IsChecked == true) return 5;
            if (BtnLevel6.IsChecked == true) return 6;
            if (BtnLevel7.IsChecked == true) return 7;
            if (BtnLevel8.IsChecked == true) return 8;
            if (BtnLevel9.IsChecked == true) return 9;
            if (BtnLevel10.IsChecked == true) return 10;
            return null;
        }

        private void LevelButton_Checked(object sender, RoutedEventArgs e)
        {
            // Ensure only one level button is checked at a time
            if (sender is ToggleButton checkedButton)
            {
                var allLevelButtons = new[] { BtnLevel1, BtnLevel2, BtnLevel3, BtnLevel4, BtnLevel5,
                                               BtnLevel6, BtnLevel7, BtnLevel8, BtnLevel9, BtnLevel10 };

                foreach (var btn in allLevelButtons)
                {
                    if (btn != checkedButton && btn.IsChecked == true)
                    {
                        btn.IsChecked = false;
                    }
                }
            }
        }

        private void BtnStart_Click(object sender, RoutedEventArgs e)
        {
            if (_running && !_isPaused)
                return;

            var level = GetSelectedLevel();
            if (!level.HasValue)
            {
                MessageBox.Show("Please select a level before starting.", "No Level Selected", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (_isPaused)
            {
                // Resume from pause
                _startTime = DateTime.Now;
                _isPaused = false;
            }
            else
            {
                // Fresh start
                _startTime = DateTime.Now;
                _pausedAccumulated = TimeSpan.Zero;
            }

            _running = true;
            _timer.Start();

            // Update button states
            BtnStart.IsEnabled = false;
            BtnPause.IsEnabled = true;
            BtnStop.IsEnabled = true;
        }

        private void BtnPause_Click(object sender, RoutedEventArgs e)
        {
            if (!_running || _isPaused)
                return;

            _timer.Stop();
            var elapsed = DateTime.Now - _startTime;
            _pausedAccumulated += elapsed;
            _running = false;
            _isPaused = true;
            UpdateElapsed();

            // Update button states
            BtnStart.IsEnabled = true;
            BtnPause.IsEnabled = false;
            BtnStop.IsEnabled = true;
        }

        private void BtnStop_Click(object sender, RoutedEventArgs e)
        {
            if (_running)
            {
                _timer.Stop();
                var elapsed = DateTime.Now - _startTime;
                _pausedAccumulated += elapsed;
                _running = false;
            }

            // Credit time to selected level and save to practice history
            var level = GetSelectedLevel();
            if (level.HasValue && _pausedAccumulated > TimeSpan.Zero)
            {
                _levelTotals[level.Value] += _pausedAccumulated;

                // Save to practice history
                SavePracticeSession(level.Value, _pausedAccumulated);

                // Save level totals to file
                SaveLevelTotals();

                _pausedAccumulated = TimeSpan.Zero;
                _isPaused = false;
                RefreshLevelButtons();
            }

            // Reset elapsed display
            TxtElapsed.Text = "00:00:00";

            // Update button states
            BtnStart.IsEnabled = true;
            BtnPause.IsEnabled = false;
            BtnStop.IsEnabled = false;
        }

        private void SavePracticeSession(int level, TimeSpan duration)
        {
            try
            {
                var entry = new PracticeHistory
                {
                    Id = Guid.NewGuid(),
                    MusicPieceTitle = $"Sight Reading - Level {level}",
                    BarSectionRange = level.ToString(),
                    Date = DateTime.Now,
                    Duration = duration,
                    Difficulty = "Sight Reading",
                    Notes = $"Sight reading practice session at level {level}"
                };

                // Use AddPracticeHistory instead of UpdatePracticeHistory
                // AddPracticeHistory adds a new entry to the history list
                PracticeHistoryManager.Instance.AddPracticeHistory(entry);

                MLLogManager.Instance.Log($"Sight Reading session saved: Level {level}, Duration {duration}", LogLevel.Info);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to save practice session: {ex.Message}", "Save Error", MessageBoxButton.OK, MessageBoxImage.Error);
                MLLogManager.Instance.LogError("Failed to save sight reading session", ex);
            }
        }

        private void UpdateElapsed()
        {
            if (_running)
            {
                var elapsed = (DateTime.Now - _startTime) + _pausedAccumulated;
                TxtElapsed.Text = Format(elapsed);
            }
            else
            {
                TxtElapsed.Text = Format(_pausedAccumulated);
            }
        }

        private void RefreshLevelButtons()
        {
            // Update time display in each level button
            UpdateLevelButtonTime(BtnLevel1, 1);
            UpdateLevelButtonTime(BtnLevel2, 2);
            UpdateLevelButtonTime(BtnLevel3, 3);
            UpdateLevelButtonTime(BtnLevel4, 4);
            UpdateLevelButtonTime(BtnLevel5, 5);
            UpdateLevelButtonTime(BtnLevel6, 6);
            UpdateLevelButtonTime(BtnLevel7, 7);
            UpdateLevelButtonTime(BtnLevel8, 8);
            UpdateLevelButtonTime(BtnLevel9, 9);
            UpdateLevelButtonTime(BtnLevel10, 10);

            // Update total time display
            UpdateTotalTime();
        }

        private void UpdateTotalTime()
        {
            TimeSpan totalTime = TimeSpan.Zero;
            for (int i = 1; i <= 10; i++)
            {
                totalTime += _levelTotals[i];
            }
            TxtTotalAllLevels.Text = Format(totalTime);
        }

        private void UpdateLevelButtonTime(ToggleButton button, int level)
        {
            var time = _levelTotals[level];
            var formattedTime = $"{(int)time.TotalMinutes:00}:{time.Seconds:00}";

            // Ensure template is applied
            button.ApplyTemplate();

            // Find the TextBlock in the button's template
            if (button.Template?.FindName("TimeText", button) is System.Windows.Controls.TextBlock timeText)
            {
                timeText.Text = formattedTime;
            }
            else
            {
                MLLogManager.Instance.Log($"Warning: Could not find TimeText in Level {level} button template", LogLevel.Warning);
            }
        }

        private static string Format(TimeSpan t)
            => $"{(int)t.TotalHours:00}:{t.Minutes:00}:{t.Seconds:00}";
    }
}
