using wwwmmm.Services;
using wwwmmm.Services.Implementation;
using Xunit;

namespace LightningSPP.Tests;

public sealed class ClisonixOnnxContractTests
{
    [Fact]
    public async Task Clisonix_WithoutModel_FailsClosed_WithoutFallback()
    {
        var oldModel = Environment.GetEnvironmentVariable("LSPP_ONNX_MODEL_PATH");
        var oldShape = Environment.GetEnvironmentVariable("LSPP_ONNX_INPUT_SHAPE");

        try
        {
            Environment.SetEnvironmentVariable("LSPP_ONNX_MODEL_PATH", null);
            Environment.SetEnvironmentVariable("LSPP_ONNX_INPUT_SHAPE", null);

            var service = new RealAIProcessor();

            var result = await service.ProcessAsync(
                new byte[16],
                new ProcessingOptions(
                    ProcessingEngine.Clisonix,
                    UseAI: false,
                    UseClisonix: false,
                    OptimizationLevel: 3,
                    UseStigmaMemory: false));

            Assert.False(result.Success);
            Assert.Empty(result.ProcessedData);
            Assert.Contains(
                "LSPP_ONNX_MODEL_PATH",
                result.Error ?? string.Empty,
                StringComparison.Ordinal);
        }
        finally
        {
            Environment.SetEnvironmentVariable("LSPP_ONNX_MODEL_PATH", oldModel);
            Environment.SetEnvironmentVariable("LSPP_ONNX_INPUT_SHAPE", oldShape);
        }
    }

    [Fact]
    public async Task UseAI_Alone_DoesNotActivateClisonixOnnx()
    {
        var oldModel = Environment.GetEnvironmentVariable("LSPP_ONNX_MODEL_PATH");
        var oldShape = Environment.GetEnvironmentVariable("LSPP_ONNX_INPUT_SHAPE");

        try
        {
            Environment.SetEnvironmentVariable("LSPP_ONNX_MODEL_PATH", null);
            Environment.SetEnvironmentVariable("LSPP_ONNX_INPUT_SHAPE", null);

            var service = new RealAIProcessor();

            var result = await service.ProcessAsync(
                new byte[16],
                new ProcessingOptions(
                    ProcessingEngine.CLX,
                    UseAI: true,
                    UseClisonix: false,
                    OptimizationLevel: 3,
                    UseStigmaMemory: false));

            Assert.True(result.Success);
            Assert.NotEmpty(result.ProcessedData);
            Assert.Equal("CLX", result.Metadata.Engine);
            Assert.DoesNotContain(
                "ONNX",
                result.Metadata.Engine,
                StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Environment.SetEnvironmentVariable("LSPP_ONNX_MODEL_PATH", oldModel);
            Environment.SetEnvironmentVariable("LSPP_ONNX_INPUT_SHAPE", oldShape);
        }
    }
    [Fact]
    public async Task Hybrid_WithClisonix_FailsClosed_InsteadOfFeedingBrotliToOnnx()
    {
        var service = new RealAIProcessor();

        var result = await service.ProcessAsync(
            new byte[64],
            new ProcessingOptions(
                ProcessingEngine.Hybrid,
                UseAI: true,
                UseClisonix: true,
                OptimizationLevel: 3,
                UseStigmaMemory: false));

        Assert.False(result.Success);
        Assert.Empty(result.ProcessedData);
        Assert.Contains(
            "Clisonix processing is unavailable",
            result.Error ?? string.Empty,
            StringComparison.Ordinal);
    }
}