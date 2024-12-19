namespace ServiceControl.Config.Extensions
{
    using System.Text;
    using System.Text.RegularExpressions;

    public static class InstanceNameExtensions
    {
        static string ReplaceSpacesWithPeriods(string name) =>
            Regex.Replace(name, @"\s+", " ")
                .Replace(' ', '.');

        //Valid service names use only ascii characters between 32-127 and not / or \ 
        //The code will remove invalid characters and replace spaces with . 
        //https://learn.microsoft.com/en-us/dotnet/api/system.serviceprocess.serviceinstaller.servicename?redirectedfrom=MSDN&view=netframework-4.8#remarks
        public static string SanitizeInstanceName(this string instanceName)
        {
            instanceName = instanceName.SanitizeFilePath();

            instanceName = instanceName.Length > 256 ? instanceName.Substring(0, 256) : instanceName;

            var serviceNameBuilder = new StringBuilder();

            foreach (char character in Encoding.UTF8.GetBytes(instanceName.ToCharArray()))
            {
                var asciiNumber = (int)character;

                if (asciiNumber is < 32 or > 122 or 47 or 92)
                {
                    continue;
                }
                else
                {
                    serviceNameBuilder.Append(character);
                }
            }

            instanceName = serviceNameBuilder.ToString();

            instanceName = instanceName.Trim();

            instanceName = ReplaceSpacesWithPeriods(instanceName);

            return instanceName;
        }
    }
}