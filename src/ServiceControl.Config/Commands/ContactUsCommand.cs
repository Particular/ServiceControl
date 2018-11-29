namespace ServiceControl.Config.Commands
{
    using System.Diagnostics;
    using Framework.Commands;

    class ContactUsCommand : AbstractCommand<object>
    {
        const string ContactUsUrl = "https://particular.net/contactus";

        public override void Execute(object obj)
        {
            var process = new Process
            {
                StartInfo =
                {
                    UseShellExecute = true,
                    FileName = ContactUsUrl
                }
            };

            process.Start();
        }
    }
}