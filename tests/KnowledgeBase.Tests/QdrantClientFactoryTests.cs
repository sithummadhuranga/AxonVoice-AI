using AxonVoiceAI.KnowledgeBase.Providers;
using FluentAssertions;

namespace AxonVoiceAI.KnowledgeBase.Tests;

public sealed class QdrantClientFactoryTests
{
    [Fact]
    public void ResolveEndpoint_RestPortConfigured_UsesGrpcPort()
    {
        var endpoint = QdrantClientFactory.ResolveEndpoint("http://qdrant:6333");

        endpoint.Host.Should().Be("qdrant");
        endpoint.Port.Should().Be(6334);
        endpoint.UseHttps.Should().BeFalse();
    }

    [Fact]
    public void ResolveEndpoint_GrpcPortConfigured_KeepsConfiguredPort()
    {
        var endpoint = QdrantClientFactory.ResolveEndpoint("http://qdrant:6334");

        endpoint.Host.Should().Be("qdrant");
        endpoint.Port.Should().Be(6334);
        endpoint.UseHttps.Should().BeFalse();
    }

    [Fact]
    public void ResolveEndpoint_HttpsConfigured_PreservesTransportSecurity()
    {
        var endpoint = QdrantClientFactory.ResolveEndpoint("https://vector.example.com:6334");

        endpoint.Host.Should().Be("vector.example.com");
        endpoint.Port.Should().Be(6334);
        endpoint.UseHttps.Should().BeTrue();
    }
}
