using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using LightningSPP.Licensing;

namespace wwwmmm.Web;

/// <summary>Longo owns the durable aggregate. Identity, receipts and notifications commit together using CAS.</summary>
internal sealed class CustomerStore
{
    internal sealed record User(string Id, string Email, string PasswordHash, bool EmailVerified, int Version, string Status);
    internal sealed record Entitlement(string Id, string CustomerId, string? SubscriptionId, string Tier,
        string LicenseKey, long Expires, string SubscriptionStatus, bool Activated, string EmailState);
    internal sealed record PaidGrant(string PaymentId, string EventId, string CustomerId, string? SubscriptionId,
        string Email, string Tier, DateTimeOffset Expires, string SubscriptionStatus, string? ExpectedUserId = null);
    internal sealed record Token(string UserId, string Purpose, long Expires, int Version);
    internal sealed record Session(string UserId, int Version, long Expires);
    internal sealed record Customer(string Id, string? UserId, string Email);
    internal sealed record Notice(string State, int Attempts, long LeaseUntil, long NextAttempt);
    internal sealed record Limit(long Window, int Count);
    internal sealed record Checkout(string PriceId, string Nonce, long Created, string? SessionId = null);
    internal sealed class State
    {
        public int Schema { get; set; } = 1;
        public Dictionary<string, User> Users { get; set; } = new();
        public Dictionary<string, Token> Tokens { get; set; } = new();
        public Dictionary<string, Session> Sessions { get; set; } = new();
        public Dictionary<string, Customer> Customers { get; set; } = new();
        public Dictionary<string, Entitlement> Entitlements { get; set; } = new();
        public Dictionary<string, string> Receipts { get; set; } = new();
        public Dictionary<string, string> Events { get; set; } = new();
        public Dictionary<string, Notice> Notices { get; set; } = new();
        public Dictionary<string, HashSet<string>> Activations { get; set; } = new();
        public Dictionary<string, Limit> Limits { get; set; } = new();
        public Dictionary<string, Checkout> Checkouts { get; set; } = new();
    }
    private sealed record Envelope(long Revision, State Data);
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private static readonly HttpClient Client = new() { Timeout = TimeSpan.FromSeconds(20) };
    private static readonly ConcurrentDictionary<string, object> Gates = new();
    private readonly Uri endpoint;
    private readonly object gate;
    private readonly TimeProvider clock;
    private static readonly PasswordHasher<User> Passwords = new(Options.Create(new PasswordHasherOptions { IterationCount = 600_000 }));
    private static readonly User Dummy = new("", "", "", false, 0, "ACTIVE");
    private static readonly string DummyHash = Passwords.HashPassword(Dummy, Convert.ToHexString(RandomNumberGenerator.GetBytes(32)));
    internal string DirectoryPath { get; }
    internal long Now => clock.GetUtcNow().ToUnixTimeSeconds();

