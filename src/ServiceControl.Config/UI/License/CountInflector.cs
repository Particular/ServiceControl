namespace ServiceControl.Config.UI.License
{
    class CountInflector
    {
        public string Singular { get; set; } = "0";
        public string Plural { get; set; } = "0";

        public string Inflect(int n)
            => n == 1
                ? string.Format(Singular, n)
                : string.Format(Plural, n);

    }
}