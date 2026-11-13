using System.Net;
using System.Text.Json;

namespace wwwmmm.Web;

internal sealed class GuestActivityStore
{
    internal sealed record Activity(DateTimeOffset At, string Operation, int Status);
    internal sealed record Guest(string Id, DateTimeOffset Created, DateTimeOffset Expires, List<Activity> History);
    private readonly string directory;
    private readonly TimeProvider clock;
    private readonly object gate = new();
    internal GuestActivityStore(string dataDirectory, TimeProvider? clock = null)
    {
        directory = Path.Combine(Path.GetFullPath(dataDirectory), "guests");
        this.clock = clock ?? TimeProvider.System;
        Directory.CreateDirectory(directory);
        if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(directory, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
    }
    private string PathFor(string id)
    {
        if (id.Length != 32 || !id.All(Uri.IsHexDigit)) throw new ArgumentException("Invalid guest ID.");
        return Path.Combine(directory, id + ".json");
    }
    internal Guest Begin(string? existingId)
    {
        lock (gate)
        {
            var now = clock.GetUtcNow();
            if (existingId is { Length: 32 } && existingId.All(Uri.IsHexDigit))
            {
                var path = PathFor(existingId);
                if (File.Exists(path))
                {
                    var existing = Read(path);
                    if (existing.Expires > now) return existing;
                }
            }
            var guest = new Guest(Guid.NewGuid().ToString("N"), now, now.AddDays(7), []);
            Save(guest);
            return guest;
        }
    }
    internal void Record(string id, string operation, int status)
    {
        if (operation is not ("document.process" or "document.package" or "archive.process" or "assistant.chat")) throw new ArgumentException("Unknown guest operation.");
        if (status is < 100 or > 599) throw new ArgumentOutOfRangeException(nameof(status));
        lock (gate)
        {
            var guest = Read(PathFor(id));
            if (guest.Expires <= clock.GetUtcNow()) return;
            guest.History.Add(new(clock.GetUtcNow(), operation, status));
            if (guest.History.Count > 20) guest.History.RemoveRange(0, guest.History.Count - 20);
            Save(guest);
        }
    }
    internal void Cleanup()
    {
        lock (gate)
            foreach (var path in Directory.EnumerateFiles(directory, "*.json"))
                if (Read(path).Expires <= clock.GetUtcNow()) File.Delete(path);
    }
    private static Guest Read(string path) => JsonSerializer.Deserialize<Guest>(File.ReadAllText(path))
        ?? throw new IOException("Guest history could not be read.");
    private void Save(Guest guest)
    {
        var path = PathFor(guest.Id);
        var temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllText(temp, JsonSerializer.Serialize(guest));
            if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(temp, UnixFileMode.UserRead | UnixFileMode.UserWrite);
            File.Move(temp, path, overwrite: true);
        }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }
}

internal static class GuestActivity
{
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, GuestActivityStore> Stores = new();
    private static GuestActivityStore Store => Stores.GetOrAdd(Path.GetFullPath(RuntimeSecurity.DataDirectory), p => new(p));
    internal static (GuestActivityStore Store, string Id) Begin(HttpListenerContext context)
    {
        var store = Store;
        var name = RuntimeSecurity.IsProduction ? "__Host-LightningGuest" : "LightningGuest";
        var guest = store.Begin(context.Request.Cookies[name]?.Value);
        context.Response.Headers.Add("Set-Cookie", $"{name}={guest.Id}; Path=/; HttpOnly; SameSite=Lax; Expires={guest.Expires.UtcDateTime:R}" + (RuntimeSecurity.IsProduction ? "; Secure" : ""));
        return (store, guest.Id);
    }
    internal static async Task CleanupAsync(CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromHours(1));
        try
        {
            do
            {
                try { Store.Cleanup(); }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
                { InternalAuditLogger.Error("Guest history cleanup unavailable: " + ex.GetType().Name); }
            } while (await timer.WaitForNextTickAsync(cancellationToken));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
    }
}
