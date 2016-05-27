namespace ServiceControl.Config.UI.MessageBox
{
    using System.Collections.Generic;
    using System.Windows.Input;
    using Caliburn.Micro;
    using ServiceControl.Config.Framework;
    using ServiceControl.Config.Framework.Rx;
    using ServiceControlInstaller.Engine.ReportCard;

    class ReportCardViewModel : RxScreen
    {
        public ReportCardViewModel(ReportCard reportCard)
        {
            Warnings = reportCard.Warnings;
            Errors = reportCard.Errors;

            Cancel = Command.Create(() => { Result = false; ((IDeactivate)this).Deactivate(true); });
        }

        public string Title { get; set; }
        public string ErrorsMessage { get; set; }
        public string WarningsMessage { get; set; }
        
        public IList<string> Warnings { get; set; }
        public IList<string> Errors { get; set; }

        public bool HasErrors => Errors.Count > 0;

        public bool HasWarnings => Warnings.Count > 0;

        public ICommand Cancel { get; private set; }
    }
}