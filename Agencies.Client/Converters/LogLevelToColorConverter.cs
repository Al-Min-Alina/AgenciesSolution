using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace Agencies.Client.Converters
{
    public class LogLevelToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string level)
            {
                return level.ToUpper() switch
                {
                    "ERROR" => Brushes.Red,
                    "WARNING" => Brushes.Orange,
                    "INFO" => Brushes.Blue,
                    "DEBUG" => Brushes.Gray,
                    _ => Brushes.Black
                };
            }
            return Brushes.Black;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}