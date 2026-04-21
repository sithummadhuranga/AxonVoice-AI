using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace AxonVoiceAI.KnowledgeBase.Ingestion;

public sealed class DocxExtractor
{
    public string ExtractText(Stream docxStream)
    {
        using var document = WordprocessingDocument.Open(docxStream, isEditable: false);
        var body = document.MainDocumentPart?.Document?.Body;

        if (body is null)
            return string.Empty;

        var builder = new System.Text.StringBuilder();

        foreach (var para in body.Elements<Paragraph>())
        {
            var text = para.InnerText;
            if (!string.IsNullOrWhiteSpace(text))
            {
                builder.AppendLine(text);
            }
        }

        return builder.ToString();
    }
}
