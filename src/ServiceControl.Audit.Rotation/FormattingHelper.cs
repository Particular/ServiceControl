namespace ServiceControl.Audit.Rotation
{
    using System.CommandLine.Rendering;

    static class FormattingHelper
    {
        public static TextSpan Underline(this string value) =>
            new ContainerSpan(StyleSpan.UnderlinedOn(),
                new ContentSpan(value),
                StyleSpan.UnderlinedOff());

        public static TextSpan Bold(this string value) =>
            new ContainerSpan(StyleSpan.BoldOn(),
                new ContentSpan(value),
                StyleSpan.BoldOff());


        public static TextSpan Rgb(this string value, byte r, byte g, byte b) =>
            new ContainerSpan(ForegroundColorSpan.Rgb(r, g, b),
                new ContentSpan(value),
                ForegroundColorSpan.Reset());

        public static TextSpan LightGreen(this string value) =>
            new ContainerSpan(ForegroundColorSpan.LightGreen(),
                new ContentSpan(value),
                ForegroundColorSpan.Reset());

        public static TextSpan White(this string value) =>
            new ContainerSpan(ForegroundColorSpan.White(),
                new ContentSpan(value),
                ForegroundColorSpan.Reset());
    }
}