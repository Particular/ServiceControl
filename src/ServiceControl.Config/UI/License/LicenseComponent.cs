namespace ServiceControl.Config.UI.License
{
    using System.Text;

    class LicenseComponent
    {
        public string Label { get; set; }
        public string Value { get; set; }
        public Importance Importance { get; set; }
        public string WarningText { get; set; }

        public bool IsSerious => Importance == Importance.Serious;
        public bool IsWarning => Importance == Importance.Warning;

        public override string ToString()
        {
            var builder = new StringBuilder(Label);
            builder.Append($" {Value}");
            if (Importance != Importance.Normal)
            {
                builder.Append($" [{Importance}]");
            }

            if (!string.IsNullOrWhiteSpace(WarningText))
            {
                builder.Append($" - {WarningText}");
            }

            return builder.ToString();
        }
    }
}