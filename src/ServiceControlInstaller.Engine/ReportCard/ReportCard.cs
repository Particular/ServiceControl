namespace ServiceControlInstaller.Engine.ReportCard
{
    using System.Collections.Generic;
    
    public class ReportCard
    {
        public ReportCard()
        {
           
        }

        public Status Status
        {
            get
            {
                if (HasErrors)
                    Status = Status.Failed;
                 else if (HasWarnings)
                    return Status.CompletedWithWarnings;

                if (status.HasValue)
                    return status.Value;
                return Status.Completed;
            }
            set { status = value; }
        }

        public IList<string> Warnings = new TruncatedStringList(700);
        public IList<string> Errors = new TruncatedStringList(700);

        public bool HasErrors => Errors.Count > 0;

        public bool HasWarnings => Warnings.Count > 0;

        public bool CancelRequested;
        private Status? status;

    }
}