namespace ServiceControlInstaller.Engine.Services
{
    internal class WindowsServiceDetails
    {
        public string Name { get; set; }

        public string DisplayName { get; set; }

        public string ServiceAccount { get; set; }

        public string ServiceAccountPwd { get; set; }

        public string ImagePath { get; set; }

        public string ServiceDescription { get; set; }
    }
}