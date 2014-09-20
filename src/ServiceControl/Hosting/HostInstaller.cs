namespace Particular.ServiceControl.Hosting
{
    using System.Collections;
    using System.Configuration.Install;
    using Microsoft.Win32;

    internal class HostInstaller : Installer
    {
        public HostInstaller(HostArguments settings, string arguments, Installer[] installers)
        {
            this.installers = installers;
            this.arguments = arguments;
            this.settings = settings;
        }

        public override void Install(IDictionary stateSaver)
        {
            Installers.AddRange(installers);

            base.Install(stateSaver);

            using (var service = Registry.LocalMachine.OpenSubKey(string.Format(@"System\CurrentControlSet\Services\{0}", settings.ServiceName), true))
            {
                var imagePath = (string) service.GetValue("ImagePath");
                imagePath += arguments;
                service.SetValue("ImagePath", imagePath);
            }
        }

        public override void Uninstall(IDictionary savedState)
        {
            Installers.AddRange(installers);

            base.Uninstall(savedState);
        }

        readonly string arguments;
        readonly Installer[] installers;
        readonly HostArguments settings;
    }
}