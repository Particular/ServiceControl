namespace ServiceControl.Configuration;

using System;

static class ValueConverter
{
    public static T Convert<T>(object value)
    {
        var underlyingType = Nullable.GetUnderlyingType(typeof(T)) ?? typeof(T);
        return (T)System.Convert.ChangeType(value, underlyingType);
    }
}