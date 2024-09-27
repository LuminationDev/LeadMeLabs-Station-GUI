using System;
using System.Globalization;
using System.Windows.Data;
using Station.Components._enums;

namespace Station.Converters;

public class EnumValueConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is Enum enumValue)
        {
            return Attributes.GetEnumValue(enumValue);
        }
        return value;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
