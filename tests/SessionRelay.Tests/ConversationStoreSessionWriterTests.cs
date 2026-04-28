using System.Net;
using System.Net.Http;
using System.Text;
using AxonVoiceAI.SessionRelay.ConversationStore;
using AxonVoiceAI.SessionRelay.Handlers;
using AxonVoiceAI.Shared.Contracts;
using AxonVoiceAI.Shared.DTOs;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace AxonVoiceAI.SessionRelay.Tests;

public sealed class ConversationStoreSessionWriterTests
{
    [Fact]
    public async Task RecordSessionStartAsync_WritesInternalStartPayload()
    {
        var context = new SessionStartContextDto(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "en");
        var handler = new RecordingHttpMessageHandler();
        var writer = CreateWriter(handler);

        await writer.RecordSessionStartAsync(context, ChannelType.WebSocket, "en", CancellationToken.None);

        handler.Requests.Should().ContainSingle();
        handler.Requests[0].Method.Should().Be(HttpMethod.Post);
        handler.Requests[0].RequestUri!.ToString().Should().EndWith("/internal/sessions");
        var payload = await handler.Requests[0].Content!.ReadAsStringAsync();
        payload.Should().Contain(context.SessionId.ToString());
        payload.Should().Contain("WebSocket");
    }

    [Fact]
    public async Task RecordFunctionCallsAsync_WritesOneRequestPerFunctionCall()
    {
        var context = new SessionStartContextDto(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "en");
        var handler = new RecordingHttpMessageHandler();
        var writer = CreateWriter(handler);

        await writer.RecordFunctionCallsAsync(
            context,
            [new FunctionCallExecutionResult(
                "check_availability",
                "{\"party_size\":4}",
                "{\"available\":true}",
                true,
                null,
                27,
                new GeminiFunctionResponse("call-1", "check_availability", "{\"available\":true}"))],
            CancellationToken.None);

        handler.Requests.Should().ContainSingle();
        handler.Requests[0].RequestUri!.ToString().Should().EndWith($"/internal/sessions/{context.SessionId:D}/function-calls");
        var payload = await handler.Requests[0].Content!.ReadAsStringAsync();
        payload.Should().Contain(context.TenantId.ToString());
        payload.Should().Contain("check_availability");
        payload.Should().Contain("27");
    }

    [Fact]
    public async Task CloseSessionAsync_WritesInternalClosePayload()
    {
        var context = new SessionStartContextDto(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "en");
        var handler = new RecordingHttpMessageHandler();
        var writer = CreateWriter(handler);

        await writer.CloseSessionAsync(context, 61, CancellationToken.None);

        handler.Requests.Should().ContainSingle();
        handler.Requests[0].RequestUri!.ToString().Should().EndWith($"/internal/sessions/{context.SessionId:D}/close");
        var payload = await handler.Requests[0].Content!.ReadAsStringAsync();
        payload.Should().Contain(context.TenantId.ToString());
        payload.Should().Contain("61");
    }

    private static ConversationStoreSessionWriter CreateWriter(RecordingHttpMessageHandler handler)
    {
        return new ConversationStoreSessionWriter(
            new StubHttpClientFactory(handler),
            NullLogger<ConversationStoreSessionWriter>.Instance);
    }

    private sealed class StubHttpClientFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler)
        {
            BaseAddress = new Uri("http://conversation-store:8080")
        };
    }

    private sealed class RecordingHttpMessageHandler : HttpMessageHandler
    {
        public List<HttpRequestMessage> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(string.Empty, Encoding.UTF8, "application/json")
            });
        }
    }
}