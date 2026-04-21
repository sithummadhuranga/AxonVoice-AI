using AxonVoiceAI.ConversationStore.Data;
using AxonVoiceAI.ConversationStore.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AxonVoiceAI.ConversationStore.Services;

/// <summary>
/// Retries failed webhook deliveries using 3-attempt exponential backoff:
/// attempt 1 → 5s, attempt 2 → 30s, attempt 3 → 5min.
/// Deliveries that exhaust all attempts are marked "failed" permanently.
/// </summary>
public sealed class WebhookDispatcher : BackgroundService
{
    private static readonly TimeSpan[] RetryDelays =
    [
        TimeSpan.FromSeconds(5),
        TimeSpan.FromSeconds(30),
        TimeSpan.FromMinutes(5),
    ];

    private const int MaxAttempts = 3;
    private static readonly TimeSpan PollingInterval = TimeSpan.FromSeconds(10);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<WebhookDispatcher> _logger;

    public WebhookDispatcher(
        IServiceScopeFactory scopeFactory,
        IHttpClientFactory httpClientFactory,
        ILogger<WebhookDispatcher> logger)
    {
        _scopeFactory = scopeFactory;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await DeliverPendingWebhooksAsync(stoppingToken);
            await Task.Delay(PollingInterval, stoppingToken);
        }
    }

    private async Task DeliverPendingWebhooksAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ConversationDbContext>();

        var due = await db.WebhookDeliveries
            .Where(w => (w.Status == "pending" || w.Status == "retrying")
                && (w.NextRetryAt == null || w.NextRetryAt <= DateTimeOffset.UtcNow))
            .OrderBy(w => w.CreatedAt)
            .Take(50)
            .ToListAsync(ct);

        foreach (var delivery in due)
        {
            await AttemptDeliveryAsync(delivery, db, ct);
        }
    }

    private async Task AttemptDeliveryAsync(WebhookDelivery delivery, ConversationDbContext db, CancellationToken ct)
    {
        delivery.AttemptCount++;

        try
        {
            var client = _httpClientFactory.CreateClient("webhook");
            var content = new StringContent(
                delivery.PayloadJson,
                System.Text.Encoding.UTF8,
                "application/json");

            var response = await client.PostAsync(delivery.WebhookUrl, content, ct);
            delivery.LastHttpStatus = (int)response.StatusCode;

            if (response.IsSuccessStatusCode)
            {
                delivery.Status = "delivered";
                delivery.DeliveredAt = DateTimeOffset.UtcNow;

                _logger.LogInformation(
                    "Webhook delivered to {Url} for session {SessionId} on attempt {Attempt}.",
                    delivery.WebhookUrl, delivery.SessionId, delivery.AttemptCount);
            }
            else
            {
                ScheduleRetryOrFail(delivery, $"HTTP {(int)response.StatusCode}");
            }
        }
        catch (Exception ex)
        {
            delivery.LastErrorMessage = ex.Message;
            ScheduleRetryOrFail(delivery, ex.Message);

            _logger.LogWarning(ex,
                "Webhook attempt {Attempt} failed for delivery {DeliveryId}.",
                delivery.AttemptCount, delivery.Id);
        }

        await db.SaveChangesAsync(ct);
    }

    private static void ScheduleRetryOrFail(WebhookDelivery delivery, string errorMessage)
    {
        delivery.LastErrorMessage = errorMessage;

        if (delivery.AttemptCount >= MaxAttempts)
        {
            delivery.Status = "failed";
        }
        else
        {
            var delay = RetryDelays[delivery.AttemptCount - 1];
            delivery.Status = "retrying";
            delivery.NextRetryAt = DateTimeOffset.UtcNow.Add(delay);
        }
    }
}
