namespace ServiceControl.Config.Framework
{
    using System;
    using System.Windows.Input;

    public interface IProgressViewModel
    {
        string ProgressTitle { set; }

        bool InProgress { set; }

        string ProgressMessage { set; }

        int ProgressPercent { set; }
    }

    public interface IProgressObject : IProgress<ProgressDetails>, IDisposable
    {
    }

    public struct ProgressDetails
    {
        public ProgressDetails(string message) : this(-1, message)
        {
        }

        public ProgressDetails(int percent, string message)
        {
            Percent = percent;
            Message = message;
            InProgress = true;
        }

        public int Percent;
        public string Message;
        public bool InProgress;
    }

    public static class ProgressViewModelExtensions
    {
        public static IProgressObject GetProgressObject(this IProgressViewModel progressViewModel, string progressTitle = "")
        {
            progressViewModel.ProgressTitle = progressTitle;
            return new Progress(progressViewModel);
        }

        public static void Report(this IProgress<ProgressDetails> progress, int step, int total, string message)
        {
            progress.Report(new ProgressDetails((int)(100.0 / total * step), message));
        }

        class Progress : IProgressObject
        {
            public Progress(IProgressViewModel viewModel)
            {
                this.viewModel = viewModel;
            }

            public void Report(ProgressDetails value)
            {
                viewModel.InProgress = value.InProgress;
                viewModel.ProgressMessage = value.Message;
                viewModel.ProgressPercent = value.Percent;
            }

            public void Dispose()
            {
                viewModel.InProgress = false;
                CommandManager.InvalidateRequerySuggested();
            }

            readonly IProgressViewModel viewModel;
        }
    }
}