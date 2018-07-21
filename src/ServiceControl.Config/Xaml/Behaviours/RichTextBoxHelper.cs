namespace ServiceControl.Config.Xaml.Behaviours
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Text;
    using System.Threading;
    using System.Windows;
    using System.Windows.Controls;
    using System.Windows.Documents;
    using System.Windows.Markup;

    public class RichTextBoxHelper : DependencyObject
    {
        public static string GetDocumentXaml(DependencyObject obj)
        {
            return (string)obj.GetValue(DocumentXamlProperty);
        }

        public static void SetDocumentXaml(DependencyObject obj, string value)
        {
            obj.SetValue(DocumentXamlProperty, value);
        }

        static HashSet<Thread> _recursionProtection = new HashSet<Thread>();

        public static readonly DependencyProperty DocumentXamlProperty =
            DependencyProperty.RegisterAttached(
                "DocumentXaml",
                typeof(string),
                typeof(RichTextBoxHelper),
                new FrameworkPropertyMetadata
                {
                    AffectsRender = true,
                    DefaultValue = String.Empty,
                    BindsTwoWayByDefault = true,
                    PropertyChangedCallback = (obj, e) =>
                    {
                        if (_recursionProtection.Contains(Thread.CurrentThread))
                        {
                            return;
                        }

                        var richTextBox = (RichTextBox)obj;

                        try
                        {
                            var parser = new ParserContext();
                            parser.XmlnsDictionary.Add(String.Empty, "http://schemas.microsoft.com/winfx/2006/xaml/presentation");
                            parser.XmlnsDictionary.Add("x", "http://schemas.microsoft.com/winfx/2006/xaml");
                            var doc = new FlowDocument();

                            var xaml = GetDocumentXaml(richTextBox);

                            if (!xaml.Contains("</"))
                            {
                                xaml = $"<Paragraph>{xaml}</Paragraph>";
                            }

                            using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(xaml)))
                            {
                                var block = (Block)XamlReader.Load(stream, parser);
                                doc.Blocks.Add(block);

                                richTextBox.Document = doc;
                            }
                        }
                        catch (Exception ex)
                        {
                            var doc = new FlowDocument();
                            var paragraph = new Paragraph();
                            doc.Blocks.Add(paragraph);
                            paragraph.Inlines.Add(ex.Message);

                            richTextBox.Document = doc;
                        }

                        // When the document changes update the source
                        richTextBox.TextChanged += (obj2, e2) =>
                        {
                            if (obj2 is RichTextBox richTextBox2)
                            {
                                SetDocumentXaml(richTextBox, XamlWriter.Save(richTextBox2.Document));
                            }
                        };
                    }
                });
    }
}