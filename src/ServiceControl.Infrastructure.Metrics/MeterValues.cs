namespace ServiceControl.Infrastructure
{
    public readonly struct MeterValues
    {
        public MeterValues(string name, float current, float average15, float average60, float average300)
        {
            Current = current;
            Average15 = average15;
            Average60 = average60;
            Average300 = average300;
            Name = name;
        }

        public string Name { get; }
        public float Current { get; }
        public float Average15 { get; }
        public float Average60 { get; }
        public float Average300 { get; }
    }
}