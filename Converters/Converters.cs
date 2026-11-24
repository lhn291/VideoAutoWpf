using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using VideoAutoWpf.Models.Minimal;

namespace VideoAutoWpf.Converters;

public class InverseBooleanConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool b) return !b;
        return true;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool b) return !b;
        return false;
    }
}

public class InverseBoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool b) return b ? Visibility.Collapsed : Visibility.Visible;
        return Visibility.Visible;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is Visibility v) return v != Visibility.Visible;
        return false;
    }
}

public class RatioMatchConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string currentRatio && parameter is string targetRatio)
        {
            return string.Equals(currentRatio, targetRatio, StringComparison.OrdinalIgnoreCase);
        }
        return false;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool isChecked && isChecked && parameter is string targetRatio)
        {
            return targetRatio;
        }
        return Binding.DoNothing;
    }
}

public class StringNotEmptyToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return !string.IsNullOrWhiteSpace(value as string) ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public class StepStatusToTextConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is StepStatus status)
        {
            return status switch
            {
                StepStatus.Waiting => "⏳ Chờ thực hiện",
                StepStatus.Processing => "🔄 Đang xử lý...",
                StepStatus.Completed => "✅ Hoàn thành",
                StepStatus.Failed => "❌ Thất bại",
                _ => "Chờ thực hiện"
            };
        }
        return "Chờ thực hiện";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
}

public class StepStatusToBrushConverter : IValueConverter
{
    private static readonly SolidColorBrush WaitingBrush = new(Color.FromRgb(0x6c, 0x70, 0x86));
    private static readonly SolidColorBrush ProcessingBrush = new(Color.FromRgb(0x89, 0xb4, 0xfa));
    private static readonly SolidColorBrush CompletedBrush = new(Color.FromRgb(0xa6, 0xe3, 0xa1));
    private static readonly SolidColorBrush FailedBrush = new(Color.FromRgb(0xf3, 0x8b, 0xa8));

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is StepStatus status)
        {
            return status switch
            {
                StepStatus.Waiting => WaitingBrush,
                StepStatus.Processing => ProcessingBrush,
                StepStatus.Completed => CompletedBrush,
                StepStatus.Failed => FailedBrush,
                _ => WaitingBrush
            };
        }
        return WaitingBrush;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
}
