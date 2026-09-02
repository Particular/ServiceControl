using System.Configuration;
using System.Reflection;
using NUnit.Framework;

[SetUpFixture]
public class AppSettingsFixture
{
    [OneTimeSetUp]
    public void LoadAppSettings()
    {
        var assemblyName = Assembly.GetExecutingAssembly().GetName();
        var configuration = ConfigurationManager.OpenExeConfiguration($"{assemblyName.Name}.dll");
        foreach (var key in configuration.AppSettings.Settings.AllKeys)
        {
            ConfigurationManager.AppSettings.Set(key, configuration.AppSettings.Settings[key].Value);
        }
    }
}