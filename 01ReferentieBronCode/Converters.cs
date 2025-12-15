using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using System.Linq;

namespace ModusPractica
{
    // --- BESTAANDE CONVERTERS ---
    public class StringToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            return string.IsNullOrEmpty(value as string) ? Visibility.Collapsed : Visibility.Visible;
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class ProgressToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (value is double progress)
            {
                if (progress < 0.25) return new SolidColorBrush(Colors.Red);
                if (progress < 0.50) return new SolidColorBrush(Colors.Orange);
                if (progress < 0.75) return new SolidColorBrush(Colors.Yellow);
                return new SolidColorBrush(Colors.Green);
            }

            return new SolidColorBrush(Colors.Gray);
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    // --- NIEUWE CONVERTERS VOOR DE SCORE-KLEUR ---
    public class GreaterThanValueConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is float score && parameter is string thresholdStr && float.TryParse(thresholdStr, NumberStyles.Any, CultureInfo.InvariantCulture, out float threshold))
            {
                return score >= threshold;
            }
            return false;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }

    public class LessThanValueConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is float score && parameter is string thresholdStr && float.TryParse(thresholdStr, NumberStyles.Any, CultureInfo.InvariantCulture, out float threshold))
            {
                // We check > 0 to exclude the "N/A" case which has a score of 0
                return score > 0 && score < threshold;
            }
            return false;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }

    public class RangeValueConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is float score && parameter is string rangeStr)
            {
                var parts = rangeStr.Split(',');
                if (parts.Length == 2 &&
                    float.TryParse(parts[0], NumberStyles.Any, CultureInfo.InvariantCulture, out float min) &&
                    float.TryParse(parts[1], NumberStyles.Any, CultureInfo.InvariantCulture, out float max))
                {
                    return score >= min && score <= max;
                }
            }
            return false;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }

    // --- NIEUW: Converter voor totale tijd per barsectie (incl. voorbereiding) ---
    public class BarSectionTotalTimeConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is BarSection section)
            {
                var history = PracticeHistoryManager.Instance.GetHistoryForBarSection(section.Id);
                var total = history
                    .Where(h => !h.IsDeleted)
                    .Aggregate(TimeSpan.Zero, (acc, h) => acc + h.Duration + h.PreparatoryPhaseDuration);
                return $"{(int)total.TotalHours:00}:{total.Minutes:00}:{total.Seconds:00}";
            }
            return "00:00";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }

    // --- NIEUW: Converter voor tooltip-tekst met uitsplitsing ---
    public class BarSectionTimeTooltipConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is BarSection section)
            {
                var history = PracticeHistoryManager.Instance.GetHistoryForBarSection(section.Id);
                var prep = history.Where(h => !h.IsDeleted).Aggregate(TimeSpan.Zero, (acc, h) => acc + h.PreparatoryPhaseDuration);
                var practice = history.Where(h => !h.IsDeleted).Aggregate(TimeSpan.Zero, (acc, h) => acc + h.Duration);
                var total = prep + practice;

                string F(TimeSpan ts) => $"{(int)ts.TotalHours:00}:{ts.Minutes:00}:{ts.Seconds:00}";
                return $"Total: {F(total)}\nPractice: {F(practice)}\nPreparation: {F(prep)}";
            }
            return string.Empty;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }

    /// <summary>
    /// Converts a nullable value to Visibility.
    /// Returns Visible if value is NOT null, Collapsed if it is null.
    /// </summary>
    public class NullToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value != null ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }
}
