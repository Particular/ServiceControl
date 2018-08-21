namespace ServiceControl.Config.Commands
{
    using System.Windows;
    using Framework.Commands;

    class CopyToClipboardCommand : AbstractCommand<string>
    {
        public override void Execute(string obj)
        {
            Clipboard.SetText(obj);
        }
    }
}