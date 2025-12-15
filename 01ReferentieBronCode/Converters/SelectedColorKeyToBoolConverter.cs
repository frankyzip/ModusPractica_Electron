using System;
using System.Globalization;
using System.Windows.Data;

namespace ModusPractica
{
    /// <summary>
    /// Converts between the selected color resource key and individual radio button IsChecked values.
    /// </summary>
    public sealed class SelectedColorKeyToBoolConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (parameter is not string expectedKey)
            {
                return false;
            }

            var currentKey = value as string;
            return string.Equals(currentKey, expectedKey, StringComparison.OrdinalIgnoreCase);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is true && parameter is string key)
            {
                return key;
            }

            return Binding.DoNothing;
        }
    }
}
