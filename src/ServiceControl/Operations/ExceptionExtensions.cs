namespace ServiceControl.Operations
{
    using System;
    using System.Text;

    public static class ExceptionExtensions
    {
        public static string ToFriendlyString(this Exception exception)
        {
            var stringBuilder = new StringBuilder();
            stringBuilder.Append("Exception:");
            stringBuilder.Append(Environment.NewLine);
            while (exception != null)
            {
                stringBuilder.AppendLine(exception.Message);

                foreach (var data in exception.Data)
                {
                    stringBuilder.Append("Data :");
                    stringBuilder.AppendLine(data.ToString());
                }

                if (exception.StackTrace != null)
                {
                    stringBuilder.AppendLine("StackTrace:");
                    stringBuilder.AppendLine(exception.StackTrace);
                }

                if (exception.Source != null)
                {
                    stringBuilder.AppendLine("Source:");
                    stringBuilder.AppendLine(exception.Source);
                }

                if (exception.TargetSite != null)
                {
                    stringBuilder.AppendLine("TargetSite:");
                    stringBuilder.AppendLine(exception.TargetSite.ToString());
                }

                exception = exception.InnerException;
            }

            return stringBuilder.ToString();
        }
    }
}