    internal CustomerStore(string directory, TimeProvider? timeProvider = null)
    {
        clock = timeProvider ?? TimeProvider.System;
        DirectoryPath = Path.Combine(Path.GetFullPath(directory), "customers");
        Directory.CreateDirectory(DirectoryPath);
        if (File.Exists(Path.Combine(DirectoryPath, "customers.db"))) throw new InvalidOperationException("Existing customer data requires an explicit reviewed migration to Longo.");
        if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(DirectoryPath, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        if (!Uri.TryCreate(Environment.GetEnvironmentVariable("LONGO_URL"), UriKind.Absolute, out var baseUri) ||
            baseUri.Scheme is not ("http" or "https") || !string.IsNullOrEmpty(baseUri.UserInfo))
            throw new InvalidOperationException("LONGO_URL must identify the isolated durable Longo service.");
        var name = RuntimeSecurity.IsProduction ? "customers-v1" : Hash(DirectoryPath);
        endpoint = new Uri(baseUri.ToString().TrimEnd('/') + "/v1/lightning/state/" + name);
        gate = Gates.GetOrAdd(endpoint.ToString(), _ => new object());
        _ = ReadState(); // Fail closed; never substitute local or in-memory persistence.
    }
    private Envelope ReadEnvelope()
    {
        using var response = Client.GetAsync(endpoint).GetAwaiter().GetResult();
        response.EnsureSuccessStatusCode();
        var envelope = response.Content.ReadFromJsonAsync<Envelope>(Json).GetAwaiter().GetResult() ?? throw new IOException("Longo state missing.");
        if (envelope.Data.Schema != 1) throw new IOException("Unsupported customer schema.");
        return envelope;
    }
    internal State ReadState() => ReadEnvelope().Data;
    private T Change<T>(Func<State, T> action)
    {
        lock (gate)
        {
            for (var attempt = 0; attempt < 32; attempt++)
            {
                var current = ReadEnvelope(); var result = action(current.Data);
                using var request = new HttpRequestMessage(HttpMethod.Put, endpoint);
                request.Headers.TryAddWithoutValidation("If-Match", current.Revision.ToString(System.Globalization.CultureInfo.InvariantCulture));
                request.Content = JsonContent.Create(new { data = current.Data }, options: Json);
                using var response = Client.SendAsync(request).GetAwaiter().GetResult();
                if (response.StatusCode == HttpStatusCode.Conflict) continue;
                response.EnsureSuccessStatusCode(); return result;
            }
            throw new IOException("Longo contention; retry required.");
        }
    }
    private void Change(Action<State> action) => Change(s => { action(s); return true; });
    internal static string Hash(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
    internal static string RandomToken() => Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
    internal static string NormalizeEmail(string email)
    {
        if (email.Length > 254 || !System.Net.Mail.MailAddress.TryCreate(email.Trim(), out var address) || address.Address != email.Trim() || !address.Host.Contains('.'))
            throw new ArgumentException("Enter a valid email address.");
        return address.Address.ToUpperInvariant();
    }
    internal static void ValidatePassword(string password)
    { if (password.Length is < 12 or > 128) throw new ArgumentException("Use a password between 12 and 128 characters."); }
    private static User? Find(State s, string value, bool byEmail = false) => byEmail ?
        s.Users.Values.SingleOrDefault(u => string.Equals(u.Email, value, StringComparison.OrdinalIgnoreCase)) : s.Users.GetValueOrDefault(value);
    internal User? FindUser(string value, bool byEmail = false) => Find(ReadState(), value, byEmail);
    internal User Register(string email, string password)
    {
        email = NormalizeEmail(email); ValidatePassword(password);
        var user = new User(Guid.NewGuid().ToString("N"), email, "", false, 0, "ACTIVE");
        user = user with { PasswordHash = Passwords.HashPassword(user, password) };
        return Change(s => { var existing = Find(s, email, true); if (existing is not null) return existing; s.Users.Add(user.Id, user); return user; });
    }
    internal User? Login(string email, string password)
    {
        User? user = null;
        try { user = FindUser(NormalizeEmail(email), true); } catch (ArgumentException) { }
        var check = Passwords.VerifyHashedPassword(user ?? Dummy, user?.PasswordHash ?? DummyHash, password);
        if (check == PasswordVerificationResult.Failed || user is null || !user.EmailVerified || user.Status != "ACTIVE") return null;
        if (check == PasswordVerificationResult.SuccessRehashNeeded)
        {
            var hash = Passwords.HashPassword(user, password);
            Change(s => { if (s.Users[user.Id].PasswordHash == user.PasswordHash) s.Users[user.Id] = user with { PasswordHash = hash }; });
        }
        return user;
    }
    internal string CreateToken(User user, string purpose, TimeSpan lifetime)
    {
        if (purpose is not ("verify" or "reset")) throw new ArgumentException("Invalid token purpose.");
        var token = RandomToken();
        Change(s => { foreach (var key in s.Tokens.Where(x => x.Value.UserId == user.Id && x.Value.Purpose == purpose).Select(x => x.Key).ToArray()) s.Tokens.Remove(key);
            s.Tokens.Add(Hash(token), new(user.Id, purpose, Now + (long)lifetime.TotalSeconds, s.Users[user.Id].Version)); });
        return token;
    }
    internal bool ConsumeToken(string token, string purpose, string? password = null)
    {
        if (token.Length != 64 || purpose is not ("verify" or "reset")) return false;
        if (purpose == "reset") ValidatePassword(password ?? "");
        return Change(s =>
        {
            var item = s.Tokens.GetValueOrDefault(Hash(token));
            if (item is null || item.Purpose != purpose || item.Expires <= Now) return false;
            var user = Find(s, item.UserId);
            if (user is null || user.Version != item.Version || user.Status != "ACTIVE") return false;
            if (purpose == "verify")
            {
                s.Users[user.Id] = user with { EmailVerified = true };
                var candidates = s.Customers.Values.Where(c => string.Equals(c.Email, user.Email, StringComparison.OrdinalIgnoreCase) && c.UserId is null).ToArray();
                if (candidates.Length == 1 && !s.Customers.Values.Any(c => c.UserId == user.Id)) s.Customers[candidates[0].Id] = candidates[0] with { UserId = user.Id };
            }
            else
            {
                s.Users[user.Id] = user with { PasswordHash = Passwords.HashPassword(user, password!), Version = checked(user.Version + 1) };
                foreach (var key in s.Sessions.Where(x => x.Value.UserId == user.Id).Select(x => x.Key).ToArray()) s.Sessions.Remove(key);
                foreach (var key in s.Tokens.Where(x => x.Value.UserId == user.Id).Select(x => x.Key).ToArray()) s.Tokens.Remove(key);
            }
            s.Tokens.Remove(Hash(token)); return true;
        });
    }
    internal string StartSession(User user)
    {
        var token = RandomToken();
        Change(s => { var current = s.Users[user.Id];
            if (current.Version != user.Version || !current.EmailVerified || current.Status != "ACTIVE") throw new InvalidOperationException("Account changed during login.");
            foreach (var key in s.Sessions.Where(x => x.Value.Expires <= Now).Select(x => x.Key).ToArray()) s.Sessions.Remove(key);
            s.Sessions.Add(Hash(token), new(user.Id, user.Version, Now + 8 * 3600)); });
        return token;
    }
    internal bool ValidSession(string userId, string sessionId)
    {
        var s = ReadState(); var session = s.Sessions.GetValueOrDefault(Hash(sessionId)); var user = Find(s, userId);
        return session is not null && session.UserId == userId && session.Expires > Now && user is { EmailVerified: true, Status: "ACTIVE" } && user.Version == session.Version;
    }
    internal void EndSession(string token) => Change(s => { s.Sessions.Remove(Hash(token)); });
    internal bool RateLimited(string key, int limit, TimeSpan window) => Change(s =>
    {
        var period = Now / (long)window.TotalSeconds * (long)window.TotalSeconds; var hash = Hash(key);
        var old = s.Limits.GetValueOrDefault(hash); var count = old?.Window == period ? checked(old.Count + 1) : 1;
        s.Limits[hash] = new(period, count);
        foreach (var stale in s.Limits.Where(x => x.Value.Window < Now - 48 * 3600).Select(x => x.Key).ToArray()) s.Limits.Remove(stale);
        return count > limit;
    });
    internal string? CustomerId(string userId) => ReadState().Customers.Values.SingleOrDefault(c => c.UserId == userId)?.Id;
    internal void LinkCustomer(string userId, string customerId) => Change(s =>
    {
        var user = Find(s, userId) ?? throw new InvalidOperationException("Unknown account.");
        if (!user.EmailVerified || user.Status != "ACTIVE" || !customerId.StartsWith("cus_", StringComparison.Ordinal)) throw new InvalidOperationException("Verified account and customer required.");
        var customer = s.Customers.GetValueOrDefault(customerId);
        if ((customer?.UserId is not null && customer.UserId != userId) || s.Customers.Values.Any(c => c.UserId == userId && c.Id != customerId)) throw new InvalidOperationException("Customer ownership conflict.");
        s.Customers[customerId] = new(customerId, userId, user.Email);
    });
    internal Entitlement CommitPaid(PaidGrant grant, Func<string> createLicense)
    {
        var email = NormalizeEmail(grant.Email);
        if (!grant.CustomerId.StartsWith("cus_", StringComparison.Ordinal) || string.IsNullOrEmpty(grant.PaymentId) || string.IsNullOrEmpty(grant.EventId) || grant.Expires.ToUnixTimeSeconds() <= Now) throw new ArgumentException("Invalid or expired paid grant.");
        var id = grant.SubscriptionId is null ? "payment:" + grant.PaymentId : "subscription:" + grant.SubscriptionId;
        return Change(s =>
        {
            var customer = s.Customers.GetValueOrDefault(grant.CustomerId);
            if (grant.ExpectedUserId is not null && customer?.UserId != grant.ExpectedUserId) throw new InvalidOperationException("Paid account mapping mismatch.");
            if (s.Events.TryGetValue(grant.EventId, out var payment) && payment != grant.PaymentId) throw new InvalidOperationException("Event payment changed.");
            if (s.Receipts.TryGetValue(grant.PaymentId, out var previous))
            {
                if (previous != id || s.Entitlements[id].CustomerId != grant.CustomerId) throw new InvalidOperationException("Payment ownership changed.");
                s.Events[grant.EventId] = grant.PaymentId; return View(s, s.Entitlements[id]);
            }
            if (customer is null)
            {
                var user = Find(s, email, true);
                var owner = user is { EmailVerified: true, Status: "ACTIVE" } && !s.Customers.Values.Any(c => c.UserId == user.Id) ? user.Id : null;
                s.Customers.Add(grant.CustomerId, new(grant.CustomerId, owner, email));
            }
            var current = s.Entitlements.GetValueOrDefault(id);
            if (current is not null && current.CustomerId != grant.CustomerId) throw new InvalidOperationException("Subscription ownership changed.");
            if (current is null || current.Expires < grant.Expires.ToUnixTimeSeconds())
            {
                current = new(id, grant.CustomerId, grant.SubscriptionId, grant.Tier, createLicense(), grant.Expires.ToUnixTimeSeconds(), grant.SubscriptionStatus, false, "PENDING");
                s.Entitlements[id] = current; s.Notices[id] = new("PENDING", 0, 0, 0);
            }
            s.Receipts.Add(grant.PaymentId, id); s.Events[grant.EventId] = grant.PaymentId;
            return View(s, current);
        });
    }
    private static Entitlement View(State s, Entitlement e) => e with { Activated = s.Activations.GetValueOrDefault(e.Id)?.Count > 0, EmailState = s.Notices[e.Id].State };
    internal List<Entitlement> Entitlements(string? userId = null, string? id = null)
    {
        var s = ReadState(); return s.Entitlements.Values.Where(e => (id is null || e.Id == id) && (userId is null || s.Customers[e.CustomerId].UserId == userId)).Select(e => View(s, e)).ToList();
    }
    internal Entitlement? GetEntitlement(string id) => Entitlements(id: id).SingleOrDefault();
    internal string NotificationEmail(string id) { var s = ReadState(); return s.Customers[s.Entitlements[id].CustomerId].Email; }
    internal bool LeaseNotification(string id) => Change(s =>
    {
        var notice = s.Notices[id]; if (notice.State == "PROVIDER_ACCEPTED" || notice.LeaseUntil > Now) return false;
        s.Notices[id] = notice with { LeaseUntil = Now + 300, Attempts = notice.Attempts + 1 }; return true;
    });
    internal void NotificationResult(string id, bool accepted, string? expectedLicense = null) => Change(s =>
    {
        if (expectedLicense is not null && s.Entitlements[id].LicenseKey != expectedLicense) return;
        var notice = s.Notices[id]; s.Notices[id] = notice with { State = accepted ? "PROVIDER_ACCEPTED" : "FAILED", LeaseUntil = 0,
            NextAttempt = accepted ? long.MaxValue : Now + Math.Min(3600, 30L << Math.Min(notice.Attempts, 6)) };
    });
    internal string[] PendingNotifications() => ReadState().Notices.Where(x => x.Value.State != "PROVIDER_ACCEPTED" && x.Value.LeaseUntil <= Now && x.Value.NextAttempt <= Now).Select(x => x.Key).Take(20).ToArray();
    internal void SubscriptionState(string id, string customerId, string status) => Change(s =>
    {
        foreach (var e in s.Entitlements.Values.Where(e => e.SubscriptionId == id && e.CustomerId == customerId).ToArray()) s.Entitlements[e.Id] = e with { SubscriptionStatus = status };
    });
    internal bool Activate(string userId, string entitlementId, string deviceId)
    {
        if (!Guid.TryParse(deviceId, out _)) return false;
        return Change(s =>
        {
            var e = s.Entitlements.GetValueOrDefault(entitlementId);
            if (e is null || s.Customers[e.CustomerId].UserId != userId || e.Expires <= Now || e.SubscriptionStatus is not ("active" or "paid") || !LicenseKey.Validate(e.LicenseKey, out var license) || license is null || !license.IsValid()) return false;
            var devices = s.Activations.GetValueOrDefault(e.Id) ?? new HashSet<string>();
            if (!devices.Contains(deviceId) && devices.Count >= 1) return false;
            devices.Add(deviceId); s.Activations[e.Id] = devices; return true;
        });
    }
    internal string CheckoutNonce(string userId, string priceId) => Change(s =>
    {
        if (s.Entitlements.Values.Any(e => s.Customers[e.CustomerId].UserId == userId && BlocksPurchase(e, Now))) throw new InvalidOperationException("An existing entitlement or subscription must be managed from your account; another checkout is not available.");
        var existing = s.Checkouts.GetValueOrDefault(userId);
        if (existing is not null && existing.Created > Now - 23 * 3600)
        { if (existing.PriceId != priceId) throw new InvalidOperationException("Another plan checkout is pending."); return existing.Nonce; }
        var nonce = Guid.NewGuid().ToString("N"); s.Checkouts[userId] = new(priceId, nonce, Now); return nonce;
    });
    internal bool CanPurchase(string userId) => !Entitlements(userId).Any(e => BlocksPurchase(e, Now));
    private static bool BlocksPurchase(Entitlement e, long now) =>
        e.SubscriptionStatus is "active" or "trialing" or "past_due" or "incomplete" or "unpaid" or "paused" ||
        (e.SubscriptionStatus == "paid" && e.Expires > now);
    internal void RecordCheckout(string userId, string nonce, string sessionId) => Change(s =>
    {
        var checkout = s.Checkouts[userId]; if (checkout.Nonce != nonce || !sessionId.StartsWith("cs_", StringComparison.Ordinal)) throw new InvalidOperationException("Checkout mismatch.");
        s.Checkouts[userId] = checkout with { SessionId = sessionId };
    });
}
