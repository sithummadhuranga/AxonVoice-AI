using AxonVoiceAI.SessionRelay.Audio;
using AxonVoiceAI.SessionRelay.Handlers;
using AxonVoiceAI.Shared.Contracts;
using AxonVoiceAI.Shared.SemanticKernel.Plugins;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.SemanticKernel;
using StackExchange.Redis;
using System.Net.WebSockets;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

var jwtSigningKey = builder.Configuration["JWT_SIGNING_KEY"]
    ?? throw new InvalidOperationException("JWT_SIGNING_KEY is required.");
var platformBaseUrl = builder.Configuration["PLATFORM_BASE_URL"]
    ?? throw new InvalidOperationException("PLATFORM_BASE_URL is required.");
var redisUrl = builder.Configuration["REDIS_URL"]
    ?? throw new InvalidOperationException("REDIS_URL is required.");
var agentConfigUrl = builder.Configuration["AGENT_CONFIG_URL"]
    ?? throw new InvalidOperationException("AGENT_CONFIG_URL is required.");
var knowledgeBaseUrl = builder.Configuration["KNOWLEDGE_BASE_URL"]
    ?? throw new InvalidOperationException("KNOWLEDGE_BASE_URL is required.");

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

    var webSocket = await httpContext.WebSockets.AcceptWebSocketAsync();
    var channel = new WebSocketAudioChannel(webSocket);

    var context = new AxonVoiceAI.Shared.DTOs.SessionStartContextDto(tenantId, agentId, sessionId, "en");
    await sessionHandler.RunSessionAsync(context, channel, ct);
});

await app.RunAsync();
