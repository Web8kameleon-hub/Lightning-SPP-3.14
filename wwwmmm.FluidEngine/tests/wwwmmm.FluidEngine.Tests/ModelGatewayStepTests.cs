using Xunit;
using wwwmmm.Workflow;
using wwwmmm.FluidEngine.Core.Contracts;

namespace wwwmmm.FluidEngine.Tests;

public class ModelGatewayStepTests
{
    [Fact]
    public void ModelGatewayStep_ImplementsIFluidStep()
    {
        // Kontrollon që ModelGatewayFluidStep zbaton kontratën IFluidStep<string, string>
        var stepType = typeof(ModelGatewayFluidStep);
        var interfaceType = typeof(IFluidStep<string, string>);
        
        Assert.True(interfaceType.IsAssignableFrom(stepType),
            "ModelGatewayFluidStep duhet të implementojë IFluidStep<string, string>");
    }
}
