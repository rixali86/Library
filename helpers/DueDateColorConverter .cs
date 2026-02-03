using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Library.Helpers
{
    public class DueDateColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is DateTime dueDate)
            {
                if (dueDate < DateTime.Now)
                    return Colors.Red; // Overdue
                if (dueDate < DateTime.Now.AddDays(2))
                    return Colors.Orange; // Due soon
            }
            return Colors.Black; // Normal
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }

    }
}
