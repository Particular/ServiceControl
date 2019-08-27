namespace ServiceControl.Monitoring.UnitTests.API
{
    using Nancy;
    using System.Collections.Generic;
    using System.Dynamic;

    public static class DynamicExtensions
    {
        public static dynamic ToDynamic<T>(this T obj)
        {
            IDictionary<string, object> expando = new ExpandoObject();

            foreach (var propertyInfo in typeof(T).GetProperties())
            {
                var currentValue = propertyInfo.GetValue(obj);
                expando.Add(propertyInfo.Name, currentValue);
            }
            return (ExpandoObject)expando;
        }
    }
}