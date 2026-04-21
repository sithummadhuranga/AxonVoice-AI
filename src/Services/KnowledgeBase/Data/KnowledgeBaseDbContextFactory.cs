using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace AxonVoiceAI.KnowledgeBase.Data;

/// <summary>
/// Provides a DbContext instance for EF Core design-time tooling (migrations)
/// without requiring the full application to start.
/// </summary>
public sealed class KnowledgeBaseDbContextFactory : IDesignTimeDbContextFactory<KnowledgeBaseDbContext>
{
    public KnowledgeBaseDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<KnowledgeBaseDbContext>();
        optionsBuilder.UseNpgsql(
            "Host=localhost;Database=axonvoice_knowledge;Username=postgres;Password=postgres",
            npgsql => npgsql.MigrationsAssembly(typeof(KnowledgeBaseDbContext).Assembly.FullName));
        return new KnowledgeBaseDbContext(optionsBuilder.Options);
    }
}
