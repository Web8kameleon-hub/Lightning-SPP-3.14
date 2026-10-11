using System.Net;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Text.Json;
using LightningSPP.Licensing;
using wwwmmm.Web;
using Xunit;

namespace LightningSPP.Tests;

/// <summary>Isolated local customer records and Stripe fixtures; never evidence of a real purchase.</summary>
public class CustomerLifecycleTests : IClassFixture<LongoFixture>
{
    private const string Password = "a long test password 123!";
    private static string TempDirectory() => Path.Combine(Path.GetTempPath(), "lightning-customer-tests-" + Guid.NewGuid().ToString("N"));
    private static CustomerStore.User Verified(CustomerStore store, string email)
    { var user = store.Register(email, Password); Assert.True(store.ConsumeToken(store.CreateToken(user,"verify",TimeSpan.FromHours(1)),"verify")); return store.FindUser(user.Id)!; }

    [Theory]
    [InlineData("not-email",Password)]
    [InlineData("a@example.test","short")]
    public void RegistrationRejectsInvalidInput(string email,string password) =>
        Assert.Throws<ArgumentException>(() => new CustomerStore(TempDirectory()).Register(email,password));

    [Fact]
    public void DuplicateNormalizedEmailDoesNotReplacePasswordAndUnverifiedLoginIsDenied()
    {
        var store = new CustomerStore(TempDirectory());
        var first = store.Register("a@example.test",Password); var duplicate = store.Register("A@EXAMPLE.TEST","another long password!");
        Assert.Equal(first.Id,duplicate.Id); Assert.NotEqual(Password,first.PasswordHash);
        Assert.Null(store.Login(first.Email,Password));
        var token = store.CreateToken(first,"verify",TimeSpan.FromHours(1));
        Assert.True(store.ConsumeToken(token,"verify")); Assert.False(store.ConsumeToken(token,"verify"));
        Assert.Null(store.Login(first.Email,"another long password!")); Assert.NotNull(store.Login(first.Email,Password));
    }
    [Fact]
    public void RegistrationAndRepeatedPaidEventsReuseTheExistingKey()
    {
        var store = new CustomerStore(TempDirectory());
        var issued = 0;
        var grant = new CustomerStore.PaidGrant("in_once", "evt_once", "cus_once", "sub_once",
            "once@example.test", "PayAsYouGo", DateTimeOffset.UtcNow.AddDays(30), "active");
        var original = store.CommitPaid(grant, () => { issued++; return "test-only-existing-key"; });
        var user = Verified(store, grant.Email);
        Assert.Equal(original.LicenseKey, Assert.Single(store.Entitlements(user.Id)).LicenseKey);
        Assert.Equal(user.Id, store.Register("ONCE@EXAMPLE.TEST", "different long password 123!").Id);
        var replay = store.CommitPaid(grant with { EventId = "evt_repeated_delivery" },
            () => { issued++; return "test-only-unexpected-key"; });
        Assert.Equal(original.LicenseKey, replay.LicenseKey);
        Assert.Equal(1, issued);
        Assert.Single(store.Entitlements(user.Id));
        Assert.Single(store.ReadState().Receipts);
    }

