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
        var geminiClient = new RecordingGeminiLiveClient();

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
        responses[0].ResponseJson.Should().Contain(sessionId.ToString());
        responses[0].ResponseJson.Should().Contain("Ada Lovelace");
        responses[0].ResponseJson.Should().Contain("Window seat");
        responses[0].ResponseJson.Should().Contain("\"DetectedLanguage\":\"si\"");
        geminiClient.LastInstruction.Should().Contain("හොඳයි, මමත් පරීක්ෂා කරලා බලන්නම්");
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
        var geminiClient = new RecordingGeminiLiveClient();

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
    public async Task HandleAsync_AcknowledgmentStartsBeforePluginCompletes()
    {
        var geminiClient = new RecordingGeminiLiveClient();
        var kernelBuilder = Kernel.CreateBuilder();
        kernelBuilder.Plugins.AddFromObject(new WaitForAcknowledgmentPlugin(geminiClient));
        var kernel = kernelBuilder.Build();

        var interceptor = new FunctionCallInterceptor(
            kernel,
            "en",
            Guid.NewGuid().ToString(),
            Guid.NewGuid().ToString(),
            Guid.NewGuid().ToString(),
            NullLogger<FunctionCallInterceptor>.Instance);

        var responses = await interceptor.HandleAsync(
            [new GeminiFunctionCall
            {
                Id = "call-3",
                Name = "wait_for_ack"
            }],
            geminiClient,
            CancellationToken.None);

        responses.Should().ContainSingle();
        responses[0].ResponseJson.Should().Contain("ack-observed");
    }

    [Fact]
    public async Task SendAcknowledgmentAsync_UsesLocalizedInstruction()
    {
        var interceptor = new FunctionCallInterceptor(
            Kernel.CreateBuilder().Build(),
            "ta",
            Guid.NewGuid().ToString(),
            Guid.NewGuid().ToString(),
            Guid.NewGuid().ToString(),
            NullLogger<FunctionCallInterceptor>.Instance);
        var geminiClient = new RecordingGeminiLiveClient();

        await interceptor.SendAcknowledgmentAsync(geminiClient, CancellationToken.None);

        geminiClient.LastInstruction.Should().Be(
            "Speak this exact acknowledgment to the caller and add nothing else: \"சரி, நான் சரிபார்க்கிறேன்\"");
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
                sessionId,
                detectedLanguage,
                specialRequests));
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

        private sealed class WaitForAcknowledgmentPlugin(RecordingGeminiLiveClient geminiClient)
        {
            [KernelFunction("wait_for_ack")]
            public async Task<string> WaitForAckAsync(CancellationToken cancellationToken = default)
            {
                await geminiClient.WaitForAcknowledgmentAsync(TimeSpan.FromSeconds(1), cancellationToken);
                return "ack-observed";
            }
        }

        private sealed class RecordingGeminiLiveClient : IGeminiLiveClient
        {
            private readonly TaskCompletionSource _acknowledgmentObserved = new(TaskCreationOptions.RunContinuationsAsynchronously);

            public string? LastInstruction { get; private set; }

            public Task SendClientContentTextTurnAsync(string text, CancellationToken ct)
            {
                ct.ThrowIfCancellationRequested();
                LastInstruction = text;
                _acknowledgmentObserved.TrySetResult();
                return Task.CompletedTask;
            }

            public Task WaitForAcknowledgmentAsync(TimeSpan timeout, CancellationToken ct)
            {
                return _acknowledgmentObserved.Task.WaitAsync(timeout, ct);
            }
        }

    private sealed record BookingEchoResult(
        string CustomerName,
        string CustomerPhone,
        string Date,
        string Time,
        int PartySize,
        string AgentId,
        string SessionId,
        string DetectedLanguage,
        string? SpecialRequests);
}