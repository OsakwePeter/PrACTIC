namespace CoffeeNChill.Gateway.Services;

/// <summary>
/// Reads storage connection strings from configuration. Environment variables override appsettings.json,
/// so docker-compose can inject real values with e.g. AzureWebJobsStorage=...
/// </summary>
public static class StorageSettings
{
    /// <summary>Tables + Queues connection string (Azurite locally, a storage account in production).</summary>
    public static string Primary(IConfiguration config) =>
        config["AzureWebJobsStorage"] is { Length: > 0 } value
            ? value
            : throw new InvalidOperationException("AzureWebJobsStorage connection string is not configured.");

    /// <summary>
    /// File share connection string. Azurite does NOT emulate Azure Files, so the staff-docs endpoints
    /// need a real storage account in AzureFileStorage (same setting the Functions project uses).
    /// Falls back to the primary connection string if AzureFileStorage is empty.
    /// </summary>
    public static string Files(IConfiguration config) =>
        config["AzureFileStorage"] is { Length: > 0 } value ? value : Primary(config);
}
