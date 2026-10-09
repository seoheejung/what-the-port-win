using System;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Windows.Data;

namespace WhatThePort.App {
    public sealed class ToolTipTextConverter : IValueConverter {
        public static string Sentences(string text){
            if(String.IsNullOrEmpty(text))return text;
            // Paths and URLs are data, even when a folder name contains a period and space.
            if(Regex.IsMatch(text,@"\A(?:[A-Za-z]:[\\/]|\\\\|[A-Za-z][A-Za-z0-9+.-]*://)"))return text;
            return Regex.Replace(text,@"(?<=[.!?])[ \t]+(?=\S)","\n");
        }
        public object Convert(object value,Type targetType,object parameter,CultureInfo culture){return Sentences(value as string);}
        public object ConvertBack(object value,Type targetType,object parameter,CultureInfo culture){throw new NotSupportedException();}
    }
}
