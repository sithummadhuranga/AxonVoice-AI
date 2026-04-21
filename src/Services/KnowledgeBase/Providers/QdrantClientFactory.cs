using Qdrant.Client;

namespace AxonVoiceAI.KnowledgeBase.Providers;

internal static class QdrantClientFactory
{
    private const int DefaultGrpcPort = 6334;
    private const int DefaultRestPort = 6333;

    public static QdrantClient Create(string configuredAddress)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(configuredAddress);

        var endpoint = ResolveEndpoint(configuredAddress);
        return new QdrantClient(endpoint.Host, endpoint.Port, endpoint.UseHttps);
    }

    internal static QdrantGrpcEndpoint ResolveEndpoint(string configuredAddress)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(configuredAddress);

        var uri = new Uri(configuredAddress, UriKind.Absolute);
        var grpcPort = uri.Port == DefaultRestPort ? DefaultGrpcPort : uri.Port;

        return new QdrantGrpcEndpoint(
            uri.Host,
            grpcPort,
            uri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase));
    }
}

internal sealed record QdrantGrpcEndpoint(string Host, int Port, bool UseHttps);
