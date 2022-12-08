namespace ServiceControlInstaller.PowerShell
{
    using System;
    using System.Reflection;

    public static class BindingRedirectAssemblyLoader
    {
        public static Assembly CurrentDomain_BindingRedirect(object sender, ResolveEventArgs args)
        {
            var name = new AssemblyName(args.Name);
            return name.Name switch
            {
                "System.Runtime.CompilerServices.Unsafe" => Assembly.LoadFrom("System.Runtime.CompilerServices.Unsafe.dll"),
                _ => null,
            };
        }
    }
}
