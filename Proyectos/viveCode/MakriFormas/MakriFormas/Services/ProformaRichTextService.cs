using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;

namespace MakriFormas.Services
{
    public static class ProformaRichTextService
    {
        private const string RichTextPrefix = "xamlrt:";

        public static string Serialize(RichTextBox richTextBox)
        {
            var range = new TextRange(richTextBox.Document.ContentStart, richTextBox.Document.ContentEnd);
            if (string.IsNullOrWhiteSpace(range.Text))
                return string.Empty;

            using var stream = new MemoryStream();
            range.Save(stream, DataFormats.Xaml);
            return RichTextPrefix + Convert.ToBase64String(stream.ToArray());
        }

        public static void Deserialize(RichTextBox richTextBox, string? storedValue)
        {
            richTextBox.Document = new FlowDocument();

            if (string.IsNullOrWhiteSpace(storedValue))
                return;

            if (TryDeserializeToDocument(storedValue, richTextBox.Document))
                return;

            richTextBox.Document.Blocks.Add(new Paragraph(new Run(storedValue.Trim())));
        }

        public static string ToPlainText(string? storedValue)
        {
            if (string.IsNullOrWhiteSpace(storedValue))
                return string.Empty;

            var tempDoc = new FlowDocument();
            if (!TryDeserializeToDocument(storedValue, tempDoc))
                return storedValue.Trim();

            var range = new TextRange(tempDoc.ContentStart, tempDoc.ContentEnd);
            return range.Text.Trim();
        }

        private static bool TryDeserializeToDocument(string value, FlowDocument document)
        {
            if (!value.StartsWith(RichTextPrefix, StringComparison.Ordinal))
                return false;

            var encoded = value[RichTextPrefix.Length..];
            if (string.IsNullOrWhiteSpace(encoded))
                return false;

            try
            {
                var bytes = Convert.FromBase64String(encoded);
                using var stream = new MemoryStream(bytes);
                var range = new TextRange(document.ContentStart, document.ContentEnd);
                range.Load(stream, DataFormats.Xaml);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
