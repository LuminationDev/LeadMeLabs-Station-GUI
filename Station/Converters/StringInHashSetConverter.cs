using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows.Data;

namespace Station.Converters;

public class StringInHashSetConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values.Length == 2 && values[0] is string checkBoxName && values[1] is HashSet<string> nameHashSet)
        {
            return nameHashSet.Contains(checkBoxName);
        }

        return false;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
    {
        if ((bool)value)
        {
            // If the CheckBox is checked, return the parameter (CheckBox name)
            return new[] { parameter };
        }

        // If the CheckBox is unchecked, return an empty string or null
        return new object[] { "" };
    }
}
