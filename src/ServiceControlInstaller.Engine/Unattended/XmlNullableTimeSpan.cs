namespace ServiceControlInstaller.Engine
{
    using System;
    using System.Xml.Serialization;

    public class XmlNullableTimeSpan
    {
        public XmlNullableTimeSpan() : this(null)
        {
        }

        public XmlNullableTimeSpan(TimeSpan? input)
        {
            timeSpan = input;
        }

        [XmlText]
        public string Value
        {
            get { return timeSpan.ToString(); }
            set { timeSpan = TimeSpan.Parse(value); }
        }

        public static implicit operator TimeSpan?(XmlNullableTimeSpan input)
        {
            return input?.timeSpan;
        }

        // Alternative to the implicit operator TimeSpan(XmlTimeSpan input)
        public TimeSpan? ToTimeSpan()
        {
            return timeSpan;
        }

        public static implicit operator XmlNullableTimeSpan(TimeSpan? input)
        {
            return new XmlNullableTimeSpan(input);
        }

        public void FromTimeSpan(TimeSpan? input)
        {
            timeSpan = input;
        }

        TimeSpan? timeSpan;
    }
}