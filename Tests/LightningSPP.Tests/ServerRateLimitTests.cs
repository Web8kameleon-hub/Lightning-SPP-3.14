using wwwmmm.Web;
using Xunit;

namespace LightningSPP.Tests;

public sealed class ServerRateLimitTests
{
    [Fact]
    public void ActualServerLimitersEnforceIndependentAtomicBudgets()
    {
        var ip = "test-" + Guid.NewGuid().ToString("N");
        var general = 0;
        var processing = 0;
        Parallel.For(0, 100, _ =>
        {
            if (!LicenseApiServer.IsRateLimited(ip)) Interlocked.Increment(ref general);
            if (!LicenseApiServer.IsTryRateLimited(ip)) Interlocked.Increment(ref processing);
        });
        Assert.Equal(30, general);
        Assert.Equal(10, processing);
    }

    [Fact]
    public void CapacityIsBoundedAndExpiredClientsReleaseTheirSlots()
    {
        var limiter = new RequestRateLimiter(2, TimeSpan.FromSeconds(5), maxClients: 2);
        var now = DateTimeOffset.UtcNow;
        Assert.False(limiter.IsLimited("a", now));
        Assert.False(limiter.IsLimited("b", now));
        Assert.True(limiter.IsLimited("c", now));
        Assert.False(limiter.IsLimited("c", now.AddSeconds(5)));
        Assert.False(limiter.IsLimited("c", now.AddSeconds(5)));
        Assert.True(limiter.IsLimited("c", now.AddSeconds(5)));
    }
}
