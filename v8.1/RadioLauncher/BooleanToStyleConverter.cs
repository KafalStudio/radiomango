using System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Data;

namespace RadioLauncher
{
    public class BooleanToStyleConverter : IValueConverter
    {
        public Style PlayStyle { get; set; }
        public Style StopStyle { get; set; }

        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is bool && (bool) value)
            {
                return StopStyle;
            }
            return PlayStyle;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}