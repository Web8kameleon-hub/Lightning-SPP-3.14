using System.Net;
using System.Text.Json;
using LightningSPP.Licensing;
using Stripe;
using wwwmmm.Web;
using Xunit;

namespace LightningSPP.Tests;

/// <summary>Stripe API fixtures exercise application reconciliation; no production payment is made or claimed.</summary>
public sealed class PaidFulfillmentTests : IDisposable, IClassFixture<LongoFixture>
{
    private readonly Dictionary<string,string?> old;
    private readonly CustomerStore store;
    public PaidFulfillmentTests()
    {
        old=new[] {"STRIPE_SECRET_KEY","LSPP_STRIPE_PLANS","LSPP_LICENSE_SIGNING_KEY","LSPP_DATA_DIR"}.ToDictionary(n=>n,Environment.GetEnvironmentVariable);
        var directory=Path.Combine(Path.GetTempPath(),"lightning-paid-tests-"+Guid.NewGuid().ToString("N"));
        Environment.SetEnvironmentVariable("LSPP_DATA_DIR",directory);
        Environment.SetEnvironmentVariable("STRIPE_SECRET_KEY","sk_test_isolated_fixture_only");
        Environment.SetEnvironmentVariable("LSPP_LICENSE_SIGNING_KEY","isolated-fixture-signing-secret-32-plus-bytes");
        Environment.SetEnvironmentVariable("LSPP_STRIPE_PLANS","""{"price_fixture":{"Tier":"PayAsYouGo","ValidityDays":31,"Mode":"subscription"}}""");
        store=new CustomerStore(directory);
    }
    public void Dispose() { foreach(var pair in old) Environment.SetEnvironmentVariable(pair.Key,pair.Value); }
    private static Event Paid(string eventId="evt_fixture") => new() {Id=eventId,Type="invoice.paid",Data=new EventData {Object=new Invoice {Id="in_fixture"}}};
    private static string InvoiceJson(string price="price_fixture",string state="paid",bool modern=true) => JsonSerializer.Serialize(new {
        id="in_fixture",status=state,customer="cus_fixture",customer_email="buyer@example.test",livemode=false,
        subscription=modern ? null : "sub_fixture",parent=modern ? new {subscription_details=new {subscription="sub_fixture"}} : null,
        lines=new {has_more=false,data=new[] {new {quantity=1,price=modern ? null : new {id=price},pricing=new {price_details=new {price}},period=new {end=DateTimeOffset.UtcNow.AddDays(30).ToUnixTimeSeconds()}}}}
    });
    private static StripeGateway Gateway(string invoice,Func<int,bool>? fail=null,string subscriptionCustomer="cus_fixture",string subscriptionState="active") =>
        new(new HttpClient(new FixtureHandler(invoice,fail,subscriptionCustomer,subscriptionState)));
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task PaidInvoiceCreatesSignedLicenseOnceForBothStripeSchemas(bool modern)
    {
        var sends=0; var processor=new PaidFulfillment(store,Gateway(InvoiceJson(modern:modern)),(_,_,_,_)=> { sends++; return Task.FromResult((true,"")); });
        Assert.Equal(HttpStatusCode.OK,(await processor.ProcessAsync(Paid())).Status);
        var first=Assert.Single(store.Entitlements()); Assert.True(LicenseKey.Validate(first.LicenseKey,out var license)); Assert.NotNull(license);
        Assert.Equal(HttpStatusCode.OK,(await processor.ProcessAsync(Paid("evt_second_delivery"))).Status);
        Assert.Equal(first.LicenseKey,Assert.Single(new CustomerStore(Path.GetDirectoryName(store.DirectoryPath)!).Entitlements()).LicenseKey);
        Assert.Equal(1,sends);
    }
    [Fact]
    public async Task EmailFailureRetainsEntitlementAndSameEventRetriesWithoutIssuingAnotherKey()
    {
        var sends=0; var processor=new PaidFulfillment(store,Gateway(InvoiceJson()),(_,_,_,_)=>Task.FromResult((++sends>1,"test-only mail failure")));
        var failed=await processor.ProcessAsync(Paid()); Assert.Equal(HttpStatusCode.ServiceUnavailable,failed.Status);
        var first=Assert.Single(store.Entitlements()); Assert.Equal("FAILED",first.EmailState);
        var retry=await processor.ProcessAsync(Paid()); Assert.Equal(HttpStatusCode.OK,retry.Status);
        var final=Assert.Single(store.Entitlements()); Assert.Equal(first.LicenseKey,final.LicenseKey); Assert.Equal("PROVIDER_ACCEPTED",final.EmailState); Assert.Equal(2,sends);
    }
    [Fact]
    public async Task TransientStripeFailureIsNotMarkedCompleteAndRetryCanFulfill()
    {
        var processor=new PaidFulfillment(store,Gateway(InvoiceJson(),n=>n==1),(_,_,_,_)=>Task.FromResult((true,"")));
        Assert.Equal(HttpStatusCode.ServiceUnavailable,(await processor.ProcessAsync(Paid())).Status); Assert.Empty(store.Entitlements());
        Assert.Equal(HttpStatusCode.OK,(await processor.ProcessAsync(Paid())).Status); Assert.Single(store.Entitlements());
    }
    [Fact]
    public async Task UnsupportedPriceAndUnrelatedSubscriptionCustomerFailClosed()
    {
        var sends=0;
        var unknown=new PaidFulfillment(store,Gateway(InvoiceJson("price_unknown")),(_,_,_,_)=> { sends++; return Task.FromResult((true,"")); });
        Assert.Equal(HttpStatusCode.ServiceUnavailable,(await unknown.ProcessAsync(Paid())).Status); Assert.Empty(store.Entitlements());
        var unrelated=new PaidFulfillment(store,Gateway(InvoiceJson(),subscriptionCustomer:"cus_another"),(_,_,_,_)=> { sends++; return Task.FromResult((true,"")); });
        Assert.Equal(HttpStatusCode.ServiceUnavailable,(await unrelated.ProcessAsync(Paid())).Status); Assert.Empty(store.Entitlements()); Assert.Equal(0,sends);
    }
    [Fact]
    public async Task SubscriptionCheckoutCannotGrantSecondEntitlementAndUnpaidInvoiceCannotGrantAny()
    {
        var processor=new PaidFulfillment(store,new StripeGateway(new HttpClient(new SessionFixtureHandler())),(_,_,_,_)=>throw new Exception("Must not send"));
        var checkout=new Event {Id="evt_checkout_fixture",Type="checkout.session.completed",Data=new EventData {Object=new Stripe.Checkout.Session {Id="cs_fixture"}}};
        Assert.Equal(HttpStatusCode.OK,(await processor.ProcessAsync(checkout)).Status); Assert.Empty(store.Entitlements());
        checkout.Type="checkout.session.async_payment_succeeded";
        Assert.Equal(HttpStatusCode.OK,(await processor.ProcessAsync(checkout)).Status); Assert.Empty(store.Entitlements());
        var unpaid=new PaidFulfillment(store,Gateway(InvoiceJson(state:"open")),(_,_,_,_)=>throw new Exception("Must not send"));
        Assert.Equal(HttpStatusCode.OK,(await unpaid.ProcessAsync(Paid())).Status); Assert.Empty(store.Entitlements());
    }
    [Fact]
    public async Task SubscriptionEventReadsCurrentStripeStateInsteadOfTrustingStalePayload()
    {
        var processor=new PaidFulfillment(store,Gateway(InvoiceJson()),(_,_,_,_)=>Task.FromResult((true,"")));
        Assert.Equal(HttpStatusCode.OK,(await processor.ProcessAsync(Paid())).Status);
        var update=new Event {Id="evt_old_subscription_fixture",Type="customer.subscription.updated",Data=new EventData {Object=new Subscription {Id="sub_fixture",Status="active"}}};
        var reconcile=new PaidFulfillment(store,Gateway(InvoiceJson(),subscriptionState:"canceled"));
        Assert.Equal(HttpStatusCode.OK,(await reconcile.ProcessAsync(update)).Status);
        Assert.Equal("CANCELED",CustomerAccounts.State(Assert.Single(store.Entitlements()),store.Now));
    }
    private sealed class FixtureHandler(string invoice,Func<int,bool>? fail,string customer,string state) : HttpMessageHandler
    {
        private int calls;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken cancellationToken)
        {
            Assert.Equal(HttpMethod.Get,request.Method); Assert.Equal("api.stripe.com",request.RequestUri!.Host);
            if(fail?.Invoke(++calls)==true) return Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
            var body=request.RequestUri.AbsolutePath.Contains("invoices/") ? invoice : JsonSerializer.Serialize(new {id="sub_fixture",customer,status=state,livemode=false});
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) {Content=new StringContent(body)});
        }
    }
    private sealed class SessionFixtureHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) {Content=new StringContent("""{"id":"cs_fixture","mode":"subscription","payment_status":"paid","livemode":false}""")});
    }
}
