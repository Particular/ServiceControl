namespace ServiceControl.Config.UI.AdvancedOptions
{
    using System.Collections.Generic;
    using System.Security.Cryptography.X509Certificates;
    using System.Windows;
    using System.Windows.Input;
    using Caliburn.Micro;
    using HttpApiWrapper;
    using PropertyChanged;
    using ReactiveUI;
    using ServiceControl.Config.Events;
    using ServiceControl.Config.Framework.Rx;
    using System.Windows.Interop;

    using ServiceControlInstaller.Engine.Instances;

    class CertificateDialogViewModel : RxScreen
    {
        private IEventAggregator eventAggregator;

        public Dictionary<string, string> X509Summary { get; set; }

        [AlsoNotifyFor(nameof(X509Summary))]
        public bool NoX509Summary => X509Summary == null;

        [AlsoNotifyFor(nameof(X509Summary))]
        public bool X509SummaryHasData => X509Summary != null;

        public bool IsDirty { get; set; }

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
                        var handle = new WindowInteropHelper((Window) GetView()).Handle;
                        store.Open(OpenFlags.ReadOnly);
                        var selectedCertificateCollection = X509Certificate2UI.SelectFromCollection(store.Certificates, "Certificate", "Choose a certificate", X509SelectionFlag.SingleSelection, handle);
                        if (selectedCertificateCollection.Count == 1)
                        {
                            IsDirty = true;
                            SslCert.MigrateToHttps(Instance.AclUrl, selectedCertificateCollection[0].GetCertHash());
                        }
                    }
                    finally
                    {
                        store.Close();
                        RetrieveCertInfo();
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
                    IsDirty = true;
                    SslCert.MigrateToHttp(Instance.AclUrl);
                    RetrieveCertInfo();
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
           RetrieveCertInfo();
        }

        void RetrieveCertInfo()
        {
            var thumbprint = SslCert.GetThumbprint(Instance.Port);
            if (thumbprint == null)
            {
                X509Summary = null;
                return;
            }

            var store = new X509Store(StoreName.My, StoreLocation.LocalMachine);
            try
            {
                store.Open(OpenFlags.ReadOnly);
                var selectedCertificateCollection = store.Certificates.Find(X509FindType.FindByThumbprint, thumbprint, false);
                if (selectedCertificateCollection.Count == 1)
                {
                    var cert = selectedCertificateCollection[0];
                    X509Summary = new Dictionary<string, string>()
                    {
                        {"Friendly Name", cert.FriendlyName},
                        {"Issuer", cert.GetNameInfo(X509NameType.SimpleName, true) },
                        {"Valid From", $"{cert.NotBefore.ToString("d")} to {cert.NotAfter.ToString("d")}"},
                        {"Thumbprint", cert.Thumbprint}
                    };
                }
                else
                {
                    X509Summary = new Dictionary<string, string>()
                    {
                        {"Status", "This certificate was not found in the Certificate store" },
                        {"Thumbprint", thumbprint},
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