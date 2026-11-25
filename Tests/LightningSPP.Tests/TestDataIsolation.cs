using System.Runtime.CompilerServices;

namespace LightningSPP.Tests;

internal static class TestDataIsolation
{
    [ModuleInitializer]
    internal static void Initialize()
    {
        // Set before LicenseManager or account providers initialize: tests must never
        // activate a fixture key in the developer's real license/account directory.
        var root = Path.Combine(Path.GetTempPath(), "lightning-test-state-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        Environment.SetEnvironmentVariable("LSPP_LICENSE_DATA_DIR", Path.Combine(root, "license"));
        Environment.SetEnvironmentVariable("LSPP_DATA_DIR", Path.Combine(root, "accounts"));
    }
}
