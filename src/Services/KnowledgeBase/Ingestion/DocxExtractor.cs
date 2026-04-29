using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace AxonVoiceAI.KnowledgeBase.Ingestion;

public sealed class DocxExtractor
{
    public string ExtractText(Stream docxStream)
    {
        using var package = WordprocessingDocument.Open(docxStream, isEditable: false);
        var mainPart = package.MainDocumentPart;
        if (mainPart is null)
            return string.Empty;

        // SAX-style reading via OpenXmlReader: streams XML tokens without materialising
        // the full DOM. The DOM approach (body.Elements<Paragraph>()) loads the entire
        // document graph into memory and triggers severe GC pressure on large DOCX files,
        // saturating the thread pool and preventing the ingestion timeout from firing.
        var builder = new System.Text.StringBuilder();
        using var reader = OpenXmlReader.Create(mainPart);

        while (reader.Read())
        {
            if (reader.ElementType == typeof(Text))
            {
                // Deserialise only this single leaf node — cheap.
                var text = ((Text)reader.LoadCurrentElement()!).Text;
                if (!string.IsNullOrEmpty(text))
                    builder.Append(text);
            }
            else if (reader.ElementType == typeof(Paragraph) && !reader.IsStartElement)
            {
                // End of paragraph — emit newline.
                builder.AppendLine();
            }
        }

        return builder.ToString().Trim();
    }
}
