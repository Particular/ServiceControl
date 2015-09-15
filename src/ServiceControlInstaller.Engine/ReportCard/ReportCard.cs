namespace ServiceControlInstaller.Engine.ReportCard
{
    using System.Collections.Generic;
    
    public class ReportCard
    {
        public Status Status { get; set; }
        public IList<string> Warnings = new TruncatedStringList(700);
        public IList<string> Errors = new TruncatedStringList(700);

        public bool HasErrors
        {
            get { return Errors.Count > 0; }
        }

        public bool HasWarnings
        {
            get { return Warnings.Count > 0; }
        }

        public void SetStatus()
        {
            if (HasErrors)
                Status = Status.Failed;
            else if (HasWarnings)
                Status = Status.CompletedWithWarnings;
            else
                Status = Status.Completed;
        }
    }
}