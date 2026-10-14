using System.Globalization;
using System.Windows.Data;
using SberProject.Core.Models;

namespace SberProject.Converters;

[ValueConversion(typeof(Enum), typeof(string))]
public sealed class EnumDescriptionConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is Enum item ? item.GetDescription() : string.Empty;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        Binding.DoNothing;
}
