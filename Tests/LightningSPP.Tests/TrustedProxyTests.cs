using System.Net;
using wwwmmm.Web;
using Xunit;

namespace LightningSPP.Tests;

public class TrustedProxyTests
{
    [Fact]
    public void OnlyLoopbackAndExplicitProxyAddressesAreTrusted()
    {
        var previous = Environment.GetEnvironmentVariable("LSPP_TRUSTED_PROXY_IPS");
        try
        {
            Environment.SetEnvironmentVariable("LSPP_TRUSTED_PROXY_IPS", "172.18.0.5,invalid");
            Assert.True(LicenseApiServer.IsTrustedProxy(IPAddress.Loopback));
            Assert.True(LicenseApiServer.IsTrustedProxy(IPAddress.IPv6Loopback));
            Assert.True(LicenseApiServer.IsTrustedProxy(IPAddress.Parse("172.18.0.5")));
            Assert.True(LicenseApiServer.IsTrustedProxy(IPAddress.Parse("::ffff:172.18.0.5")));
            Assert.False(LicenseApiServer.IsTrustedProxy(IPAddress.Parse("172.18.0.6")));
            Assert.False(LicenseApiServer.IsTrustedProxy(IPAddress.Parse("198.51.100.1")));
            Environment.SetEnvironmentVariable("LSPP_TRUSTED_PROXY_IPS", null);
            Assert.False(LicenseApiServer.IsTrustedProxy(IPAddress.Parse("172.18.0.5")));
        }
        finally { Environment.SetEnvironmentVariable("LSPP_TRUSTED_PROXY_IPS", previous); }
    }
}
