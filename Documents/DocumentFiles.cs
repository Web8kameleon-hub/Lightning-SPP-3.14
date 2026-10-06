namespace wwwmmm.Documents;

internal static class DocumentFiles
{
    internal static async Task<byte[]> ReadAsync(string path, int limit, CancellationToken token)
    {
        await using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (file.Length <= 0 || file.Length > limit)
            throw new ArgumentException($"Input must contain between 1 and {limit} bytes.");
        var data = new byte[(int)file.Length];
        await file.ReadExactlyAsync(data, token);
        return data;
    }

    internal static async Task SaveNewAsync(string path, byte[] bytes, string extension, CancellationToken token)
    {
        var destination = Path.GetFullPath(path);
        if (!destination.EndsWith(extension, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException($"Destination must end in {extension}.");
        var temporary = destination + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            await using (var output = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                await output.WriteAsync(bytes, token);
                await output.FlushAsync(token);
            }
            token.ThrowIfCancellationRequested();
            File.Move(temporary, destination, overwrite: false);
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
    }
}
