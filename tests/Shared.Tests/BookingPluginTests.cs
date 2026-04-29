using AxonVoiceAI.Shared.Contracts;
using AxonVoiceAI.Shared.DTOs;
using AxonVoiceAI.Shared.SemanticKernel.Plugins;
using FluentAssertions;
using Moq;

namespace AxonVoiceAI.Shared.Tests;

public sealed class BookingPluginTests
{
    [Fact]
    public async Task CreatePendingBookingAsync_ValidContext_PassesTenantScopedRequestToRepository()
    {
        var tenantId = Guid.NewGuid();
        var agentId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var expectedResult = new PendingBookingCreationResult(
            Guid.NewGuid(),
            "ZXCV1234",
            new DateTimeOffset(2026, 5, 2, 18, 0, 0, TimeSpan.Zero));
        var repository = new Mock<IBookingRepository>();
        CreatePendingBookingRequest? capturedRequest = null;
        repository
            .Setup(repo => repo.CreatePendingBookingAsync(It.IsAny<CreatePendingBookingRequest>(), It.IsAny<CancellationToken>()))
            .Callback<CreatePendingBookingRequest, CancellationToken>((request, _) => capturedRequest = request)
            .ReturnsAsync(expectedResult);
        var plugin = new BookingPlugin(repository.Object);

        var result = await plugin.CreatePendingBookingAsync(
            customerName: "Ada Lovelace",
            customerPhone: "+94110000000",
            date: "2026-05-02",
            time: "19:00",
            partySize: 4,
            agentId: agentId.ToString(),
            tenantId: tenantId.ToString(),
            sessionId: sessionId.ToString(),
            detectedLanguage: "en",
            specialRequests: "Window seat",
            cancellationToken: CancellationToken.None);

        result.Should().BeEquivalentTo(new BookingResult(expectedResult.BookingId, expectedResult.ConfirmationCode, expectedResult.ExpiresAt));
        capturedRequest.Should().NotBeNull();
        capturedRequest!.TenantId.Should().Be(tenantId);
        capturedRequest.AgentId.Should().Be(agentId);
        capturedRequest.SessionId.Should().Be(sessionId);
        capturedRequest.CustomerName.Should().Be("Ada Lovelace");
        capturedRequest.CustomerPhone.Should().Be("+94110000000");
        capturedRequest.PartySize.Should().Be(4);
        capturedRequest.DetectedLanguage.Should().Be("en");
        capturedRequest.SpecialRequests.Should().Be("Window seat");
    }
}