    [Fact]
    public void TokensAreHashedPurposeBoundExpiringAndResetRevokesAllSessions()
    {
        var clock = new MutableClock(); var store = new CustomerStore(TempDirectory(),clock); var user = Verified(store,"a@example.test");
        var session = store.StartSession(user); Assert.True(store.ValidSession(user.Id,session));
        var expired = store.CreateToken(user,"reset",TimeSpan.FromSeconds(1)); clock.Advance(TimeSpan.FromSeconds(2));
        Assert.False(store.ConsumeToken(expired,"reset",Password));
        var token = store.CreateToken(user,"reset",TimeSpan.FromHours(1));
        var durable = store.ReadState();
        Assert.False(durable.Tokens.ContainsKey(token));
        Assert.True(durable.Tokens.ContainsKey(CustomerStore.Hash(token)));
        Assert.False(durable.Sessions.ContainsKey(session));
        Assert.False(store.ConsumeToken(token,"verify"));
        Assert.True(store.ConsumeToken(token,"reset","new long password 456!"));
        Assert.False(store.ConsumeToken(token,"reset","new long password 456!"));
        Assert.False(store.ValidSession(user.Id,session)); Assert.Null(store.Login(user.Email,Password));
        Assert.NotNull(store.Login(user.Email,"new long password 456!"));
    }
    [Fact]
    public void DurableLimitsDoNotResetAcrossRestartsOrOtherWindowSizes()
    {
        var path=TempDirectory(); var clock=new MutableClock(); var store=new CustomerStore(path,clock);
        Assert.False(store.RateLimited("registration",1,TimeSpan.FromHours(1)));
        Assert.False(store.RateLimited("login",1,TimeSpan.FromMinutes(1)));
        Assert.True(new CustomerStore(path,clock).RateLimited("registration",1,TimeSpan.FromHours(1)));
        clock.Advance(TimeSpan.FromHours(2)); Assert.False(store.RateLimited("registration",1,TimeSpan.FromHours(1)));
    }
    [Fact]
    public void PaidCustomerOwnershipCannotBeReassignedToAnotherAccount()
    {
        var store=new CustomerStore(TempDirectory()); var a=Verified(store,"a@example.test"); var b=Verified(store,"b@example.test");
        store.LinkCustomer(a.Id,"cus_fixture_a");
        Assert.Throws<InvalidOperationException>(()=>store.LinkCustomer(b.Id,"cus_fixture_a"));
        Assert.Null(store.CustomerId(b.Id)); Assert.Equal("cus_fixture_a",store.CustomerId(a.Id));
    }
    [Fact]
    public async Task ConcurrentRepeatedEventsMintOneEntitlementAndOneLicenseAcrossStoreInstances()
    {
        var directory=TempDirectory(); var store=new CustomerStore(directory); var count=0;
        var grant=new CustomerStore.PaidGrant("in_fixture","evt_fixture","cus_fixture","sub_fixture","a@example.test","PayAsYouGo",DateTimeOffset.UtcNow.AddDays(30),"active");
        var jobs=Enumerable.Range(0,8).Select(i=>Task.Run(()=>new CustomerStore(directory).CommitPaid(grant with {EventId="evt_fixture_"+i},()=> { Interlocked.Increment(ref count); return "test-only-license"; })));
        var results=await Task.WhenAll(jobs);
        Assert.Equal(1,count); Assert.Single(store.Entitlements()); Assert.All(results,e=>Assert.Equal(results[0].LicenseKey,e.LicenseKey));
        Assert.Single(store.ReadState().Receipts);
        Assert.Equal(8,store.ReadState().Events.Count);
    }
    [Fact]
    public void IssuanceFailureRollsBackReceiptAndRemainsRetryable()
    {
        var store=new CustomerStore(TempDirectory());
        var grant=new CustomerStore.PaidGrant("in_fixture","evt_fixture","cus_fixture","sub_fixture","a@example.test","PayAsYouGo",DateTimeOffset.UtcNow.AddDays(30),"active");
        Assert.Throws<IOException>(()=>store.CommitPaid(grant,()=>throw new IOException("Fixture disk failure")));
        Assert.Empty(store.Entitlements());
        Assert.NotNull(store.CommitPaid(grant,()=>"test-only-license"));
    }
    [Fact]
    public void OlderInvoiceDoesNotShortenPaidPeriodOrDuplicateSubscriptionEntitlement()
    {
        var store=new CustomerStore(TempDirectory());
        var grant=new CustomerStore.PaidGrant("in_new","evt_new","cus_fixture","sub_fixture","a@example.test","PayAsYouGo",DateTimeOffset.UtcNow.AddDays(30),"active");
        var current=store.CommitPaid(grant,()=>"test-only-license");
        var older=store.CommitPaid(grant with {PaymentId="in_old",EventId="evt_old",Expires=DateTimeOffset.UtcNow.AddDays(10)},()=>throw new Exception("Must not mint"));
        Assert.Equal(current,older); Assert.Single(store.Entitlements());
    }
    [Fact]
    public void LegacyPurchaseIsClaimedOnlyAfterVerificationAndActivationUsesSignedLicenseAndOwner()
    {
        var old=Environment.GetEnvironmentVariable("LSPP_LICENSE_SIGNING_KEY");
        try
        {
            Environment.SetEnvironmentVariable("LSPP_LICENSE_SIGNING_KEY","fixture-signing-secret-with-more-than-32-bytes");
            var store=new CustomerStore(TempDirectory()); var a=store.Register("a@example.test",Password); var b=Verified(store,"b@example.test");
            var entitlement=store.CommitPaid(new("in_fixture","evt_fixture","cus_fixture","sub_fixture",a.Email,"PayAsYouGo",DateTimeOffset.UtcNow.AddDays(30),"active"),()=>LicenseKey.Generate(LicenseKey.LicenseTier.PayAsYouGo,a.Email,"",30).Key);
            Assert.Empty(store.Entitlements(a.Id)); Assert.Empty(store.Entitlements(b.Id));
            Assert.True(store.ConsumeToken(store.CreateToken(a,"verify",TimeSpan.FromHours(1)),"verify"));
            Assert.Single(store.Entitlements(a.Id));
            var device=Guid.NewGuid().ToString(); Assert.False(store.Activate(b.Id,entitlement.Id,device));
            Assert.True(store.Activate(a.Id,entitlement.Id,device)); Assert.True(store.Activate(a.Id,entitlement.Id,device));
            Assert.False(store.Activate(a.Id,entitlement.Id,Guid.NewGuid().ToString()));
            Assert.Equal("ACTIVE",CustomerAccounts.State(store.GetEntitlement(entitlement.Id)!,store.Now));
            store.SubscriptionState("sub_fixture","cus_fixture","past_due");
            Assert.False(store.Activate(a.Id,entitlement.Id,device)); Assert.Equal("PAST_DUE",CustomerAccounts.State(store.GetEntitlement(entitlement.Id)!,store.Now));
            var invalid=store.GetEntitlement(entitlement.Id)! with {LicenseKey="invalid" ,SubscriptionStatus="active"};
            Assert.Equal("FULFILLMENT_FAILED",CustomerAccounts.State(invalid,store.Now));
        }
        finally { Environment.SetEnvironmentVariable("LSPP_LICENSE_SIGNING_KEY",old); }
    }
    [Fact]
    public async Task HttpAuthenticationUsesProtectedCookiesCsrfAndOwnershipInsteadOfSuccessRedirect()
    {
        var old=Environment.GetEnvironmentVariable("LSPP_DATA_DIR"); var directory=TempDirectory();
        var oldTrustProxy=Environment.GetEnvironmentVariable("LSPP_TRUST_PROXY_HEADERS");
        Environment.SetEnvironmentVariable("LSPP_DATA_DIR",directory);
        Environment.SetEnvironmentVariable("LSPP_TRUST_PROXY_HEADERS","true");
        var listener=new TcpListener(IPAddress.Loopback,0); listener.Start(); var port=((IPEndPoint)listener.LocalEndpoint).Port; listener.Stop();
        using var cancel=new CancellationTokenSource(); var server=LicenseApiServer.RunAsync(port,cancel.Token);
        using var handler=new HttpClientHandler {CookieContainer=new CookieContainer()};
        using var client=new HttpClient(handler) {BaseAddress=new Uri($"http://localhost:{port}")};
        client.DefaultRequestHeaders.Add("Origin",client.BaseAddress.GetLeftPart(UriPartial.Authority));
        // The existing server's shared rate limiter must not leak this test's budget into PDF tests.
        client.DefaultRequestHeaders.Add("X-Forwarded-For","192.0.2.244");
        try
        {
            Assert.Equal(HttpStatusCode.Unauthorized,(await client.GetAsync("/api/account")).StatusCode);
            var csrf=(await client.GetFromJsonAsync<JsonElement>("/api/account/csrf")).GetProperty("csrfToken").GetString();
            client.DefaultRequestHeaders.Add("X-CSRF-Token",csrf);
            var invalid=await client.PostAsJsonAsync("/api/account/register",new {email="invalid",password="short"});
            Assert.Equal(HttpStatusCode.BadRequest,invalid.StatusCode);
            var store=CustomerAccounts.Store; var a=Verified(store,"a@example.test"); var b=Verified(store,"b@example.test");
            var badLogin=await client.PostAsJsonAsync("/api/account/login",new {email=a.Email,password="wrong"}); Assert.Equal(HttpStatusCode.Unauthorized,badLogin.StatusCode);
            var login=await client.PostAsJsonAsync("/api/account/login",new {email=a.Email,password=Password}); Assert.Equal(HttpStatusCode.OK,login.StatusCode);
            Assert.Contains(login.Headers.GetValues("Set-Cookie"),cookie=>cookie.Contains("httponly",StringComparison.OrdinalIgnoreCase)&&cookie.Contains("samesite=strict",StringComparison.OrdinalIgnoreCase));
            var account=await client.GetFromJsonAsync<JsonElement>("/api/account"); Assert.Equal(a.Id,account.GetProperty("userId").GetString());
            Assert.Empty(account.GetProperty("entitlements").EnumerateArray());
            client.DefaultRequestHeaders.Remove("X-CSRF-Token");
            Assert.Equal(HttpStatusCode.Forbidden,(await client.PostAsJsonAsync("/api/account/logout",new {})).StatusCode);
            client.DefaultRequestHeaders.Add("X-CSRF-Token",account.GetProperty("csrfToken").GetString());
            var bEntitlement=store.CommitPaid(new("in_b","evt_b","cus_b","sub_b",b.Email,"PayAsYouGo",DateTimeOffset.UtcNow.AddDays(30),"active"),()=>"test-only-invalid-key");
            Assert.Equal(HttpStatusCode.NotFound,(await client.PostAsJsonAsync("/api/account/license",new {entitlementId=bEntitlement.Id})).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound,(await client.PostAsJsonAsync("/api/account/activate",new {entitlementId=bEntitlement.Id,deviceId=Guid.NewGuid()})).StatusCode);
            Assert.Equal(HttpStatusCode.MethodNotAllowed,(await client.PostAsync("/",new StringContent("test webhook"))).StatusCode);
            var dashboard=await client.GetStringAsync("/dashboard?purchase=success"); Assert.Contains("purchaseStatus",dashboard);
            Assert.Equal(HttpStatusCode.OK,(await client.PostAsJsonAsync("/api/account/logout",new {})).StatusCode);
            Assert.Equal(HttpStatusCode.Unauthorized,(await client.GetAsync("/api/account")).StatusCode);
        }
        finally { cancel.Cancel(); await server; Environment.SetEnvironmentVariable("LSPP_DATA_DIR",old); Environment.SetEnvironmentVariable("LSPP_TRUST_PROXY_HEADERS",oldTrustProxy); }
    }
    private sealed class MutableClock : TimeProvider
    { private DateTimeOffset now=DateTimeOffset.UtcNow; public override DateTimeOffset GetUtcNow()=>now; internal void Advance(TimeSpan delta)=>now+=delta; }
}
