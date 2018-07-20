namespace ServiceControlInstaller.Engine
{
    using System;
    using System.Xml.Serialization;

    public class XmlTimeSpan
    {
        public XmlTimeSpan() : this(TimeSpan.Zero)
        {
        }

        public XmlTimeSpan(TimeSpan input)
        {
            timeSpan = input;
        }

        [XmlText]
        public string Value
        {
            get { return timeSpan.ToString(); }
            set { timeSpan = TimeSpan.Parse(value); }
        }

        public static implicit operator TimeSpan(XmlTimeSpan input)
        {
            return input?.timeSpan ?? TimeSpan.Zero;
        }

        // Alternative to the implicit operator TimeSpan(XmlTimeSpan input)
        public TimeSpan ToTimeSpan()
        {
            return timeSpan;
        }

        public static implicit operator XmlTimeSpan(TimeSpan input)
        {
            return new XmlTimeSpan(input);
        }

        public void FromTimeSpan(TimeSpan input)
        {
            timeSpan = input;
        }

        private TimeSpan timeSpan;
    }
}