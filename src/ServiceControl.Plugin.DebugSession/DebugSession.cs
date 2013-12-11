namespace NServiceBus.Features
{
    using System.IO;
    using System.Reflection;
    using ServiceControl.Plugin.DebugSession;

    public class DebugSession : Feature
    {
        const string ServiceControlDebugSessionIdFileName = "ServiceControl.DebugSessionId.txt";

        public override bool ShouldBeEnabled()
        {
            return File.Exists(DebugSessionFilename);
        }

        public override bool IsEnabledByDefault
        {
            get { return true; }
        }

        public override void Initialize()
        {
            Configure.Component<ApplyDebugSessionId>(DependencyLifecycle.SingleInstance)
                .ConfigureProperty(p => p.SessionId, File.ReadAllText(DebugSessionFilename));

        }
                
        private string DetermineCorrectPathTo(string file)
        {
            var binPath = Path.GetDirectoryName((new System.Uri(Assembly.GetExecutingAssembly().CodeBase)).LocalPath);
            return binPath != null ? Path.Combine(binPath, file) : file;
        }

        private string _debugSessionFileName = null;
        public string DebugSessionFilename
        {
            get
            {
                if (_debugSessionFileName == null)
                {
                    _debugSessionFileName = DetermineCorrectPathTo(ServiceControlDebugSessionIdFileName);
                }
                return _debugSessionFileName;
            }
        }
    }
}