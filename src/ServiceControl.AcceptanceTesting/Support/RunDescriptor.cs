namespace NServiceBus.AcceptanceTesting.Support
{
    using System;

    public class RunDescriptor
    {
        protected bool Equals(RunDescriptor other)
        {
            return string.Equals(Key, other.Key);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != GetType()) return false;
            return Equals((RunDescriptor) obj);
        }

        public override int GetHashCode()
        {
            return Key != null ? Key.GetHashCode() : 0;
        }

        public string Key { get; set; }

        public ScenarioContext ScenarioContext { get; set; }

        public TimeSpan TestExecutionTimeout { get; set; }

    }
}