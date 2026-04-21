using AxonVoiceAI.SessionRelay.Audio;
using AxonVoiceAI.SessionRelay.Handlers;
using AxonVoiceAI.Shared.Configuration;
using AxonVoiceAI.Shared.Contracts;
using AxonVoiceAI.Shared.SemanticKernel.Plugins;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.SemanticKernel;
using StackExchange.Redis;
using System.Net.WebSockets;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

var jwtSigningKey = builder.Configuration.GetRequiredValue(builder.Environment, "JWT_SIGNING_KEY");
var platformBaseUrl = builder.Configuration.GetRequiredValue(builder.Environment, "PLATFORM_BASE_URL");
var redisUrl = builder.Configuration.GetRequiredValue(builder.Environment, "REDIS_URL");
var agentConfigUrl = builder.Configuration.GetRequiredValue(builder.Environment, "AGENT_CONFIG_URL");
var knowledgeBaseUrl = builder.Configuration.GetRequiredValue(builder.Environment, "KNOWLEDGE_BASE_URL");

builder.Services.AddPlatformDataProtection(builder.Configuration, builder.Environment, "AxonVoiceAI.SessionRelay");

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

// Register Semantic Kernel plugins for function call dispatch.
builder.Services.AddSingleton<KernelPluginCollection>(sp =>
{
    var availabilityRepo = sp.GetRequiredService<IAvailabilityRepository>();
    var bookingRepo = sp.GetRequiredService<IBookingRepository>();
    var collection = new KernelPluginCollection();
    collection.AddFromObject(new AvailabilityPlugin(availabilityRepo));
    collection.AddFromObject(new BookingPlugin(bookingRepo));
    return collection;
});

builder.Services.AddSingleton<SessionHandler>();

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSigningKey)),
            ValidateIssuer = true,
            ValidIssuer = platformBaseUrl,
            ValidateAudience = true,
            ValidAudience = "relay",
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero,
        };

        // Allow the JWT to be passed as a WebSocket query parameter.
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = ctx =>
            {
                var token = ctx.Request.Query["token"].FirstOrDefault();
                if (!string.IsNullOrEmpty(token))
                    ctx.Token = token;
                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization();

var app = builder.Build();

app.UseWebSockets(new WebSocketOptions { KeepAliveInterval = TimeSpan.FromSeconds(30) });
app.UseAuthentication();
app.UseAuthorization();

app.Map("/ws/session", async (HttpContext httpContext, SessionHandler sessionHandler, CancellationToken ct) =>
{
    if (!httpContext.WebSockets.IsWebSocketRequest)
    {
        httpContext.Response.StatusCode = 400;
        return;
    }

    var user = httpContext.User;
    if (!user.Identity?.IsAuthenticated ?? true)
    {
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
