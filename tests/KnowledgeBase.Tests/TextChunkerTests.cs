using AxonVoiceAI.KnowledgeBase.Ingestion;
using FluentAssertions;

namespace AxonVoiceAI.KnowledgeBase.Tests;

public sealed class TextChunkerTests
{
    [Fact]
    public void Chunk_TextFitsTrailingOverlapWindow_ReturnsFinalChunkAndStops()
    {
        var text = new string('a', 1_963);

        var chunks = TextChunker.Chunk(text);

        chunks.Should().HaveCount(2);
        chunks[0].Should().HaveLength(1_600);
        chunks[1].Should().HaveLength(523);
    }

    [Fact]
    public void Chunk_TextNeedsOverlap_AdvancesToFinalChunkWithoutLooping()
    {
        var text = string.Join(' ', Enumerable.Repeat("sentence", 500));

        var chunks = TextChunker.Chunk(text);

        chunks.Should().HaveCountGreaterThan(1);
        chunks.Should().OnlyContain(chunk => chunk.Length >= 200);
    }
}