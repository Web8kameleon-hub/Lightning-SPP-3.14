using wwwmmm.Configuration;
using wwwmmm.Services.Imaging;
using Xunit;

namespace LightningSPP.Tests;

/// <summary>
/// Verifies that the hybrid image engine selection (L=Skia, R=ImageSharp) is
/// explicit, configurable, and has no silent fallback — matching the documented
/// intent of ImageEngineAdapter.
/// </summary>
public class ImageEngineSelectionTests
{
    [Fact]
    public void FromEnvironment_NoVariableSet_DefaultsToSkia()
    {
        var original = Environment.GetEnvironmentVariable(ImageEngineSelection.EnvironmentVariable);
        try
        {
            Environment.SetEnvironmentVariable(ImageEngineSelection.EnvironmentVariable, null);

            var engine = ImageEngineSelection.FromEnvironment();

            Assert.Equal(ImageEngine.Skia, engine);
        }
        finally
        {
            Environment.SetEnvironmentVariable(ImageEngineSelection.EnvironmentVariable, original);
        }
    }

    [Theory]
    [InlineData("Skia", ImageEngine.Skia)]
    [InlineData("ImageSharp", ImageEngine.ImageSharp)]
    [InlineData("imagesharp", ImageEngine.ImageSharp)]
    [InlineData("SKIA", ImageEngine.Skia)]
    public void FromEnvironment_ValidValue_ReturnsMatchingEngine(string value, ImageEngine expected)
    {
        var original = Environment.GetEnvironmentVariable(ImageEngineSelection.EnvironmentVariable);
        try
        {
            Environment.SetEnvironmentVariable(ImageEngineSelection.EnvironmentVariable, value);

            var engine = ImageEngineSelection.FromEnvironment();

            Assert.Equal(expected, engine);
        }
        finally
        {
            Environment.SetEnvironmentVariable(ImageEngineSelection.EnvironmentVariable, original);
        }
    }

    [Fact]
    public void FromEnvironment_InvalidValue_ThrowsInsteadOfCoercing()
    {
        var original = Environment.GetEnvironmentVariable(ImageEngineSelection.EnvironmentVariable);
        try
        {
            Environment.SetEnvironmentVariable(ImageEngineSelection.EnvironmentVariable, "Vips");

            Assert.Throws<InvalidOperationException>(() => ImageEngineSelection.FromEnvironment());
        }
        finally
        {
            Environment.SetEnvironmentVariable(ImageEngineSelection.EnvironmentVariable, original);
        }
    }

    [Fact]
    public void ServiceFactory_DefaultConfiguration_ReturnsSkiaEngine()
    {
        ServiceFactory.Configure(LightningConfiguration.Default);
        try
        {
            var service = ServiceFactory.CreateScanService();

            Assert.IsType<wwwmmm.Services.Implementation.RealImageProcessor>(service);
        }
        finally
        {
            ServiceFactory.Configure(LightningConfiguration.Default);
        }
    }

    [Fact]
    public void ServiceFactory_ImageSharpConfigured_ReturnsImageSharpEngine()
    {
        ServiceFactory.Configure(new LightningConfiguration { ImageEngine = ImageEngine.ImageSharp });
        try
        {
            var service = ServiceFactory.CreateScanService();

            Assert.IsType<wwwmmm.Services.Implementation.FastScanService>(service);
        }
        finally
        {
            ServiceFactory.Configure(LightningConfiguration.Default);
        }
    }
}
