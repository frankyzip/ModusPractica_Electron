using Microsoft.Win32;
using System.Globalization;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ModusPractica
{
    public partial class PracticeStatisticsWindow : Window
    {
        private DispatcherTimer _refreshTimer;
        private List<(DateTime Date, double Minutes)> practiceTimePerDay;
        private List<(string Label, double Minutes)> timePerCategory;
        private List<(string MusicPiece, double Minutes)> topMusicPieces;
        private List<(string MusicPiece, double Minutes)> topMusicPiecesMonth;

        public PracticeStatisticsWindow()
        {
            InitializeComponent();

            // Explicitly bind the event handler
            CmbPeriodFilter.SelectionChanged += CmbPeriodFilter_SelectionChanged;

            // Set default selection
            CmbPeriodFilter.SelectedIndex = 0;

            // Timer for manual refresh
            InitializeRefreshTimer();

            // When loaded: load data and draw appropriate chart
            Loaded += (s, e) =>
            {
                CollectPracticeTimes();
                UpdateStatistics();

                Dispatcher.BeginInvoke(new Action(() =>
                {
                    if (TabCharts.SelectedIndex == 0)
                        DrawChart();
                    else if (TabCharts.SelectedIndex == 1)
                    {
                        CollectPracticeTimesPerMusicPiece();
                        DrawMusicPiecesChart();
                    }
                    else if (TabCharts.SelectedIndex == 2)
                    {
                        CollectPracticeTimesPerMusicPieceMonth();
                        DrawMusicPiecesMonthChart();
                    }
                }), DispatcherPriority.Loaded);
            };

            // When switching tabs: draw the appropriate chart
            TabCharts.SelectionChanged += (s, e) =>
            {
                if (e.Source is not TabControl) return;

                if (TabCharts.SelectedIndex == 0)
                {
                    DrawChart();
                }
                else if (TabCharts.SelectedIndex == 1)
                {
                    CollectPracticeTimesPerMusicPiece();
                    DrawMusicPiecesChart();
                }
                else if (TabCharts.SelectedIndex == 2)
                {
                    CollectPracticeTimesPerMusicPieceMonth();
                    DrawMusicPiecesMonthChart();
                }
            };

            // On resize: only draw if the canvas is visible in the active tab
            CnvPracticeTimeChart.SizeChanged += (s, e) =>
            {
                if (TabCharts.SelectedIndex == 0)
                    DrawChart();
            };
            CnvDaysChart.SizeChanged += (s, e) =>
            {
                if (TabCharts.SelectedIndex == 1)
                    DrawMusicPiecesChart();
            };
            CnvMonthPiecesChart.SizeChanged += (s, e) =>
            {
                if (TabCharts.SelectedIndex == 2)
                    DrawMusicPiecesMonthChart();
            };

            // Live refresh when practice history changes
            PracticeHistoryManager.Instance.HistoryChanged += (_, __) =>
            {
                Dispatcher.Invoke(() =>
                {
                    CollectPracticeTimes();
                    UpdateStatistics();
                    if (TabCharts.SelectedIndex == 0) DrawChart();
                    else if (TabCharts.SelectedIndex == 1) { CollectPracticeTimesPerMusicPiece(); DrawMusicPiecesChart(); }
                    else if (TabCharts.SelectedIndex == 2) { CollectPracticeTimesPerMusicPieceMonth(); DrawMusicPiecesMonthChart(); }
                });
            };
        }

        private void BtnExport_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                SaveFileDialog saveFileDialog = new SaveFileDialog
                {
                    Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*",
                    Title = "Export practice statistics",
                    DefaultExt = "csv",
                    FileName = "ModusPractica_PracticeStatistics.csv"
                };

                if (saveFileDialog.ShowDialog() == true)
                {
                    StringBuilder csvContent = new StringBuilder();

                    // Period data
                    csvContent.AppendLine("Period,Practice Time (minutes)");
                    foreach (var item in timePerCategory)
                    {
                        csvContent.AppendLine($"{item.Label},{item.Minutes}");
                    }

                    // Daily data
                    csvContent.AppendLine();
                    csvContent.AppendLine("Date,Practice Time (minutes)");
                    foreach (var item in practiceTimePerDay)
                    {
                        csvContent.AppendLine($"{item.Date:yyyy-MM-dd},{item.Minutes}");
                    }

                    File.WriteAllText(saveFileDialog.FileName, csvContent.ToString());
                    MessageBox.Show($"Statistics exported to {saveFileDialog.FileName}",
                        "Export complete", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MLLogManager.Instance.LogError("Error exporting practice statistics.", ex);
                MessageBox.Show($"Error exporting: {ex.Message}", "Export error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void BtnRefresh_Click(object sender, RoutedEventArgs e)
        {
            _refreshTimer.Stop();
            _refreshTimer.Start();
        }

        private void CmbPeriodFilter_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                CollectPracticeTimes();
                UpdateStatistics();

                // Redraw the currently selected chart
                if (TabCharts.SelectedIndex == 0)
                {
                    DrawChart();
                }
                else if (TabCharts.SelectedIndex == 1)
                {
                    CollectPracticeTimesPerMusicPiece();
                    DrawMusicPiecesChart();
                }
                else if (TabCharts.SelectedIndex == 2)
                {
                    CollectPracticeTimesPerMusicPieceMonth();
                    DrawMusicPiecesMonthChart();
                }
            }
            catch (Exception ex)
            {
                MLLogManager.Instance.LogError("Error when changing period filter in statistics window.", ex);
                MessageBox.Show($"Error when changing period: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private string FormatTime(double minutes)
        {
            int totalSeconds = (int)Math.Round(minutes * 60);
            int hours = totalSeconds / 3600;
            int mins = (totalSeconds % 3600) / 60;
            int secs = totalSeconds % 60;

            return $"{hours:00}:{mins:00}:{secs:00}";
        }

        private void InitializeRefreshTimer()
        {
            _refreshTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(500),
                IsEnabled = false
            };
            _refreshTimer.Tick += (s, e) =>
            {
                _refreshTimer.Stop();
                CollectPracticeTimes();
                UpdateStatistics();

                if (TabCharts.SelectedIndex == 0)
                    DrawChart();
                else if (TabCharts.SelectedIndex == 1)
                {
                    CollectPracticeTimesPerMusicPiece();
                    DrawMusicPiecesChart();
                }
                else if (TabCharts.SelectedIndex == 2)
                {
                    CollectPracticeTimesPerMusicPieceMonth();
                    DrawMusicPiecesMonthChart();
                }
            };
        }

        private void DrawAxis(Canvas canvas, bool isHorizontal, double startX, double startY, double length, int tickCount, double maxValue)
        {
            var axisLine = new Line { Stroke = Brushes.LightGray, StrokeThickness = 1 };
            if (isHorizontal)
            {
                axisLine.X1 = startX;
                axisLine.Y1 = startY;
                axisLine.X2 = startX + length;
                axisLine.Y2 = startY;
            }
            else // Vertical
            {
                axisLine.X1 = startX;
                axisLine.Y1 = startY;
                axisLine.X2 = startX;
                axisLine.Y2 = startY - length;
            }
            canvas.Children.Add(axisLine);

            if (!isHorizontal && maxValue > 0)
            {
                for (int i = 0; i <= tickCount; i++)
                {
                    double position = i * (length / tickCount);
                    double value = i * (maxValue / tickCount);
                    var tickMark = new Line { X1 = startX - 5, Y1 = startY - position, X2 = startX, Y2 = startY - position, Stroke = Brushes.LightGray, StrokeThickness = 1 };
                    canvas.Children.Add(tickMark);

                    var label = new TextBlock { Text = FormatTime(value), FontSize = 10, Foreground = Brushes.DimGray };
                    Canvas.SetLeft(label, startX - 55);
                    Canvas.SetTop(label, startY - position - 7);
                    canvas.Children.Add(label);
                }
            }
        }

        private void DrawChart()
        {
            if (CnvPracticeTimeChart == null || timePerCategory == null || CnvPracticeTimeChart.ActualWidth <= 0 || CnvPracticeTimeChart.ActualHeight <= 0)
                return;

            CnvPracticeTimeChart.Children.Clear();

            if (!timePerCategory.Any() || timePerCategory.All(t => t.Minutes == 0))
            {
                var noDataText = new TextBlock { Text = "No practice time available for the selected period.", FontSize = 14, Foreground = Brushes.Gray };
                Canvas.SetLeft(noDataText, 20);
                Canvas.SetTop(noDataText, 20);
                CnvPracticeTimeChart.Children.Add(noDataText);
                return;
            }

            double maxMinutes = timePerCategory.Max(t => t.Minutes);
            if (maxMinutes == 0) maxMinutes = 1; // Avoid division by zero

            double marginLeft = 60;
            double marginRight = 20;
            double marginBottom = 40;
            double marginTop = 20;

            double chartWidth = CnvPracticeTimeChart.ActualWidth - marginLeft - marginRight;
            double chartHeight = CnvPracticeTimeChart.ActualHeight - marginTop - marginBottom;

            DrawAxis(CnvPracticeTimeChart, false, marginLeft, CnvPracticeTimeChart.ActualHeight - marginBottom, chartHeight, 5, maxMinutes);
            DrawAxis(CnvPracticeTimeChart, true, marginLeft, CnvPracticeTimeChart.ActualHeight - marginBottom, chartWidth, 0, 0);

            double barWidth = chartWidth / timePerCategory.Count * 0.7;
            double barSpacing = chartWidth / timePerCategory.Count * 0.3;

            for (int i = 0; i < timePerCategory.Count; i++)
            {
                var item = timePerCategory[i];
                double barHeight = (item.Minutes / maxMinutes) * chartHeight;
                double x = marginLeft + (i * (barWidth + barSpacing)) + (barSpacing / 2);
                double y = CnvPracticeTimeChart.ActualHeight - marginBottom - barHeight;

                DrawBar(CnvPracticeTimeChart, x, y, barWidth, barHeight, Brushes.SteelBlue, item.Label, FormatTime(item.Minutes), marginBottom);
            }
        }

        private void DrawBar(Canvas canvas, double x, double y, double width, double height, Brush color, string label, string valueLabel, double marginBottom)
        {
            var bar = new Rectangle { Width = width, Height = height, Fill = color, RadiusX = 3, RadiusY = 3 };
            Canvas.SetLeft(bar, x);
            Canvas.SetTop(bar, y);
            canvas.Children.Add(bar);

            var valueText = new TextBlock { Text = valueLabel, FontSize = 10, FontWeight = FontWeights.Bold, Foreground = Brushes.DimGray };
            valueText.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            Canvas.SetLeft(valueText, x + (width / 2) - (valueText.DesiredSize.Width / 2));
            Canvas.SetTop(valueText, y - 15);
            canvas.Children.Add(valueText);

            var labelText = new TextBlock { Text = label, FontSize = 11, TextAlignment = TextAlignment.Center, Width = width + 20, TextTrimming = TextTrimming.CharacterEllipsis };
            Canvas.SetLeft(labelText, x - 10);
            Canvas.SetTop(labelText, canvas.ActualHeight - marginBottom + 5);
            canvas.Children.Add(labelText);
        }

        private void DrawBarWithName(Canvas canvas, double x, double y, double width, double height, Brush color, string musicPieceName, string valueLabel, double marginBottom)
        {
            var bar = new Rectangle { Width = width, Height = height, Fill = color, RadiusX = 3, RadiusY = 3 };
            Canvas.SetLeft(bar, x);
            Canvas.SetTop(bar, y);
            canvas.Children.Add(bar);

            var valueText = new TextBlock { Text = valueLabel, FontSize = 10, FontWeight = FontWeights.Bold, Foreground = Brushes.DimGray };
            valueText.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            Canvas.SetLeft(valueText, x + width / 2 - valueText.DesiredSize.Width / 2);
            Canvas.SetTop(valueText, y - 15);
            canvas.Children.Add(valueText);

            var nameText = new TextBlock
            {
                Text = musicPieceName,
                FontSize = 11,
                TextWrapping = TextWrapping.NoWrap,
                TextTrimming = TextTrimming.CharacterEllipsis,
                MaxWidth = 100,
                RenderTransform = new RotateTransform(-45),
                RenderTransformOrigin = new Point(0, 0)
            };
            Canvas.SetLeft(nameText, x);
            Canvas.SetTop(nameText, canvas.ActualHeight - marginBottom + 10);
            canvas.Children.Add(nameText);
        }

        private void UpdateStatistics()
        {
            DateTime today = DateHelper.LocalToday();
            var firstDayOfWeek = CultureInfo.CurrentCulture.DateTimeFormat.FirstDayOfWeek;
            var weekStart = today.AddDays(-(int)today.DayOfWeek + (int)firstDayOfWeek);

            if (weekStart > today)
            {
                weekStart = weekStart.AddDays(-7);
            }

            var monthStart = new DateTime(today.Year, today.Month, 1);
            var yearStart = new DateTime(today.Year, 1, 1);

            var allSessions = PracticeHistoryManager.Instance.GetAllHistory();

            // Use Brussels local day boundary and only use Duration (session timer)
            double todayMin = allSessions
                .Where(s => DateOnly.FromDateTime(DateHelper.ToLocalBrussels(s.Date)) == DateOnly.FromDateTime(today))
                .Sum(s => s.Duration.TotalMinutes);

            double weekMin = allSessions
                .Where(s =>
                {
                    var d = DateHelper.ToLocalBrussels(s.Date).Date;
                    return d >= weekStart.Date && d <= today.Date;
                })
                .Sum(s => s.Duration.TotalMinutes);

            double monthMin = allSessions
                .Where(s =>
                {
                    var d = DateHelper.ToLocalBrussels(s.Date);
                    return d.Year == today.Year && d.Month == today.Month;
                })
                .Sum(s => s.Duration.TotalMinutes);

            double yearMin = allSessions
                .Where(s => DateHelper.ToLocalBrussels(s.Date).Year == today.Year)
                .Sum(s => s.Duration.TotalMinutes);

            double totalMin = allSessions
                .Sum(s => s.Duration.TotalMinutes);

            TxtTodayPracticeTime.SetCurrentValue(TextBlock.TextProperty, FormatTime(todayMin));
            TxtWeekPracticeTime.SetCurrentValue(TextBlock.TextProperty, FormatTime(weekMin));
            TxtMonthPracticeTime.SetCurrentValue(TextBlock.TextProperty, FormatTime(monthMin));
            TxtYearPracticeTime.SetCurrentValue(TextBlock.TextProperty, FormatTime(yearMin));
            TxtTotalPracticeTime.SetCurrentValue(TextBlock.TextProperty, FormatTime(totalMin));
        }

        private void CollectPracticeTimes()
        {
            practiceTimePerDay = new List<(DateTime Date, double Minutes)>();
            timePerCategory = new List<(string Label, double Minutes)>();

            if (CmbPeriodFilter.SelectedItem is not ComboBoxItem selectedItem) return;

            string period = selectedItem.Content?.ToString() ?? string.Empty;
            DateTime today = DateHelper.LocalToday();
            var firstDayOfWeek = CultureInfo.CurrentCulture.DateTimeFormat.FirstDayOfWeek;

            var allSessions = PracticeHistoryManager.Instance.GetAllHistory().ToList();

            if (!allSessions.Any()) return;

            switch (period)
            {
                case "Current week":
                case "Previous week":
                    DateTime weekStart;
                    if (period == "Current week")
                    {
                        weekStart = today.AddDays(-(int)today.DayOfWeek + (int)firstDayOfWeek);
                        if (weekStart > today) weekStart = weekStart.AddDays(-7);
                    }
                    else // Previous week
                    {
                        var thisWeekStart = today.AddDays(-(int)today.DayOfWeek + (int)firstDayOfWeek);
                        if (thisWeekStart > today) thisWeekStart = thisWeekStart.AddDays(-7);
                        weekStart = thisWeekStart.AddDays(-7);
                    }
                    var weekEnd = weekStart.AddDays(6);

                    var weekData = allSessions
                        .Select(x => new { LocalDate = DateHelper.ToLocalBrussels(x.Date).Date, Minutes = x.Duration.TotalMinutes })
                        .Where(x => x.LocalDate >= weekStart.Date && x.LocalDate <= weekEnd.Date)
                        .GroupBy(x => x.LocalDate)
                        .ToDictionary(g => g.Key, g => g.Sum(s => s.Minutes));

                    practiceTimePerDay = Enumerable.Range(0, 7)
                        .Select(i => weekStart.AddDays(i))
                        .Select(d => (Date: d, Minutes: weekData.ContainsKey(d) ? weekData[d] : 0.0))
                        .ToList();

                    timePerCategory = practiceTimePerDay
                        .Select(x => (Label: x.Date.ToString("ddd d", CultureInfo.CurrentCulture), Minutes: x.Minutes))
                        .ToList();
                    break;

                case "Current month":
                case "Previous month":
                    DateTime monthStart;
                    int daysInMonth;
                    if (period == "Current month")
                    {
                        monthStart = new DateTime(today.Year, today.Month, 1);
                        daysInMonth = DateTime.DaysInMonth(today.Year, today.Month);
                    }
                    else // Previous month
                    {
                        var prevMonthDate = today.AddMonths(-1);
                        monthStart = new DateTime(prevMonthDate.Year, prevMonthDate.Month, 1);
                        daysInMonth = DateTime.DaysInMonth(prevMonthDate.Year, prevMonthDate.Month);
                    }

                    var monthData = allSessions
                        .Select(x => new { Local = DateHelper.ToLocalBrussels(x.Date), Minutes = x.Duration.TotalMinutes })
                        .Where(x => x.Local.Year == monthStart.Year && x.Local.Month == monthStart.Month)
                        .GroupBy(x => x.Local.Day)
                        .ToDictionary(g => g.Key, g => g.Sum(s => s.Minutes));

                    practiceTimePerDay = Enumerable.Range(1, daysInMonth)
                        .Select(day => (Date: new DateTime(monthStart.Year, monthStart.Month, day),
                                        Minutes: monthData.ContainsKey(day) ? monthData[day] : 0.0))
                        .ToList();

                    timePerCategory = practiceTimePerDay
                        .Select(x => (Label: x.Date.Day.ToString(), Minutes: x.Minutes))
                        .ToList();
                    break;

                case "Current year":
                    var yearSessions = allSessions.Where(x => DateHelper.ToLocalBrussels(x.Date).Year == today.Year).ToList();

                    practiceTimePerDay = yearSessions
                        .GroupBy(x => DateHelper.ToLocalBrussels(x.Date).Date)
                        .OrderBy(g => g.Key)
                        .Select(g => (Date: g.Key, Minutes: g.Sum(x => x.Duration.TotalMinutes)))
                        .ToList();

                    timePerCategory = Enumerable.Range(1, 12)
                        .Select(m => (Label: CultureInfo.CurrentCulture.DateTimeFormat.GetAbbreviatedMonthName(m),
                                      Minutes: yearSessions.Where(s => DateHelper.ToLocalBrussels(s.Date).Month == m).Sum(s => s.Duration.TotalMinutes)))
                        .ToList();
                    break;

                case "All years":
                    if (!allSessions.Any()) break;

                    practiceTimePerDay = allSessions
                        .GroupBy(x => DateHelper.ToLocalBrussels(x.Date).Date)
                        .OrderBy(g => g.Key)
                        .Select(g => (Date: g.Key, Minutes: g.Sum(x => x.Duration.TotalMinutes)))
                        .ToList();

                    timePerCategory = allSessions
                        .GroupBy(x => DateHelper.ToLocalBrussels(x.Date).Year)
                        .OrderBy(g => g.Key)
                        .Select(g => (Label: g.Key.ToString(), Minutes: g.Sum(s => s.Duration.TotalMinutes)))
                        .ToList();
                    break;
            }
        }

        private void CollectPracticeTimesPerMusicPiece()
        {
            topMusicPieces = new List<(string MusicPiece, double Minutes)>();

            DateTime today = DateHelper.LocalToday();
            var firstDayOfWeek = CultureInfo.CurrentCulture.DateTimeFormat.FirstDayOfWeek;
            var weekStart = today.AddDays(-(int)today.DayOfWeek + (int)firstDayOfWeek);

            if (weekStart > today)
            {
                weekStart = weekStart.AddDays(-7);
            }
            var weekEnd = weekStart.AddDays(6);

            var allSessions = PracticeHistoryManager.Instance.GetAllHistory();
            var musicPieceStats = allSessions
                .Where(s =>
                {
                    var d = DateHelper.ToLocalBrussels(s.Date).Date;
                    return d >= weekStart.Date && d <= weekEnd.Date;
                })
                .GroupBy(s => s.MusicPieceId)
                .Select(g => new
                {
                    MusicPieceId = g.Key,
                    Title = g.OrderByDescending(s => s.Date).First().MusicPieceTitle,
                    TotalMinutes = g.Sum(s => s.Duration.TotalMinutes)
                })
                .ToDictionary(g => g.MusicPieceId, g => (g.Title, g.TotalMinutes));

            topMusicPieces = musicPieceStats
                .Select(kvp => (MusicPiece: kvp.Value.Title, Minutes: kvp.Value.TotalMinutes))
                .Where(item => item.Minutes > 0)
                .OrderByDescending(item => item.Minutes)
                .ToList();
        }

        private void CollectPracticeTimesPerMusicPieceMonth()
        {
            topMusicPiecesMonth = new List<(string MusicPiece, double Minutes)>();

            DateTime today = DateHelper.LocalToday();
            var monthStart = new DateTime(today.Year, today.Month, 1);
            var monthEnd = monthStart.AddMonths(1).AddDays(-1);

            var allSessions = PracticeHistoryManager.Instance.GetAllHistory();
            var musicPieceStats = allSessions
                .Where(s =>
                {
                    var d = DateHelper.ToLocalBrussels(s.Date).Date;
                    return d >= monthStart && d <= monthEnd;
                })
                .GroupBy(s => s.MusicPieceId)
                .Select(g => new
                {
                    MusicPieceId = g.Key,
                    Title = g.OrderByDescending(s => s.Date).First().MusicPieceTitle,
                    TotalMinutes = g.Sum(s => s.Duration.TotalMinutes)
                })
                .ToDictionary(g => g.MusicPieceId, g => (g.Title, g.TotalMinutes));

            topMusicPiecesMonth = musicPieceStats
                .Select(kvp => (MusicPiece: kvp.Value.Title, Minutes: kvp.Value.TotalMinutes))
                .Where(item => item.Minutes > 0)
                .OrderByDescending(item => item.Minutes)
                .ToList();
        }

        private void DrawMusicPiecesChart()
        {
            if (CnvDaysChart == null || topMusicPieces == null || CnvDaysChart.ActualWidth <= 0 || CnvDaysChart.ActualHeight <= 0)
                return;

            CnvDaysChart.Children.Clear();

            if (!topMusicPieces.Any())
            {
                var noDataText = new TextBlock { Text = "No practice sessions for music pieces this week.", FontSize = 14, Foreground = Brushes.Gray };
                Canvas.SetLeft(noDataText, 20);
                Canvas.SetTop(noDataText, 20);
                CnvDaysChart.Children.Add(noDataText);
                return;
            }

            double maxMinutes = topMusicPieces.Max(d => d.Minutes);
            if (maxMinutes == 0) maxMinutes = 1; // Avoid division by zero
            int maxItemsToShow = Math.Min(topMusicPieces.Count, 10);

            double marginLeft = 60;
            double marginRight = 20;
            double marginBottom = 80;
            double marginTop = 20;

            double chartWidth = CnvDaysChart.ActualWidth - marginLeft - marginRight;
            double chartHeight = CnvDaysChart.ActualHeight - marginTop - marginBottom;

            DrawAxis(CnvDaysChart, false, marginLeft, CnvDaysChart.ActualHeight - marginBottom, chartHeight, 5, maxMinutes);
            DrawAxis(CnvDaysChart, true, marginLeft, CnvDaysChart.ActualHeight - marginBottom, chartWidth, 0, 0);

            double barWidth = chartWidth / maxItemsToShow * 0.7;
            double barSpacing = chartWidth / maxItemsToShow * 0.3;

            Color[] pastelColors = new Color[]
            {
                Color.FromRgb(255, 179, 186), Color.FromRgb(255, 223, 186),
                Color.FromRgb(255, 255, 186), Color.FromRgb(186, 255, 201),
                Color.FromRgb(186, 225, 255), Color.FromRgb(186, 186, 255),
                Color.FromRgb(255, 186, 255), Color.FromRgb(229, 204, 255),
                Color.FromRgb(204, 255, 229), Color.FromRgb(255, 204, 204),
            };

            for (int i = 0; i < maxItemsToShow; i++)
            {
                var item = topMusicPieces[i];
                double barHeight = (item.Minutes / maxMinutes) * chartHeight;
                double x = marginLeft + (i * (barWidth + barSpacing)) + (barSpacing / 2);
                double y = CnvDaysChart.ActualHeight - marginBottom - barHeight;

                var barBrush = new SolidColorBrush(pastelColors[i % pastelColors.Length]);
                DrawBarWithName(CnvDaysChart, x, y, barWidth, barHeight, barBrush, item.MusicPiece, FormatTime(item.Minutes), marginBottom);
            }
        }

        private void DrawMusicPiecesMonthChart()
        {
            if (CnvMonthPiecesChart == null || topMusicPiecesMonth == null || CnvMonthPiecesChart.ActualWidth <= 0 || CnvMonthPiecesChart.ActualHeight <= 0)
                return;

            CnvMonthPiecesChart.Children.Clear();

            if (!topMusicPiecesMonth.Any())
            {
                var noDataText = new TextBlock { Text = "No practice sessions for music pieces this month.", FontSize = 14, Foreground = Brushes.Gray };
                Canvas.SetLeft(noDataText, 20);
                Canvas.SetTop(noDataText, 20);
                CnvMonthPiecesChart.Children.Add(noDataText);
                return;
            }

            double maxMinutes = topMusicPiecesMonth.Max(d => d.Minutes);
            if (maxMinutes == 0) maxMinutes = 1; // Avoid division by zero
            int maxItemsToShow = Math.Min(topMusicPiecesMonth.Count, 10);

            double marginLeft = 60;
            double marginRight = 20;
            double marginBottom = 80;
            double marginTop = 20;

            double chartWidth = CnvMonthPiecesChart.ActualWidth - marginLeft - marginRight;
            double chartHeight = CnvMonthPiecesChart.ActualHeight - marginTop - marginBottom;

            DrawAxis(CnvMonthPiecesChart, false, marginLeft, CnvMonthPiecesChart.ActualHeight - marginBottom, chartHeight, 5, maxMinutes);
            DrawAxis(CnvMonthPiecesChart, true, marginLeft, CnvMonthPiecesChart.ActualHeight - marginBottom, chartWidth, 0, 0);

            double barWidth = chartWidth / maxItemsToShow * 0.7;
            double barSpacing = chartWidth / maxItemsToShow * 0.3;

            Color[] pastelColors = new Color[]
            {
                Color.FromRgb(255, 179, 186), Color.FromRgb(255, 223, 186),
                Color.FromRgb(255, 255, 186), Color.FromRgb(186, 255, 201),
                Color.FromRgb(186, 225, 255), Color.FromRgb(186, 186, 255),
                Color.FromRgb(255, 186, 255), Color.FromRgb(229, 204, 255),
                Color.FromRgb(204, 255, 229), Color.FromRgb(255, 204, 204),
            };

            for (int i = 0; i < maxItemsToShow; i++)
            {
                var item = topMusicPiecesMonth[i];
                double barHeight = (item.Minutes / maxMinutes) * chartHeight;
                double x = marginLeft + (i * (barWidth + barSpacing)) + (barSpacing / 2);
                double y = CnvMonthPiecesChart.ActualHeight - marginBottom - barHeight;

                var barBrush = new SolidColorBrush(pastelColors[i % pastelColors.Length]);
                DrawBarWithName(CnvMonthPiecesChart, x, y, barWidth, barHeight, barBrush, item.MusicPiece, FormatTime(item.Minutes), marginBottom);
            }
        }

        private void TabCharts_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.Source is not TabControl) return;

            if (TabCharts.SelectedIndex == 0)
            {
                DrawChart();
            }
            else if (TabCharts.SelectedIndex == 1)
            {
                CollectPracticeTimesPerMusicPiece();
                DrawMusicPiecesChart();
            }
            else if (TabCharts.SelectedIndex == 2)
            {
                CollectPracticeTimesPerMusicPieceMonth();
                DrawMusicPiecesMonthChart();
            }
        }
    }
}
