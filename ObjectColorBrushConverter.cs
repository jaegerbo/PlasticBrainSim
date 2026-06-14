using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace PlasticBrainSim;

public sealed class ObjectColorBrushConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values.Length < 3 ||
            values[0] is not byte red ||
            values[1] is not byte green ||
            values[2] is not byte blue)
        {
            return Brushes.Transparent;
        }

        return new SolidColorBrush(Color.FromRgb(red, green, blue));
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}