using AxonVoiceAI.KnowledgeBase.Data;
using AxonVoiceAI.KnowledgeBase.Ingestion;
using AxonVoiceAI.KnowledgeBase.Providers;
using AxonVoiceAI.KnowledgeBase.Retrieval;
using AxonVoiceAI.Shared.Configuration;
using AxonVoiceAI.Shared.Contracts;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetRequiredValue(builder.Environment, "POSTGRES_CONNECTION_STRING", "POSTGRES_URL");
var jwtSigningKey = builder.Configuration.GetRequiredValue(builder.Environment, "JWT_SIGNING_KEY");
var platformBaseUrl = builder.Configuration.GetRequiredValue(builder.Environment, "PLATFORM_BASE_URL");
var ollamaUrl = builder.Configuration.GetRequiredValue(builder.Environment, "OLLAMA_URL");
var ollamaModel = builder.Configuration.GetValueOrDefault("nomic-embed-text", "OLLAMA_EMBED_MODEL", "OLLAMA_EMBEDDING_MODEL");
var qdrantUrl = builder.Configuration.GetRequiredValue(builder.Environment, "QDRANT_URL");

builder.Services.AddPlatformDataProtection(builder.Configuration, builder.Environment, "AxonVoiceAI.KnowledgeBase");

builder.Services.AddDbContext<KnowledgeBaseDbContext>(options =>
    options.UseNpgsql(connectionString));

builder.Services.AddSingleton(_ => QdrantClientFactory.Create(qdrantUrl));

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

app.UseExceptionHandler(pipeline =>
{
    pipeline.Run(async context =>
    {
        var exceptionFeature = context.Features.Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerFeature>();
        var logger = context.RequestServices
            .GetRequiredService<ILoggerFactory>()
            .CreateLogger("ExceptionHandler");
        logger.LogError(exceptionFeature?.Error, "Unhandled exception on {Method} {Path}",
            context.Request.Method, context.Request.Path);
        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsJsonAsync(new { error = "An internal error occurred." });
    });
});

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

await app.RunAsync();
