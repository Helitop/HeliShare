using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace HeliShare.Helpers
{

    public class NullToVisibilityConverter : IValueConverter
    {
        /// <summary>
        /// Если true: возвращает Collapsed, когда значение ЕСТЬ.
        /// Если false (по умолчанию): возвращает Collapsed, когда значения НЕТ.
        /// </summary>
        public bool Invert { get; set; }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool hasValue = false;

            // Проверка на наличие значения
            if (value != null)
            {
                if (value is string s)
                    hasValue = !string.IsNullOrWhiteSpace(s);
                else if (value is byte[] bytes)
                    hasValue = bytes.Length > 0;
                else
                    hasValue = true;
            }

            // Логика инверсии
            if (Invert)
                hasValue = !hasValue;

            return hasValue ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return Binding.DoNothing;
        }
    }
}