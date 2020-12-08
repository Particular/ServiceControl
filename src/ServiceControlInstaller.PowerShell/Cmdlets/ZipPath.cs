using System.IO;
using System.Management.Automation;

namespace ServiceControlInstaller.PowerShell
{
    class ZipPath
    {
        public static string Get(PSCmdlet instance)
        {
            return Path.Combine(Path.GetDirectoryName(instance.MyInvocation.MyCommand.Module.Path), "..");
        }
    }
}