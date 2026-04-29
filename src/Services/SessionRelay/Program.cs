using AxonVoiceAI.SessionRelay.Audio;
using AxonVoiceAI.SessionRelay.ConversationStore;
using AxonVoiceAI.SessionRelay.Handlers;
using AxonVoiceAI.SessionRelay.Repositories;
using AxonVoiceAI.Shared.Configuration;
using AxonVoiceAI.Shared.Contracts;
using AxonVoiceAI.Shared.SemanticKernel.Plugins;
using Microsoft.IdentityModel.Tokens;
using Microsoft.SemanticKernel;
using StackExchange.Redis;
using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Net.WebSockets;
using System.Security.Claims;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

var jwtSigningKey = builder.Configuration.GetRequiredValue(builder.Environment, "JWT_SIGNING_KEY");
var platformBaseUrl = builder.Configuration.GetRequiredValue(builder.Environment, "PLATFORM_BASE_URL");
var redisUrl = builder.Configuration.GetRequiredValue(builder.Environment, "REDIS_URL");
var agentConfigUrl = builder.Configuration.GetRequiredValue(builder.Environment, "AGENT_CONFIG_URL");
var knowledgeBaseUrl = builder.Configuration.GetRequiredValue(builder.Environment, "KNOWLEDGE_BASE_URL");
var conversationStoreUrl = builder.Configuration.GetRequiredValue(builder.Environment, "CONVERSATION_STORE_URL");

builder.Services.AddPlatformDataProtection(builder.Configuration, builder.Environment, "AxonVoiceAI.SessionRelay");
builder.Services.AddMemoryCache();

builder.Services.AddSingleton<IConnectionMultiplexer>(
    ConnectionMultiplexer.Connect(redisUrl));

builder.Services.AddHttpClient("agent-config", client =>
{
    client.BaseAddress = new Uri(agentConfigUrl);
    client.Timeout = TimeSpan.FromSeconds(10);
});

builder.Services.AddHttpClient("knowledge-base", client =>
{
    client.BaseAddress = new Uri(knowledgeBaseUrl);
    client.Timeout = TimeSpan.FromSeconds(10);
});

builder.Services.AddHttpClient("conversation-store", client =>
{
    client.BaseAddress = new Uri(conversationStoreUrl);
    client.Timeout = TimeSpan.FromSeconds(10);
});

builder.Services.AddSingleton<IAvailabilityRepository, AgentConfigAvailabilityRepository>();
builder.Services.AddSingleton<IBookingRepository, AgentConfigBookingRepository>();
builder.Services.AddSingleton<IOrderRepository, AgentConfigOrderRepository>();
builder.Services.AddSingleton<IKnowledgeSearchRepository, KnowledgeBaseKnowledgeSearchRepository>();
builder.Services.AddSingleton<ConversationStoreSessionWriter>();

// Register Semantic Kernel plugins for function call dispatch.
builder.Services.AddSingleton<KernelPluginCollection>(sp =>
{
    var availabilityRepo = sp.GetRequiredService<IAvailabilityRepository>();
    var bookingRepo = sp.GetRequiredService<IBookingRepository>();
    var orderRepo = sp.GetRequiredService<IOrderRepository>();
    var knowledgeSearchRepo = sp.GetRequiredService<IKnowledgeSearchRepository>();
    var collection = new KernelPluginCollection();
    collection.AddFromObject(new KnowledgeSearchPlugin(knowledgeSearchRepo));
    collection.AddFromObject(new AvailabilityPlugin(availabilityRepo));
    collection.AddFromObject(new BookingPlugin(bookingRepo));
    collection.AddFromObject(new OrderPlugin(orderRepo));
    return collection;
});

builder.Services.AddSingleton<SessionHandler>();

var app = builder.Build();

var sessionTokenValidationParameters = new TokenValidationParameters
{
    ValidateIssuerSigningKey = true,
    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSigningKey)),
    ValidateIssuer = true,
    ValidIssuer = platformBaseUrl,
    ValidateAudience = true,
    ValidAudience = "relay",
    ValidateLifetime = true,
    ClockSkew = TimeSpan.FromSeconds(30),
};

app.UseWebSockets(new WebSocketOptions { KeepAliveInterval = TimeSpan.FromSeconds(30) });

app.Map("/ws/session", async (HttpContext httpContext, SessionHandler sessionHandler, CancellationToken ct) =>
{
    if (!httpContext.WebSockets.IsWebSocketRequest)
    {
        httpContext.Response.StatusCode = 400;
        return;
    }

    var logger = httpContext.RequestServices
        .GetRequiredService<ILoggerFactory>()
        .CreateLogger("SessionRelay.SessionToken");

    var rawToken = httpContext.Request.Query["token"].FirstOrDefault();
    if (string.IsNullOrWhiteSpace(rawToken))
    {
        httpContext.Response.StatusCode = 401;
        return;
    }

    ClaimsPrincipal user;
    try
    {
        user = ValidateSessionToken(TrimJwtBoundaryNoise(rawToken), sessionTokenValidationParameters);
    }
    catch (SecurityTokenException ex)
    {
        logger.LogWarning(
            ex,
            "Session token validation failed for {Path}. Length={Length} DotCount={DotCount} InvalidCharacters={InvalidCharacters}",
            httpContext.Request.Path,
            rawToken.Length,
            rawToken.Count(character => character == '.'),
            string.Join(
                ",",
                rawToken
                    .Where(character => !IsJwtBoundaryCharacter(character))
                    .Distinct()
                    .Select(character => $"U+{(int)character:X4}:{GetUnicodeCategory(character)}")));
        httpContext.Response.StatusCode = 401;
        return;
    }

    var tenantId = Guid.Parse(user.FindFirst("tenant_id")!.Value);
    var agentId = Guid.Parse(user.FindFirst("agent_id")!.Value);
    var sessionId = Guid.Parse(user.FindFirst("session_id")!.Value);
    var sessionLanguage = user.FindFirst("language")?.Value ?? string.Empty;

    var webSocket = await httpContext.WebSockets.AcceptWebSocketAsync();
    var channel = new WebSocketAudioChannel(webSocket);

    var context = new AxonVoiceAI.Shared.DTOs.SessionStartContextDto(tenantId, agentId, sessionId, sessionLanguage);
    await sessionHandler.RunSessionAsync(context, channel, ct);
});

await app.RunAsync();

static string TrimJwtBoundaryNoise(string token)
{
    var start = 0;
    var end = token.Length;

    while (start < end && !IsJwtBoundaryCharacter(token[start]))
        start++;

    while (end > start && !IsJwtBoundaryCharacter(token[end - 1]))
        end--;

    return token[start..end];
}

static bool IsJwtBoundaryCharacter(char character)
{
    return char.IsAsciiLetterOrDigit(character)
        || character == '.'
        || character == '_'
        || character == '-';
}

static string GetUnicodeCategory(char character)
{
    return CharUnicodeInfo.GetUnicodeCategory(character).ToString();
}

static ClaimsPrincipal ValidateSessionToken(string token, TokenValidationParameters validationParameters)
{
    var tokenHandler = new JwtSecurityTokenHandler();
    return tokenHandler.ValidateToken(token, validationParameters, out _);
}
