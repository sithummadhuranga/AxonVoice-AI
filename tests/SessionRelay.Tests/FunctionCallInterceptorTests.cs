using System.Text.Json;
using AxonVoiceAI.SessionRelay.Gemini;
using AxonVoiceAI.SessionRelay.Handlers;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.SemanticKernel;

namespace AxonVoiceAI.SessionRelay.Tests;

public sealed class FunctionCallInterceptorTests
{
    [Fact]
    public async Task HandleAsync_CreatePendingBookingCallUsesAliasedAndInjectedArguments()
    {
        var agentId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        using var argsDocument = JsonDocument.Parse(
            """
            {
              "customer_name": "Ada Lovelace",
              "contact_number": "+94110000000",
              "date": "2026-04-21",
              "time": "18:30",
              "party_size": 4,
              "notes": "Window seat"
            }
            """);

        var kernelBuilder = Kernel.CreateBuilder();
        kernelBuilder.Plugins.AddFromObject(new BookingEchoPlugin());
        var kernel = kernelBuilder.Build();

        var interceptor = new FunctionCallInterceptor(
            kernel,
            "si",
            Guid.NewGuid().ToString(),
            agentId.ToString(),
            sessionId.ToString(),
            NullLogger<FunctionCallInterceptor>.Instance);
        var geminiClient = new StubGeminiLiveClient();

        var responses = await interceptor.HandleAsync(
            [new GeminiFunctionCall
            {
                Id = "call-1",
                Name = "create_pending_booking",
                Args = argsDocument.RootElement.Clone()
            }],
            geminiClient,
            CancellationToken.None);

        responses.Should().ContainSingle();
        responses[0].ResponseJson.Should().Contain(agentId.ToString());
        responses[0].ResponseJson.Should().Contain("\"TenantId\":\"");
        responses[0].ResponseJson.Should().Contain(sessionId.ToString());
        responses[0].ResponseJson.Should().Contain("Ada Lovelace");
        responses[0].ResponseJson.Should().Contain("Window seat");
        responses[0].ResponseJson.Should().Contain("\"DetectedLanguage\":\"si\"");
    }

    [Fact]
    public async Task HandleAsync_CancelledFunctionCall_DoesNotReturnResponse()
    {
        var kernelBuilder = Kernel.CreateBuilder();
        kernelBuilder.Plugins.AddFromObject(new CancellablePlugin());
        var kernel = kernelBuilder.Build();

        var interceptor = new FunctionCallInterceptor(
            kernel,
            "en",
            Guid.NewGuid().ToString(),
            Guid.NewGuid().ToString(),
            Guid.NewGuid().ToString(),
            NullLogger<FunctionCallInterceptor>.Instance);
        var geminiClient = new StubGeminiLiveClient();

        using var cancellationTokenSource = new CancellationTokenSource();
        await cancellationTokenSource.CancelAsync();

        var responses = await interceptor.HandleAsync(
            [new GeminiFunctionCall
            {
                Id = "call-2",
                Name = "cancellable_function"
            }],
            geminiClient,
            cancellationTokenSource.Token);

        responses.Should().BeEmpty();
    }

    [Fact]
    public async Task HandleAsync_MultipleCalls_AllResponsesReturned()
    {
        var kernelBuilder = Kernel.CreateBuilder();
        kernelBuilder.Plugins.AddFromObject(new KnowledgeEchoPlugin());
        var kernel = kernelBuilder.Build();

        using var args1 = JsonDocument.Parse("""{"query": "opening hours"}""");
        using var args2 = JsonDocument.Parse("""{"query": "parking"}""");

        var interceptor = new FunctionCallInterceptor(
            kernel,
            "en",
            Guid.NewGuid().ToString(),
            Guid.NewGuid().ToString(),
            Guid.NewGuid().ToString(),
            NullLogger<FunctionCallInterceptor>.Instance);
        var geminiClient = new StubGeminiLiveClient();

        var responses = await interceptor.HandleAsync(
            [
                new GeminiFunctionCall { Id = "call-a", Name = "search_knowledge_base", Args = args1.RootElement.Clone() },
                new GeminiFunctionCall { Id = "call-b", Name = "search_knowledge_base", Args = args2.RootElement.Clone() }
            ],
            geminiClient,
            CancellationToken.None);

        responses.Should().HaveCount(2);
        responses.Select(r => r.ResponseJson).Should().Contain(r => r.Contains("opening hours"));
        responses.Select(r => r.ResponseJson).Should().Contain(r => r.Contains("parking"));
    }

