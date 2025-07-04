#nullable enable

namespace ServiceControl.Configuration;

using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;

public class AppConfigConfigurationSource : IConfigurationSource
{
    public IConfigurationProvider Build(IConfigurationBuilder builder)
    {
        var propertiesWithAttribute = from a in AppDomain.CurrentDomain.GetAssemblies()
                                      from t in a.GetTypes()
                                      from p in t.GetProperties()
                                      let attributes = p.GetCustomAttributes(typeof(AppConfigSettingAttribute), true)
                                      where attributes != null && attributes.Length > 0
                                      select new { Type = p, Attribute = attributes.Cast<AppConfigSettingAttribute>().Single() };

        var mappings = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);

        foreach (var property in propertiesWithAttribute)
        {
            var section = property.Type.DeclaringType!.Name.Replace("Options", "");
            var name = property.Type.Name;
            mappings[$"{section}:{name}"] = property.Attribute.Keys;
        }

        return new AppConfigConfigurationProvider(mappings);
    }
}
