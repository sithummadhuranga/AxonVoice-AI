using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace AxonVoiceAI.KnowledgeBase.Ingestion;

public sealed class PdfExtractor
{
    // pdftotext from poppler-utils runs as a child process so its memory consumption
    // is completely isolated from the .NET managed heap. This prevents the GC storm
    // that occurs with PdfPig on PDFs containing large embedded images or complex
    // content streams, where the managed allocations trigger continuous gen2 collections
    // that suspend the timer thread and break Task.Delay-based timeouts.
    //
    // The process-level WaitForExit timeout is a safety net; DocumentIngestionJob also
    // enforces a 30-second wall-clock limit via Task.WhenAny at the caller.

    private const int ProcessTimeoutMs = 45_000;

    private readonly ILogger<PdfExtractor> _logger;

    public PdfExtractor(ILogger<PdfExtractor> logger)
    {
        _logger = logger;
    }

    public string ExtractText(Stream pdfStream)
    {
        var tempPdf = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.pdf");
        try
        {
            using (var file = File.Create(tempPdf))
                pdfStream.CopyTo(file);

            _logger.LogInformation(
                "Starting external pdftotext extraction for temporary file {TempPdf}.",
                tempPdf);

            using var process = new Process();
            process.StartInfo = new ProcessStartInfo
            {
                FileName = "pdftotext",
                // -enc UTF-8: emit UTF-8 text
                // -nopgbrk: omit form-feed characters between pages
                // --: end of options, then input path and "-" for stdout
                ArgumentList = { "-enc", "UTF-8", "-nopgbrk", "--", tempPdf, "-" },
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            process.Start();

            _logger.LogInformation(
                "Started pdftotext process {ProcessId} for {TempPdf}.",
                process.Id,
                tempPdf);

            // Read stdout and stderr concurrently to prevent deadlock when either buffer fills.
            var stdoutTask = process.StandardOutput.ReadToEndAsync();
            var stderrTask = process.StandardError.ReadToEndAsync();

            if (!process.WaitForExit(ProcessTimeoutMs))
            {
                process.Kill(entireProcessTree: true);
                throw new TimeoutException(
                    $"pdftotext did not complete within {ProcessTimeoutMs / 1_000} seconds. " +
                    "The PDF may be corrupt, encrypted, or image-only.");
            }

            // Ensure async reads have drained after WaitForExit.
            var output = stdoutTask.GetAwaiter().GetResult();
            var stderr = stderrTask.GetAwaiter().GetResult();

            if (process.ExitCode != 0)
            {
                _logger.LogWarning(
                    "pdftotext exited with code {ExitCode}: {Stderr}",
                    process.ExitCode,
                    stderr.Trim());
            }

            _logger.LogInformation(
                "pdftotext completed for {TempPdf} with exit code {ExitCode} and {CharacterCount} extracted characters.",
                tempPdf,
                process.ExitCode,
                output.Length);

            return output;
        }
        finally
        {
            if (File.Exists(tempPdf))
                File.Delete(tempPdf);
        }
    }
}
