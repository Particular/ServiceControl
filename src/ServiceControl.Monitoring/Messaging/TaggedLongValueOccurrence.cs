namespace ServiceControl.Monitoring.Messaging
{
    public class TaggedLongValueOccurrence : RawMessage
    {
        public string TagValue { get; set; }

        public bool TryRecord(long dateTicks, long value)
        {
            if (IsFull)
            {
                return false;
            }

            Entries[Index].DateTicks = dateTicks;
            Entries[Index].Value = value;

            Index += 1;

            return true;
        }
    }
}