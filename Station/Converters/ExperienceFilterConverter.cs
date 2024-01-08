using System;
using System.Globalization;
using System.Windows.Data;

namespace Station.Converters;

public class ExperienceFilterConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        return new Tuple<String, String>((String)values[0], (String)values[1]);
    }
    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
