using System;
using System.Globalization;
using System.IO;
using System.Windows.Data;
using System.Windows.Media.Imaging;
using Station.Components._managers;
using Station.Components._organisers;

namespace Station.Converters;

public class ImageUrlConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        try
        {
            // Check the local cache first
            string? local = ThumbnailOrganiser.GetEntry((string)values[2]);
            if (local != null)
            {
                return new BitmapImage(new Uri(local));
            }
            
            switch (values[0])
            {
                case "Steam":
                    return new BitmapImage(
                        new Uri($"https://cdn.cloudflare.steamstatic.com/steam/apps/{values[1]}/header.jpg?"));

                case "Custom":
                    return new BitmapImage(new Uri("pack://application:,,,/Assets/Images/default_header.jpg"));
                
                case "Embedded":
                    string? id = values[1].ToString();
                    if (id == null)
                    {
                        return new BitmapImage(new Uri("pack://application:,,,/Assets/Images/default_header.jpg"));
                    }
                    
                    WrapperManager.ApplicationList.TryGetValue(id, out var experience);
                    string filePath = experience.HeaderPath ?? Path.GetFullPath(Path.Combine(experience.AltPath, "..", "header.jpg"));
                    return !File.Exists(filePath) ? new BitmapImage(new Uri("pack://application:,,,/Assets/Images/default_header.jpg")) : new BitmapImage(new Uri(filePath));

                case "Vive":
                    return new BitmapImage(new Uri("pack://application:,,,/Assets/Images/default_header.jpg"));

                case "Revive":
                    return new BitmapImage(new Uri("pack://application:,,,/Assets/Images/default_header.jpg"));
            }
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }

        // Default if parameters are not as expected
        return new BitmapImage(new Uri("pack://application:,,,/Assets/Images/default_header.jpg"));
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
