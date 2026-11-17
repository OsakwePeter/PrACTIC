using Azure.Data.Tables;

namespace CoffeeNChill.Gateway.Services;

/// <summary>
/// Creates the table on first use (not at startup) so the gateway still boots when storage is
/// briefly unavailable; the failure then surfaces as a clean 503 from the exception middleware.
/// </summary>
public abstract class TableBackedService
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private volatile bool _ready;

    protected TableBackedService(TableClient table) => Table = table;

    protected TableClient Table { get; }

    protected async Task<TableClient> ReadyTableAsync(CancellationToken ct)
    {
        if (_ready) return Table;

        await _gate.WaitAsync(ct);
        try
        {
            if (!_ready)
            {
                await Table.CreateIfNotExistsAsync(ct);
                _ready = true;
            }
        }
        finally
        {
            _gate.Release();
        }

        return Table;
    }
}
