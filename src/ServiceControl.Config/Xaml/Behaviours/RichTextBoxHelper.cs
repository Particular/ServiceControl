using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Markup;

namespace ServiceControl.Config.Xaml.Behaviours
{
    public class RichTextBoxHelper : DependencyObject
    {
        private static HashSet<Thread> _recursionProtection = new HashSet<Thread>();

        public static string GetDocumentXaml(DependencyObject obj)
        {
            return (string)obj.GetValue(DocumentXamlProperty);
        }

        public static void SetDocumentXaml(DependencyObject obj, string value)
        {
            obj.SetValue(DocumentXamlProperty, value);
        }

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
                        return;

                    var richTextBox = (RichTextBox)obj;

                    try
                    {
                        var parser = new ParserContext();
                        parser.XmlnsDictionary.Add(String.Empty, "http://schemas.microsoft.com/winfx/2006/xaml/presentation");
                        parser.XmlnsDictionary.Add("x", "http://schemas.microsoft.com/winfx/2006/xaml");
                        var doc = new FlowDocument();

                        using (var stream = new MemoryStream(Encoding.UTF8.GetBytes("<Paragraph>" + GetDocumentXaml(richTextBox) + "</Paragraph>")))
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
                        RichTextBox richTextBox2 = obj2 as RichTextBox;
                        if (richTextBox2 != null)
                        {
                            SetDocumentXaml(richTextBox, XamlWriter.Save(richTextBox2.Document));
                        }
                    };
                }
            });
    }
}