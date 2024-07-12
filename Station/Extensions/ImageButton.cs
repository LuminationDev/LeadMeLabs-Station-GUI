using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Station.Extensions;

public class ImageButton : Button
{
    public static readonly DependencyProperty HoverImageSourceProperty = DependencyProperty.Register(
        "HoverImageSource", typeof(ImageSource), typeof(ImageButton), new PropertyMetadata(null));

    public ImageSource HoverImageSource
    {
        get => (ImageSource)GetValue(HoverImageSourceProperty);
        set => SetValue(HoverImageSourceProperty, value);
    }
    
    public static readonly DependencyProperty SourceProperty = DependencyProperty.Register(
        "ImageSource", typeof(ImageSource), typeof(ImageButton), new PropertyMetadata(null));

    public ImageSource ImageSource
    {
        get => (ImageSource)GetValue(SourceProperty);
        set => SetValue(SourceProperty, value);
    }
}
