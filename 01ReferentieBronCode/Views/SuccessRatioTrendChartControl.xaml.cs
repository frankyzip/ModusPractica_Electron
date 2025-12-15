using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ModusPractica.Views
{
    public partial class SuccessRatioTrendChartControl : UserControl
    {
        public SuccessRatioTrendChartControl()
        {
            InitializeComponent();
            Loaded += (_, __) => Redraw();
            SizeChanged += (_, __) => Redraw();

            // Auto-refresh on history changes if enabled
            PracticeHistoryManager.Instance.HistoryChanged += (_, __) =>
            {
                if (AutoRefreshOnHistoryChanged)
                    Redraw();
            };
        }

        #region Dependency Properties

        public static readonly DependencyProperty BarSectionProperty = DependencyProperty.Register(
            nameof(BarSection), typeof(BarSection), typeof(SuccessRatioTrendChartControl),
            new PropertyMetadata(null, OnInputChanged));

        public BarSection? BarSection
        {
            get => (BarSection?)GetValue(BarSectionProperty);
            set => SetValue(BarSectionProperty, value);
        }

        public static readonly DependencyProperty MaxSessionsProperty = DependencyProperty.Register(
            nameof(MaxSessions), typeof(int), typeof(SuccessRatioTrendChartControl),
            new PropertyMetadata(7, OnInputChanged));

        public int MaxSessions
        {
            get => (int)GetValue(MaxSessionsProperty);
            set => SetValue(MaxSessionsProperty, Math.Clamp(value, 3, 10));
        }

        public static readonly DependencyProperty IncludeDeletedProperty = DependencyProperty.Register(
            nameof(IncludeDeleted), typeof(bool), typeof(SuccessRatioTrendChartControl),
            new PropertyMetadata(false, OnInputChanged));

        public bool IncludeDeleted
        {
            get => (bool)GetValue(IncludeDeletedProperty);
            set => SetValue(IncludeDeletedProperty, value);
        }

        public static readonly DependencyProperty HighlightLatestProperty = DependencyProperty.Register(
            nameof(HighlightLatest), typeof(bool), typeof(SuccessRatioTrendChartControl),
            new PropertyMetadata(true, OnInputChanged));

        public bool HighlightLatest
        {
            get => (bool)GetValue(HighlightLatestProperty);
            set => SetValue(HighlightLatestProperty, value);
        }

        public static readonly DependencyProperty AutoRefreshOnHistoryChangedProperty = DependencyProperty.Register(
            nameof(AutoRefreshOnHistoryChanged), typeof(bool), typeof(SuccessRatioTrendChartControl),
            new PropertyMetadata(true));

        public bool AutoRefreshOnHistoryChanged
        {
            get => (bool)GetValue(AutoRefreshOnHistoryChangedProperty);
            set => SetValue(AutoRefreshOnHistoryChangedProperty, value);
        }

        // NEW: Tempo overlay toggle
        public static readonly DependencyProperty ShowTempoOverlayProperty = DependencyProperty.Register(
            nameof(ShowTempoOverlay), typeof(bool), typeof(SuccessRatioTrendChartControl),
            new PropertyMetadata(false, OnInputChanged));

        public bool ShowTempoOverlay
        {
            get => (bool)GetValue(ShowTempoOverlayProperty);
            set => SetValue(ShowTempoOverlayProperty, value);
        }

        private static void OnInputChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is SuccessRatioTrendChartControl ctrl)
            {
                ctrl.Redraw();
            }
        }

        #endregion

        private void Redraw()
        {
            ChartCanvas.Children.Clear();

            if (BarSection == null)
            {
                ShowEmptyState(true);
                return;
            }

            var history = PracticeHistoryManager.Instance.GetHistoryForBarSection(BarSection.Id).ToList();
            if (!IncludeDeleted)
                history = history.Where(h => !h.IsDeleted).ToList();

            if (history.Count < 2)
            {
                ShowEmptyState(true);
                return;
            }

            history = history.OrderBy(h => h.Date).ToList();

            int take = Math.Min(MaxSessions, history.Count);
            int startIndex = history.Count - take;

            var data = new List<(DateTime Date, double Ratio)>();
            for (int i = startIndex; i < history.Count; i++)
            {
                int totalReps = 0;
                int totalFails = 0;
                for (int j = startIndex; j <= i; j++)
                {
                    totalReps += history[j].Repetitions;
                    totalFails += history[j].TotalFailures;
                }

                double ratio = 0.0;
                if (totalReps + totalFails > 0)
                    ratio = (double)totalReps / (totalReps + totalFails);

                data.Add((history[i].Date, ratio));
            }

            if (data.Count < 2)
            {
                ShowEmptyState(true);
                return;
            }

            ShowEmptyState(false);
            DrawZonesAndAxes();
            DrawSeries(data);
            if (ShowTempoOverlay)
            {
                DrawTempoOverlay();
            }
            DrawLegend();
        }

        private void ShowEmptyState(bool show)
        {
            EmptyStateText.SetCurrentValue(VisibilityProperty, show ? Visibility.Visible : Visibility.Collapsed);
        }

        private double RatioToScaledY(double ratio)
        {
            if (ratio <= 0.60)
                return (ratio / 0.60) * 0.20; // 0-60% -> 0-20%
            return 0.20 + ((ratio - 0.60) / 0.40) * 0.80; // 60-100% -> 20-100%
        }

        private void DrawZonesAndAxes()
        {
            double width = ChartCanvas.ActualWidth;
            double height = ChartCanvas.ActualHeight;
            if (width == 0) width = 350;
            if (height == 0) height = 650; // fallback to original default

            double chartLeft = 30;
            double chartTop = 10;
            double chartWidth = width - 40;
            double chartHeight = height - 60;

            void DrawZone(double minRatio, double maxRatio, string colorHex, string zoneName)
            {
                double scaledYMax = RatioToScaledY(maxRatio);
                double scaledYMin = RatioToScaledY(minRatio);
                double yTop = chartTop + chartHeight - (scaledYMax * chartHeight);
                double yBottom = chartTop + chartHeight - (scaledYMin * chartHeight);
                double zoneHeight = yBottom - yTop;

                var zone = new System.Windows.Shapes.Rectangle
                {
                    Width = chartWidth,
                    Height = zoneHeight,
                    Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString(colorHex)),
                    Opacity = 0.4
                };
                Canvas.SetLeft(zone, chartLeft);
                Canvas.SetTop(zone, yTop);
                ChartCanvas.Children.Add(zone);

                // Draw zone name centered horizontally, slightly above the middle of the band
                var nameText = new TextBlock
                {
                    Text = zoneName,
                    FontSize = 11,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = new SolidColorBrush(Color.FromRgb(95, 95, 95))
                };
                double textX = chartLeft + (chartWidth / 2) - 30; // approximate centering
                double textY = yTop + (zoneHeight / 2) - 8;
                Canvas.SetLeft(nameText, textX);
                Canvas.SetTop(nameText, textY);
                ChartCanvas.Children.Add(nameText);
            }

            // Zones (keep in sync with SessionReport)
            DrawZone(0.95, 1.0, "#D0D0D0", "Overlearning");
            DrawZone(0.90, 0.95, "#C8E6C9", "Mastery");
            DrawZone(0.80, 0.90, "#FFE0B2", "Consolidation (Ideal)");
            DrawZone(0.60, 0.80, "#FFF59D", "Exploration");
            DrawZone(0.00, 0.60, "#FFCDD2", "Too Hard");

            void DrawBoundary(double ratio)
            {
                double scaledY = RatioToScaledY(ratio);
                double y = chartTop + chartHeight - (scaledY * chartHeight);
                var line = new System.Windows.Shapes.Line
                {
                    X1 = chartLeft,
                    Y1 = y,
                    X2 = chartLeft + chartWidth,
                    Y2 = y,
                    Stroke = new SolidColorBrush(Color.FromRgb(150, 150, 150)),
                    StrokeThickness = 1,
                    StrokeDashArray = new DoubleCollection { 2, 2 }
                };
                ChartCanvas.Children.Add(line);
            }

            DrawBoundary(0.60);
            DrawBoundary(0.80);
            DrawBoundary(0.90);
            DrawBoundary(0.95);

            // X-axis
            var axis = new System.Windows.Shapes.Line
            {
                X1 = chartLeft,
                Y1 = chartTop + chartHeight,
                X2 = chartLeft + chartWidth,
                Y2 = chartTop + chartHeight,
                Stroke = new SolidColorBrush(Color.FromRgb(150, 150, 150)),
                StrokeThickness = 1.5
            };
            ChartCanvas.Children.Add(axis);

            // Y markers: 0%, 60%, 100%
            void Marker(double ratio, string label)
            {
                double scaledY = RatioToScaledY(ratio);
                double y = chartTop + chartHeight - (scaledY * chartHeight);
                var m = new System.Windows.Shapes.Line
                {
                    X1 = chartLeft - 5,
                    Y1 = y,
                    X2 = chartLeft,
                    Y2 = y,
                    Stroke = new SolidColorBrush(Color.FromRgb(150, 150, 150)),
                    StrokeThickness = 1
                };
                ChartCanvas.Children.Add(m);
                var t = new TextBlock
                {
                    Text = label,
                    FontSize = 10,
                    Foreground = new SolidColorBrush(Color.FromRgb(100, 100, 100))
                };
                Canvas.SetLeft(t, chartLeft - 25);
                Canvas.SetTop(t, y - 7);
                ChartCanvas.Children.Add(t);
            }
            Marker(0.0, "0%");
            Marker(0.60, "60%");
            Marker(0.80, "80%");
            Marker(0.90, "90%");
            Marker(0.95, "95%");
            Marker(1.0, "100%");
        }

        private void DrawSeries(List<(DateTime Date, double Ratio)> data)
        {
            double width = ChartCanvas.ActualWidth;
            double height = ChartCanvas.ActualHeight;
            if (width == 0) width = 350;
            if (height == 0) height = 650; // fallback to original default

            double chartLeft = 30;
            double chartTop = 10;
            double chartWidth = width - 40;
            double chartHeight = height - 60;

            double xStep = chartWidth / (data.Count - 1);
            var points = new List<Point>();
            for (int i = 0; i < data.Count; i++)
            {
                double x = chartLeft + (i * xStep);
                double scaledY = RatioToScaledY(data[i].Ratio);
                double y = chartTop + chartHeight - (scaledY * chartHeight);
                points.Add(new Point(x, y));

                var circle = new System.Windows.Shapes.Ellipse
                {
                    Width = 6,
                    Height = 6,
                    Fill = new SolidColorBrush(Color.FromRgb(74, 134, 232)),
                    Stroke = new SolidColorBrush(Colors.White),
                    StrokeThickness = 2
                };
                Canvas.SetLeft(circle, x - 3);
                Canvas.SetTop(circle, y - 3);
                ChartCanvas.Children.Add(circle);

                if (HighlightLatest && i == data.Count - 1)
                {
                    circle.Width = 10;
                    circle.Height = 10;
                    circle.StrokeThickness = 3;
                    Canvas.SetLeft(circle, x - 5);
                    Canvas.SetTop(circle, y - 5);
                }
            }

            for (int i = 0; i < points.Count - 1; i++)
            {
                var line = new System.Windows.Shapes.Line
                {
                    X1 = points[i].X,
                    Y1 = points[i].Y,
                    X2 = points[i + 1].X,
                    Y2 = points[i + 1].Y,
                    Stroke = new SolidColorBrush(Color.FromRgb(74, 134, 232)),
                    StrokeThickness = 2.5
                };
                ChartCanvas.Children.Add(line);
            }
        }

        private void DrawLegend()
        {
            double width = ChartCanvas.ActualWidth;
            double height = ChartCanvas.ActualHeight;
            if (width == 0) width = 350;
            if (height == 0) height = 650; // fallback to original default

            double chartLeft = 30;
            double chartWidth = width - 40;
            double legendTop = 10 + (height - 60) + 10;

            var zones = new[]
            {
                new { Name = "Too Hard", Range = "0-60%", Color = "#FFCDD2" },
                new { Name = "Exploration", Range = "60-80%", Color = "#FFF59D" },
                new { Name = "Consolidation (Ideal)", Range = "80-90%", Color = "#FFE0B2" },
                new { Name = "Mastery", Range = "90-95%", Color = "#C8E6C9" },
                new { Name = "Overlearning", Range = "95-100%", Color = "#D0D0D0" }
            };

            double itemWidth = chartWidth / zones.Length;
            double boxSize = 10;
            double x = chartLeft;

            foreach (var zone in zones)
            {
                var color = (Color)ColorConverter.ConvertFromString(zone.Color);
                var box = new System.Windows.Shapes.Rectangle
                {
                    Width = boxSize,
                    Height = boxSize,
                    Fill = new SolidColorBrush(color),
                    Stroke = new SolidColorBrush(Color.FromRgb(100, 100, 100)),
                    StrokeThickness = 0.5
                };
                Canvas.SetLeft(box, x);
                Canvas.SetTop(box, legendTop);
                ChartCanvas.Children.Add(box);

                var label = new TextBlock
                {
                    Text = $"{zone.Name}\n({zone.Range})",
                    FontSize = 8,
                    Foreground = new SolidColorBrush(Color.FromRgb(80, 80, 80)),
                    TextAlignment = TextAlignment.Left
                };
                Canvas.SetLeft(label, x + boxSize + 3);
                Canvas.SetTop(label, legendTop - 2);
                ChartCanvas.Children.Add(label);

                x += itemWidth;
            }

            // Add tempo overlay legend item if enabled
            if (ShowTempoOverlay)
            {
                double legendX = chartLeft + chartWidth - 140; // right side area
                var line = new System.Windows.Shapes.Line
                {
                    X1 = legendX,
                    Y1 = legendTop + 5,
                    X2 = legendX + 20,
                    Y2 = legendTop + 5,
                    Stroke = new SolidColorBrush(Color.FromRgb(128, 0, 128)), // purple
                    StrokeThickness = 2
                };
                ChartCanvas.Children.Add(line);
                var tempoLbl = new TextBlock
                {
                    Text = "Tempo (% of target)",
                    FontSize = 9,
                    Foreground = new SolidColorBrush(Color.FromRgb(80, 80, 80))
                };
                Canvas.SetLeft(tempoLbl, legendX + 24);
                Canvas.SetTop(tempoLbl, legendTop - 2);
                ChartCanvas.Children.Add(tempoLbl);
            }
        }

        private void DrawTempoOverlay()
        {
            if (BarSection == null) return;

            var history = PracticeHistoryManager.Instance.GetHistoryForBarSection(BarSection.Id).ToList();
            if (!IncludeDeleted)
                history = history.Where(h => !h.IsDeleted).ToList();
            if (history.Count < 2) return;

            history = history.OrderBy(h => h.Date).ToList();

            int take = Math.Min(MaxSessions, history.Count);
            int startIndex = history.Count - take;

            var data = new List<(DateTime Date, double TempoRatio)>();
            for (int i = startIndex; i < history.Count; i++)
            {
                var h = history[i];
                double tempoRatio = 0.0;
                if (h.TargetTempo > 0 && h.AchievedTempo > 0)
                {
                    tempoRatio = Math.Min(1.0, (double)h.AchievedTempo / h.TargetTempo);
                }
                else
                {
                    // If no tempo data, we leave it at 0; skip points entirely if all zeros later
                }
                data.Add((h.Date, tempoRatio));
            }

            // If all ratios are zero (no tempo data), do not draw
            if (data.All(d => d.TempoRatio <= 0.0)) return;

            double width = ChartCanvas.ActualWidth;
            double height = ChartCanvas.ActualHeight;
            if (width == 0) width = 350;
            if (height == 0) height = 650; // fallback to original default

            double chartLeft = 30;
            double chartTop = 10;
            double chartWidth = width - 40;
            double chartHeight = height - 60;

            double xStep = chartWidth / (data.Count - 1);
            Point? prev = null;
            for (int i = 0; i < data.Count; i++)
            {
                double x = chartLeft + (i * xStep);
                double scaledY = RatioToScaledY(data[i].TempoRatio);
                double y = chartTop + chartHeight - (scaledY * chartHeight);
                var p = new Point(x, y);
                if (prev != null)
                {
                    var line = new System.Windows.Shapes.Line
                    {
                        X1 = prev.Value.X,
                        Y1 = prev.Value.Y,
                        X2 = p.X,
                        Y2 = p.Y,
                        Stroke = new SolidColorBrush(Color.FromRgb(128, 0, 128)), // purple
                        StrokeThickness = 2,
                        StrokeDashArray = new DoubleCollection { 3, 3 }
                    };
                    ChartCanvas.Children.Add(line);
                }
                prev = p;
            }
        }
    }
}
