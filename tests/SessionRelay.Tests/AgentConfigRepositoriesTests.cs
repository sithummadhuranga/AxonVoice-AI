using System.Net;
using System.Net.Http;
using System.Text;
using AxonVoiceAI.SessionRelay.Repositories;
using AxonVoiceAI.Shared.Contracts;
using FluentAssertions;

namespace AxonVoiceAI.SessionRelay.Tests;

public sealed class AgentConfigRepositoriesTests
{
    [Fact]
    public async Task QueryAvailabilityAsync_AgentConfigRespondsWithAvailability_ReturnsParsedResult()
    {
        var agentId = Guid.NewGuid();
        var requestedDatetime = new DateTimeOffset(2026, 5, 2, 19, 0, 0, TimeSpan.Zero);
        var handler = new StubHttpMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """
                    {"available":true,"remainingCapacity":11,"closestAlternatives":[]}
                    """,
                    Encoding.UTF8,
                    "application/json")
            });
        var repository = new AgentConfigAvailabilityRepository(new StubHttpClientFactory(handler));

        var result = await repository.QueryAvailabilityAsync(agentId, requestedDatetime, 3, CancellationToken.None);

        result.Should().BeEquivalentTo(new AvailabilityQueryResult(true, 11, []));
        handler.LastRequest!.Method.Should().Be(HttpMethod.Get);
        handler.LastRequest.RequestUri!.ToString().Should().Contain($"/internal/agents/{agentId:D}/tools/availability");
        handler.LastRequest.RequestUri!.Query.Should().Contain("partySize=3");
    }

    [Fact]
    public async Task CreatePendingBookingAsync_AgentConfigRespondsWithPendingBooking_ReturnsParsedResult()
    {
        var request = new CreatePendingBookingRequest(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Ada Lovelace",
            "+94110000000",
            new DateTimeOffset(2026, 5, 2, 19, 0, 0, TimeSpan.Zero),
            4,
            "en",
            "Window seat");
        var expected = new PendingBookingCreationResult(Guid.NewGuid(), "ZXCV1234", new DateTimeOffset(2026, 5, 2, 18, 0, 0, TimeSpan.Zero));
        var handler = new StubHttpMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    $$"""
                    {"bookingId":"{{expected.BookingId}}","confirmationCode":"{{expected.ConfirmationCode}}","expiresAt":"{{expected.ExpiresAt:O}}"}
                    """,
                    Encoding.UTF8,
                    "application/json")
            });
        var repository = new AgentConfigBookingRepository(new StubHttpClientFactory(handler));

        var result = await repository.CreatePendingBookingAsync(request, CancellationToken.None);

        result.Should().BeEquivalentTo(expected);
        handler.LastRequest!.Method.Should().Be(HttpMethod.Post);
        handler.LastRequest.RequestUri!.ToString().Should().Contain($"/internal/agents/{request.AgentId:D}/tools/pending-bookings");
    }

    private sealed class StubHttpClientFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler)
        {
            BaseAddress = new Uri("http://agent-config:8080")
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