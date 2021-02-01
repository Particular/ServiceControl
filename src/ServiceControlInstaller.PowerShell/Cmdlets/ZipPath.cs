namespace ServiceControlInstaller.PowerShell
{
    using System.IO;
    using System.Management.Automation;

    class ZipPath
    {
        public static string Get(PSCmdlet instance)
        {
            return Path.Combine(Path.GetDirectoryName(instance.MyInvocation.MyCommand.Module.Path), "..");
        }
    }
}