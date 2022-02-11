namespace ServiceControlInstaller.PowerShell
{
    using System;
    using System.Reflection;

    public static class BindingRedirectAssemblyLoader
    {
        public static Assembly CurrentDomain_BindingRedirect(object sender, ResolveEventArgs args)
        {
            var name = new AssemblyName(args.Name);
            switch (name.Name)
            {
                case "System.Runtime.CompilerServices.Unsafe":
                    return Assembly.LoadFrom("System.Runtime.CompilerServices.Unsafe.dll");

                default:
                    return null;
            }
        }
    }
}
