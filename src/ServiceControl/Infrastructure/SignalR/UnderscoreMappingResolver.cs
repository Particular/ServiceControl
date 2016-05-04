namespace ServiceControl.Infrastructure.SignalR
{
    using System.Globalization;
    using System.Text.RegularExpressions;
    using Newtonsoft.Json.Serialization;

    public class UnderscoreMappingResolver : DefaultContractResolver
    {
        protected override string ResolvePropertyName(string propertyName)
        {
            return Regex.Replace(
                propertyName, @"([A-Z])([A-Z][a-z])|([a-z0-9])([A-Z])", "$1$3_$2$4").ToLower(CultureInfo.InvariantCulture);
        }
    }
}