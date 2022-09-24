using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Shell;
using X410Launcher.ViewModels;

namespace X410Launcher.Converters;

public class ProgressStateConverter : IValueConverter
{
    public object? Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (targetType != typeof(TaskbarItemProgressState))
        {
            throw new ArgumentException($"Bad target type: {targetType.FullName}", nameof(targetType));
        }

        // Can't use binding for ViewModel so we'll pass it as a parameter.
        if (parameter is X410StatusViewModel status)
        {
            if (status.ProgressIsIndeterminate)
            {
                return TaskbarItemProgressState.Indeterminate;
            }
            else if (status.Progress == 0)
            {
                return TaskbarItemProgressState.None;
            }
            else
            {
                return TaskbarItemProgressState.Normal;
            }
        }

        return TaskbarItemProgressState.None;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
