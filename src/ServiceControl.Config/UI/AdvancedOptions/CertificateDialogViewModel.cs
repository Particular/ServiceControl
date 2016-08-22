namespace ServiceControl.Config.UI.AdvancedOptions
{
    using System.Security.Cryptography.X509Certificates;
    using System.Windows.Input;
    using Caliburn.Micro;
    using HttpApiWrapper;
    using ReactiveUI;
    using ServiceControl.Config.Events;
    using ServiceControl.Config.Framework.Rx;
    using ServiceControlInstaller.Engine.Instances;

    class CertificateDialogViewModel : RxScreen
    {
        private IEventAggregator eventAggregator;

        public class X509Summary
        {
            public string FriendlyName { get; set; }
            public string Subject { get; set; }
            public string Thumbprint { get; set; }
        }

        public X509Summary CertificateInfo { get; set; }
        public ServiceControlInstance Instance;

        public CertificateDialogViewModel(IEventAggregator eventAggregator, ServiceControlInstance serviceControlInstance)
        {
            this.eventAggregator = eventAggregator;
            Instance = serviceControlInstance;
        }

        public ICommand ApplyCertificateCommand
        {
            get
            {
                return new ReactiveCommand().DoAction(obj =>
                {
                    var store = new X509Store(StoreName.My, StoreLocation.LocalMachine);
                    try
                    {
                        store.Open(OpenFlags.ReadOnly);
                        var selectedCertificateCollection = X509Certificate2UI.SelectFromCollection(store.Certificates, "Certificate", "Choose a certificate", X509SelectionFlag.SingleSelection);
                        if (selectedCertificateCollection.Count == 1)
                        {
                            SslCert.MigrateToHttps(Instance.AclUrl, selectedCertificateCollection[0].GetCertHash());
                        }
                    }
                    finally
                    {
                        store.Close();
                        CertificateInfo = RetrieveCertInfo();
                        eventAggregator.PublishOnUIThread(new RefreshInstances());
                    }
                });
            }
        }

        public ICommand RemoveCertificateCommand
        {
            get
            {
                return new ReactiveCommand().DoAction(obj =>
                {
                    SslCert.MigrateToHttp(Instance.AclUrl);
                    CertificateInfo = RetrieveCertInfo();
                    eventAggregator.PublishOnUIThread(new RefreshInstances());
                });
            }
        }

        public ICommand ExitCommand
        {
            get
            {
                return new ReactiveCommand().DoAction(obj =>{ TryClose(false);});
            }
        }

        protected override void OnActivate()
        {
           CertificateInfo = RetrieveCertInfo();
        }

        X509Summary RetrieveCertInfo()
        {
            var thumbPrint = SslCert.GetThumbprint(Instance.Port);
            if (thumbPrint == null)
                return null;

            var store = new X509Store(StoreName.My, StoreLocation.LocalMachine);
            try
            {
                store.Open(OpenFlags.ReadOnly);
                var selectedCertificateCollection = store.Certificates.Find(X509FindType.FindByThumbprint, thumbPrint, false);
                if (selectedCertificateCollection.Count == 1)
                {
                    var cert = selectedCertificateCollection[0];
                    return new X509Summary
                    {
                        FriendlyName = cert.FriendlyName,
                        Subject = cert.Subject,
                        Thumbprint = cert.Thumbprint
                    };
                }
                else
                {
                    return new X509Summary
                    {
                        FriendlyName = "<Missing Certificate!>",
                        Thumbprint = thumbPrint
                    };
                }
            }
            finally
            {
                store.Close();
            }
        }
    }
}