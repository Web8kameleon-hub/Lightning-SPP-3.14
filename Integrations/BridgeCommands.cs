using System.Text.Json;

namespace wwwmmm.Integrations;

internal static class BridgeCommands
{
    internal static async Task<int> RunAsync(string[] args, CancellationToken cancellationToken = default)
    {
        try
        {
            if (args.Length == 3 && args[1] == "excel-export")
            {
                var destination = Path.GetFullPath(args[2]);
                if (!destination.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
                    throw new ArgumentException("Export destination must end in .xlsx.");
                if (File.Exists(destination))
                    throw new ArgumentException("Export destination already exists; choose a new filename.");
                using var core = new ClisonixExcelCoreBridge(Connection("SPP_EXCEL_CORE_URL", "SPP_EXCEL_CORE_API_KEY"));
                var data = await core.ExportLagterAsync(cancellationToken);
                await SaveNewFileAsync(destination, data, cancellationToken);
                Console.WriteLine(JsonSerializer.Serialize(new { saved = destination, bytes = data.Length }));
                return 0;
            }
            if (args.Length == 3 && args[1] is "excel-parse" or "excel-validate")
            {
                using var excel = new ClisonixExcelBridge(Connection("SPP_EXCEL_SERVICE_URL", "SPP_EXCEL_SERVICE_API_KEY"));
                var bytes = await ReadBoundedFileAsync(args[2], 10 * 1024 * 1024, cancellationToken);
                var filename = Path.GetFileName(args[2]);
                var result = args[1] == "excel-parse"
                    ? await excel.ParseAsync(filename, bytes, cancellationToken)
                    : await excel.ValidateAsync(filename, bytes, cancellationToken);
                Console.WriteLine(result.GetRawText());
                return 0;
            }
            if (args.Length == 3 && args[1] == "node-read")
            {
                using var nodeDb = new OsClxNodeDbBridge(Connection("SPP_OSCLX_URL", "SPP_OSCLX_API_KEY", "SPP_OSCLX_HMAC_SECRET"));
                Console.WriteLine((await nodeDb.ReadNodeAsync(args[2], cancellationToken)).GetRawText());
                return 0;
            }
            if (args.Length == 4 && args[1] == "node-ingest")
            {
                var json = await ReadBoundedFileAsync(args[3], 64 * 1024, cancellationToken);
                var telemetry = OsClxNodeDbBridge.ParseTelemetry(System.Text.Encoding.UTF8.GetString(json));
                using var nodeDb = new OsClxNodeDbBridge(Connection("SPP_OSCLX_URL", "SPP_OSCLX_API_KEY", "SPP_OSCLX_HMAC_SECRET"));
                Console.WriteLine((await nodeDb.IngestAsync(args[2], telemetry, cancellationToken)).GetRawText());
                return 0;
            }
            Console.Error.WriteLine("Usage: bridge excel-export <new.xlsx> | excel-parse <input.xlsx> | excel-validate <input.xlsx> | node-read <node-id> | node-ingest <node-id> <measured-telemetry.json>");
            return 2;
        }
        catch (OperationCanceledException)
        {
            Console.Error.WriteLine("Bridge cancelled. For writes, read back the node before retrying.");
            return 1;
        }
        catch (JsonException)
        {
            Console.Error.WriteLine("Invalid telemetry JSON; all measured fields are required and unknown fields are rejected.");
            return 1;
        }
        catch (Exception ex) when (ex is BridgeException or ArgumentException)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Console.Error.WriteLine("Could not read or save the bridge file. Check its location, size and access permissions.");
            return 1;
        }
    }

    private static BridgeConnection Connection(string urlName, string keyName, string? hmacName = null)
    {
        var value = Environment.GetEnvironmentVariable(urlName);
        if (string.IsNullOrWhiteSpace(value) || !Uri.TryCreate(value, UriKind.Absolute, out var uri))
            throw new ArgumentException($"Configure {urlName} with the actual service base URL.");
        return new(uri, Environment.GetEnvironmentVariable(keyName),
            hmacName is null ? null : Environment.GetEnvironmentVariable(hmacName));
    }

    private static async Task<byte[]> ReadBoundedFileAsync(string path, int limit, CancellationToken cancellationToken)
    {
        await using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read,
            8192, FileOptions.Asynchronous | FileOptions.SequentialScan);
        if (file.Length == 0 || file.Length > limit)
            throw new ArgumentException($"Input file must contain 1 to {limit} bytes.");
        var bytes = new byte[(int)file.Length];
        await file.ReadExactlyAsync(bytes, cancellationToken);
        return bytes;
    }

    private static async Task SaveNewFileAsync(string destination, byte[] data, CancellationToken cancellationToken)
    {
        var temporary = destination + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            await using (var file = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                await file.WriteAsync(data, cancellationToken);
                await file.FlushAsync(cancellationToken);
            }
            cancellationToken.ThrowIfCancellationRequested();
            File.Move(temporary, destination, overwrite: false);
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
    }
}
