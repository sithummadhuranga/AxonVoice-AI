using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;

namespace AxonVoiceAI.KnowledgeBase.Ingestion;

public sealed class PdfExtractor
{
    public string ExtractText(Stream pdfStream)
    {
        using var document = PdfDocument.Open(pdfStream);
        var builder = new System.Text.StringBuilder();

        foreach (var page in document.GetPages())
        {
            var pageText = string.Join(" ", page.GetWords().Select(w => w.Text));
            if (!string.IsNullOrWhiteSpace(pageText))
            {
                builder.AppendLine(pageText);
                builder.AppendLine();
            }
        }

        return builder.ToString();
    }
}
