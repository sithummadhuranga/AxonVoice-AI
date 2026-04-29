using AxonVoiceAI.SessionRelay.Prompts;
using FluentAssertions;

namespace AxonVoiceAI.SessionRelay.Tests;

public sealed class KnowledgeContextQueryBuilderTests
{
    [Fact]
    public void BuildDefault_UnknownLanguageIncludesCoreTermsAndFallbackLanguages()
    {
        var query = KnowledgeContextQueryBuilder.BuildDefault(string.Empty);

        query.Should().Contain("products services pricing");
        query.Should().Contain("reservations");
        query.Should().Contain("appointments");
        query.Should().Contain("tickets");
        query.Should().Contain("availability");
        query.Should().Contain("sinhala tamil english");
    }

    [Fact]
    public void BuildDefault_KnownLanguageAddsLanguageHint()
    {
        var query = KnowledgeContextQueryBuilder.BuildDefault("ta");

        query.Should().Contain("tamil");
        query.Should().NotContain("sinhala tamil english");
    }
}