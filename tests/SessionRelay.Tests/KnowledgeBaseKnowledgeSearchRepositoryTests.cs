using System.Net;
using System.Net.Http;
using System.Text;
using AxonVoiceAI.SessionRelay.Repositories;
using AxonVoiceAI.Shared.DTOs;
using FluentAssertions;

namespace AxonVoiceAI.SessionRelay.Tests;

public sealed class KnowledgeBaseKnowledgeSearchRepositoryTests
{
    [Fact]
    public async Task SearchKnowledgeAsync_KnowledgeBaseRespondsWithMatches_ReturnsParsedChunks()
    {
        var tenantId = Guid.NewGuid();
        var agentId = Guid.NewGuid();
        var handler = new StubHttpMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """
                    [{"text":"Chicken kottu costs LKR 1800.","sourceFilename":"menu.pdf","chunkIndex":4,"score":0.94}]
                    """,
                    Encoding.UTF8,
                    "application/json")
            });
        var repository = new KnowledgeBaseKnowledgeSearchRepository(new StubHttpClientFactory(handler));

        var result = await repository.SearchKnowledgeAsync(tenantId, agentId, "chicken kottu price", CancellationToken.None);

        result.Should().BeEquivalentTo(
            [new KnowledgeChunkDto("Chicken kottu costs LKR 1800.", "menu.pdf", 4, 0.94f)]);
        handler.LastRequest!.Method.Should().Be(HttpMethod.Get);
        handler.LastRequest.RequestUri!.ToString().Should().Contain($"/internal/agents/{agentId:D}/knowledge/context");
        handler.LastRequest.RequestUri!.Query.Should().Contain($"tenantId={tenantId:D}");
        handler.LastRequest.RequestUri!.Query.Should().Contain("query=chicken%20kottu%20price");
    }

    private sealed class StubHttpClientFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler)
        {
            BaseAddress = new Uri("http://knowledge-base:8080")
        };
    }

    private sealed class StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _responder = responder;

        public HttpRequestMessage? LastRequest { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            return Task.FromResult(_responder(request));
        }
    }
}