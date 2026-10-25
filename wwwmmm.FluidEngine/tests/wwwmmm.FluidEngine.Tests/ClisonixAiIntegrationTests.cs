using Xunit;
using wwwmmm.Workflow;
using wwwmmm.FluidEngine.Core.Context;
using wwwmmm.FluidEngine.Core.Pipelines;

namespace wwwmmm.FluidEngine.Tests;

public class ClisonixAiIntegrationTests
{
    [Fact]
    public async Task ModelGateway_FullPipeline_WithRealOllama()
    {
        // 1. Krijo ModelGateway me modelin clisonix-ocean në Ollama lokal
        var route = new ModelRoute("clisonix", "http://localhost:11434", "clisonix-ocean:v2");
        var gateway = new ModelGateway(new[] { route });

        // 2. Krijo PipelineContext
        var context = new PipelineContext();

        // 3. Krijo hallkën e AI (ModelGatewayFluidStep)
        var aiStep = new ModelGatewayFluidStep(gateway, "clisonix");

        // 4. Dërgo një pyetje reale te modeli
        var prompt = "Çfarë është Lightning SPP? Përgjigju shkurt në shqip.";
        var response = await aiStep.ExecuteAsync(prompt, context, CancellationToken.None);

        // 5. Verifiko që ka përgjigje reale (Never Fake)
        Assert.False(string.IsNullOrWhiteSpace(response), "AI ktheu përgjigje bosh!");
        Assert.True(response.Length > 10, "AI ktheu përgjigje shumë të shkurtër.");
        Assert.Single(context.StepTraces);
        Assert.Equal("ModelGateway/AI", context.StepTraces[0].StepName);
        Assert.True(context.StepTraces[0].Success, "AI hapi dështoi!");
    }
}
