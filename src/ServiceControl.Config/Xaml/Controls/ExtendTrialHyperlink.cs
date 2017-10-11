namespace ServiceControl.Config.Xaml.Controls
{
    using System;
    using System.Diagnostics;
    using System.Windows;
    using System.Windows.Documents;

    public class ExtendTrialHyperlink : Hyperlink
    {
        const string ExtendTrialUrl = "https://particular.net/extend-your-trial";

        public ExtendTrialHyperlink()
        {
            NavigateUri = new Uri(ExtendTrialUrl);
            Inlines.Add(ExtendTrialUrl);

            Style = new Style(GetType(), FindResource(typeof(Hyperlink)) as Style);
        }

        protected override void OnClick()
        {
            base.OnClick();

            Process p = new Process()
            {
                StartInfo = new ProcessStartInfo()
                {
                    FileName = this.NavigateUri.AbsoluteUri
                }
            };
            p.Start();
        }

        
    }
}