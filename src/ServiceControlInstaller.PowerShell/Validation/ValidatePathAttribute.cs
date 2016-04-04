namespace ServiceControlInstaller.PowerShell
{
    using System;
    using System.Management.Automation;
    using Microsoft.PowerShell.Commands;
    
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
    class ValidatePathAttribute : ValidateArgumentsAttribute
    {
        protected override void Validate(object arguments, EngineIntrinsics engineIntrinsics)
        {
            ProviderInfo provider;
            PSDriveInfo drive;
            engineIntrinsics.SessionState.Path.GetUnresolvedProviderPathFromPSPath((string)arguments, out provider, out drive);
            if (provider.ImplementingType != typeof(FileSystemProvider))
            {
                throw new ArgumentException("Path is invalid");
            }
        }
    }
}