    [Fact]
    public async Task HandleAsync_SearchKnowledgeCallInjectsTenantAndAgentIds()
    {
        var tenantId = Guid.NewGuid();
        var agentId = Guid.NewGuid();
        using var argsDocument = JsonDocument.Parse(
            """
            {
              "query": "menu prices"
            }
            """);

        var kernelBuilder = Kernel.CreateBuilder();
        kernelBuilder.Plugins.AddFromObject(new KnowledgeEchoPlugin());
        var kernel = kernelBuilder.Build();

        var interceptor = new FunctionCallInterceptor(
            kernel,
            "en",
            tenantId.ToString(),
            agentId.ToString(),
            Guid.NewGuid().ToString(),
            NullLogger<FunctionCallInterceptor>.Instance);
        var geminiClient = new StubGeminiLiveClient();

        var responses = await interceptor.HandleAsync(
            [new GeminiFunctionCall
            {
                Id = "call-knowledge",
                Name = "search_knowledge_base",
                Args = argsDocument.RootElement.Clone()
            }],
            geminiClient,
            CancellationToken.None);

        responses.Should().ContainSingle();
        responses[0].ResponseJson.Should().Contain("menu prices");
        responses[0].ResponseJson.Should().Contain(agentId.ToString());
        responses[0].ResponseJson.Should().Contain(tenantId.ToString());
    }

    [Fact]
    public async Task SendAcknowledgmentAsync_IsNoOp_DoesNotCallGeminiClient()
    {
        // Acknowledgment is now fully model-driven via the system prompt.
        // The method must complete without error and without calling any client method.
        var interceptor = new FunctionCallInterceptor(
            Kernel.CreateBuilder().Build(),
            "ta",
            Guid.NewGuid().ToString(),
            Guid.NewGuid().ToString(),
            Guid.NewGuid().ToString(),
            NullLogger<FunctionCallInterceptor>.Instance);
        var geminiClient = new StubGeminiLiveClient();

        var act = () => interceptor.SendAcknowledgmentAsync(geminiClient, CancellationToken.None);

        await act.Should().NotThrowAsync();
    }

    private sealed class BookingEchoPlugin
    {
        [KernelFunction("create_pending_booking")]
        public Task<BookingEchoResult> CreatePendingBookingAsync(
            string customerName,
            string customerPhone,
            string date,
            string time,
            int partySize,
            string agentId,
            string tenantId,
            string sessionId,
            string detectedLanguage,
            string? specialRequests = null,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new BookingEchoResult(
                customerName,
                customerPhone,
                date,
                time,
                partySize,
                agentId,
                tenantId,
                sessionId,
                detectedLanguage,
                specialRequests));
        }
    }

    private sealed class KnowledgeEchoPlugin
    {
        [KernelFunction("search_knowledge_base")]
        public Task<KnowledgeEchoResult> SearchKnowledgeAsync(
            string query,
            string agentId,
            string tenantId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new KnowledgeEchoResult(query, agentId, tenantId));
        }
    }

    private sealed class CancellablePlugin
    {
        [KernelFunction("cancellable_function")]
        public Task<string> InvokeAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult("should-not-complete");
        }
    }

    /// <summary>
    /// IGeminiLiveClient is now an empty interface; this stub satisfies the type requirement
    /// without needing to implement any methods.
    /// </summary>
    private sealed class StubGeminiLiveClient : IGeminiLiveClient { }

    private sealed record BookingEchoResult(
        string CustomerName,
        string CustomerPhone,
        string Date,
        string Time,
        int PartySize,
        string AgentId,
        string TenantId,
        string SessionId,
        string DetectedLanguage,
        string? SpecialRequests);

    private sealed record KnowledgeEchoResult(
        string Query,
        string AgentId,
        string TenantId);
}
