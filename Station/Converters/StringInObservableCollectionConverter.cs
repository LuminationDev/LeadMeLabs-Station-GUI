using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows.Data;

namespace Station.Converters;

public class StringInObservableCollectionConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        var name = values[0] as string;
        var collection = values[1] as ObservableCollection<string>;

        if (name == null || collection == null)
        {
            return false;
        }

        return collection.Contains(name);
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
    {
        var isChecked = (bool)value;
        var name = parameter as string;
        var collection = targetTypes[1].GetType() == typeof(ObservableCollection<string>) ? (ObservableCollection<string>)Activator.CreateInstance(targetTypes[1]) : null;

        if (name == null || collection == null)
        {
            return null;
        }

        if (isChecked && !collection.Contains(name))
        {
            collection.Add(name);
        }
        else if (!isChecked && collection.Contains(name))
        {
            collection.Remove(name);
        }

        return new object[] { name, collection };
    }
}
