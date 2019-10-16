namespace ServiceControl.Config
{
    using System;
    using System.Diagnostics;
    using System.IO;
    using System.Reflection;
    using System.Windows;
    using UI.MessageBox;

    public partial class App
    {
        public App()
        {
            InitializeComponent();

            if (!Debugger.IsAttached)
            {
                ExceptionMessageBox.Attach();
            }

            EnsureStandardPopupAlignment();
        }

        static void EnsureStandardPopupAlignment()
        {
            //HINT: this code enforces MenuDropAlignment to be default. This is important on laptops that enable `Tablet Mode`.
            //      In such case the dropdowns are moved to the left to not appear under hand when using touch. This behavior
            //      applies even if the users are not using touch and causes dropdowns to be misaligned. It's fine to override
            //      the setting at once at startup because the only way to change it by the users is via Control Panel.
            //      See: https://github.com/Particular/ServiceControl/issues/1374 for more details
            var dropdownAlignment = typeof(SystemParameters).GetField("_menuDropAlignment", BindingFlags.NonPublic | BindingFlags.Static);

            if (SystemParameters.MenuDropAlignment && dropdownAlignment != null)
            {
                dropdownAlignment.SetValue(null, false);
            }
        }

        [STAThread]
        public static void Main(string[] args)
        {
            AppDomain.CurrentDomain.AssemblyResolve += (s, e) => ResolveAssembly(e.Name);

            Splash.Show();
            if (args.Length == 0)
            {
                var app = new App();
                app.Run();
            }
        }

        static Assembly ResolveAssembly(string name)
        {
            var assemblyLocation = Assembly.GetEntryAssembly().Location;
            var appDirectory = Path.GetDirectoryName(assemblyLocation);
            var requestingName = new AssemblyName(name).Name;

            // ReSharper disable once AssignNullToNotNullAttribute
            var combine = Path.Combine(appDirectory, requestingName + ".dll");
            if (!File.Exists(combine))
            {
                return null;
            }

            return Assembly.LoadFrom(combine);
        }
    }
}