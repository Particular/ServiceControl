namespace ServiceControl.Configuration;

using System;
using System.ComponentModel;

static class ValueConverter
{
    public static T Convert<T>(object value)
    {
        var underlyingType = Nullable.GetUnderlyingType(typeof(T)) ?? typeof(T);
        var converter = TypeDescriptor.GetConverter(underlyingType);
        return (T)converter.ConvertFrom(value);
    }
}