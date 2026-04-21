using AxonVoiceAI.KnowledgeBase.Data;
using AxonVoiceAI.KnowledgeBase.Ingestion;
using AxonVoiceAI.KnowledgeBase.Providers;
using AxonVoiceAI.KnowledgeBase.Retrieval;
using AxonVoiceAI.Shared.Contracts;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Qdrant.Client;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration["POSTGRES_CONNECTION_STRING"]
    ?? throw new InvalidOperationException("POSTGRES_CONNECTION_STRING is required.");
var jwtSigningKey = builder.Configuration["JWT_SIGNING_KEY"]
    ?? throw new InvalidOperationException("JWT_SIGNING_KEY is required.");
var platformBaseUrl = builder.Configuration["PLATFORM_BASE_URL"]
    ?? throw new InvalidOperationException("PLATFORM_BASE_URL is required.");
var ollamaUrl = builder.Configuration["OLLAMA_URL"]
    ?? throw new InvalidOperationException("OLLAMA_URL is required.");
var ollamaModel = builder.Configuration["OLLAMA_EMBED_MODEL"] ?? "nomic-embed-text";
var qdrantUrl = builder.Configuration["QDRANT_URL"]
    ?? throw new InvalidOperationException("QDRANT_URL is required.");

builder.Services.AddDbContext<KnowledgeBaseDbContext>(options =>
    options.UseNpgsql(connectionString));

builder.Services.AddSingleton(_ =>
{
    var uri = new Uri(qdrantUrl);
    return new QdrantClient(uri.Host, uri.Port);
});

builder.Services.AddSingleton<QdrantVectorStore>();

builder.Services.AddSingleton<IEmbeddingProvider>(sp =>
{
    var factory = sp.GetRequiredService<IHttpClientFactory>();
    var client = factory.CreateClient("ollama");
    client.BaseAddress = new Uri(ollamaUrl);
    client.Timeout = TimeSpan.FromMinutes(5);
    var logger = sp.GetRequiredService<ILogger<OllamaEmbeddingProvider>>();
    return new OllamaEmbeddingProvider(client, ollamaModel, logger);
});

builder.Services.AddHttpClient("ollama");
builder.Services.AddSingleton<PdfExtractor>();
builder.Services.AddSingleton<DocxExtractor>();
builder.Services.AddScoped<IKnowledgeRetriever, KnowledgeRetriever>();
builder.Services.AddSingleton<DocumentIngestionJob>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<DocumentIngestionJob>());

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
            ValidateAudience = false,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30),
        };
    });

builder.Services.AddAuthorization();
builder.Services.AddControllers();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<KnowledgeBaseDbContext>();
    await db.Database.MigrateAsync();
    var vectorStore = scope.ServiceProvider.GetRequiredService<QdrantVectorStore>();
    await vectorStore.EnsureCollectionExistsAsync(CancellationToken.None);
}

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

await app.RunAsync();
