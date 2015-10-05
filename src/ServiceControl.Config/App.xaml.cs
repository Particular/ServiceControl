namespace ServiceControl.Config
{
    using System;
    using System.Diagnostics;
    using System.Security.Principal;
    using ServiceControl.Config.UI.MessageBox;

    public partial class App
    {
        public App()
        {
            InitializeComponent();

            if (!Debugger.IsAttached)
                ExceptionMessageBox.Attach();
        }

        [STAThread]
        public static void Main(string[] args)
        {
            var isElevated = new WindowsPrincipal(WindowsIdentity.GetCurrent())
                    .IsInRole(WindowsBuiltInRole.Administrator);

            if (!isElevated)
            {
                Console.WriteLine("Task requires app to be run with elevated permissions.");
                return;
            }

            if (args.Length == 0)
            {
                var app = new App();
                app.Run();
            }
        }
    }
}