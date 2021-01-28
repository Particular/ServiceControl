namespace ServiceControl.Config.UI.MessageBox
{
    using System.Text;
    using System.Windows;

    public partial class ReportCardView
    {
        public ReportCardView()
        {
            InitializeComponent();
        }

        void CopyClick(object sender, RoutedEventArgs e)
        {
            var vm = (ReportCardViewModel)DataContext;

            var messages = new StringBuilder();
            messages.Append(vm.Title).Append(":");
            messages.AppendLine();
            messages.AppendLine(vm.ErrorsMessage);

            if (vm.HasWarnings)
            {
                foreach (var warning in vm.Warnings)
                {
                    messages.AppendLine(warning);
                }
            }

            if (vm.HasErrors)
            {
                foreach (var error in vm.Errors)
                {
                    messages.AppendLine(error);
                }
            }

            messages.AppendLine(Footer.Text);

            Clipboard.SetText(messages.ToString());
        }
    }
}