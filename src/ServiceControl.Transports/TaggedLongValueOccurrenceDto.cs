namespace ServiceControl.Transports
{
    public class TaggedLongValueOccurrenceDto
    {
        public TaggedLongValueOccurrenceDto(EntryDto[] messageEntries, string messageTagValue)
        {
            Entries = messageEntries;
            TagValue = messageTagValue;
        }

        public EntryDto[] Entries { get; set; }
        public string TagValue { get; set; }
    }
}