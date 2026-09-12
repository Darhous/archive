using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Darhous.Archive.Desktop.Converters;

/// <summary>Bind an enum property to Visibility for one specific value, e.g. <c>Visibility="{Binding Kind, Converter={StaticResource EnumEquals}, ConverterParameter=Pdf}"</c>.</summary>
public sealed class EnumEqualsToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is null || parameter is null)
        {
            return Visibility.Collapsed;
        }

        var expected = Enum.Parse(value.GetType(), parameter.ToString()!);
        return Equals(value, expected) ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
