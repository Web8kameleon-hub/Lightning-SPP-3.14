using System.Net;
using System.Text.Json;
using wwwmmm.Workflow;
using Xunit;

namespace LightningSPP.Tests;

public sealed class ModelQualityPolicyTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;
    private const string Digest = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    private const string OtherDigest = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
    private const string Scope = "translation-reference-fidelity";
    private static ModelQualityEvidence E(string model, double score, string language = "sq") =>
        new(model,Digest,language,Scope,score,3,"chrF2","test-fixture-signature",OtherDigest,Now.AddMinutes(-5),Now.AddDays(1));
    private static HashSet<string> Permissions => new(["model.invoke"]);

    [Fact]
    public void LargeHistoryDoesNotPermanentlyBlockFutureQuestionsOrRemoveSafety()
    {
        ConversationMessage[] messages=[new("system","Safety and grounding"),new("user",new string('x',6000)),
            new("assistant",new string('y',6000)),new("user","Current question and evidence")];
        var bounded=ModelGateway.FitQualityConversation(messages,6144);
        Assert.Equal([messages[0],messages[^1]],bounded);
        Assert.Equal(4,messages.Length);
        Assert.Throws<ArgumentException>(() => ModelGateway.FitQualityConversation(
            [new("system","Safety"),new("user",new string('x',7000))],6144));
    }

    [Fact]
    public void LatestLowOrExpiredMeasurementCannotResurrectOlderHighScore()
    {
        var good = E("fixture-model",90) with { EvaluatedAt = Now.AddHours(-2) };
        var low = E("fixture-model",10);
        Assert.Null(new ModelQualityPolicy([good,low],40).Evidence("fixture-model",Digest,"sq-AL",Scope,Now));
        var expired = low with { Score=95, EvaluatedAt=Now.AddMinutes(-10),ValidUntil=Now.AddMinutes(-1) };
        Assert.Null(new ModelQualityPolicy([good,expired],40).Evidence("fixture-model",Digest,"sq",Scope,Now));
    }

    [Fact]
    public void MetadataLookupDoesNotTurnUnknownLanguagesOrChangedModelsIntoAnEnglishFallback()
    {
        var policy=new ModelQualityPolicy([E("fixture-model",80)],40);
        Assert.NotNull(policy.Evidence("fixture-model",Digest,"sq-AL",Scope,Now));
        Assert.Null(policy.Evidence("fixture-model",OtherDigest,"sq",Scope,Now));
        Assert.Null(policy.Evidence("fixture-model",Digest,"ja",Scope,Now));
        Assert.Null(policy.Evidence("fixture-model",Digest,"und",Scope,Now));
        Assert.Null(policy.Evidence("fixture-model",Digest,"sq","unmeasured-task",Now));
        Assert.Throws<ArgumentException>(() => new ModelQualityPolicy([E("one",80),E("two",90) with { Signature="different-cohort" }],40));
    }

    [Fact]
    public async Task ChoosesMeasuredQualityInsteadOfRouteOrderModelNameOrResponseLength()
    {
        var handler = new Provider();
        var gateway = new ModelGateway([new("first","https://first.example.test","low"),new("second","https://second.example.test","high")],
            new HttpClient(handler),new([E("low",45),E("high",80)],40));
        var result=await gateway.ChatBestAsync([new("user","test-only question")],"sq",Scope,Permissions,default);
        Assert.Equal("high",result.Selection.Identity.Model);
        Assert.Equal(80,result.Selection.Evidence.Score);
        Assert.Equal(["high"],handler.Inferences);
    }

    [Fact]
    public async Task RemotePermissionIsCheckedBeforeContactingTheRemoteProvider()
    {
        var handler=new Provider();
        var gateway=new ModelGateway([new("remote","https://remote.example.test","remote",Remote:true),new("local","http://localhost","local")],
            new HttpClient(handler),new([E("remote",99),E("local",60)],40));
        var result=await gateway.ChatBestAsync([new("user","test-only")],"sq",Scope,Permissions,default);
        Assert.Equal("local",result.Selection.Identity.Model);
        Assert.DoesNotContain(handler.Hosts,h=>h=="remote.example.test");
    }

    [Fact]
    public async Task AuthorizationFailureDoesNotFallThroughToAnotherProvider()
    {
        var handler=new Provider { DenyInference=true };
        var gateway=new ModelGateway([new("one","https://first.example.test","high"),new("two","https://second.example.test","low")],
            new HttpClient(handler),new([E("high",90),E("low",50)],40));
        var error=await Assert.ThrowsAsync<HttpRequestException>(() => gateway.ChatBestAsync([new("user","test-only")],"sq",Scope,Permissions,default));
        Assert.Equal(HttpStatusCode.Forbidden,error.StatusCode);
        Assert.Single(handler.Inferences);
    }

    [Fact]
    public async Task ModelDigestChangeIsRejectedBeforeSendingConversationContent()
    {
        var handler=new Provider { ChangeDigest=true };
        var gateway=new ModelGateway([new("one","https://first.example.test","high")],new HttpClient(handler),new([E("high",90)],40));
        await Assert.ThrowsAsync<InvalidOperationException>(() => gateway.ChatBestAsync([new("user","private-test-fixture")],"sq",Scope,Permissions,default));
        Assert.Empty(handler.Inferences);
    }

    private sealed class Provider : HttpMessageHandler
    {
        public bool DenyInference,ChangeDigest;
        private int catalogs;
        public List<string> Inferences { get; }=[];
        public List<string> Hosts { get; }=[];
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken token)
        {
            Hosts.Add(request.RequestUri!.Host);
            object body;
            if(request.RequestUri.AbsolutePath=="/api/tags")
            {
                var digest=ChangeDigest&&++catalogs>1?OtherDigest:Digest;
                body=new {models=new[]{"low","high","local","remote"}.Select(name=>new{name,digest})};
            }
            else if(request.RequestUri.AbsolutePath=="/api/show") body=new{capabilities=new[]{"completion"}};
            else
            {
                using var data=JsonDocument.Parse(await request.Content!.ReadAsStringAsync(token));
                var model=data.RootElement.GetProperty("model").GetString()!;Inferences.Add(model);
                if(DenyInference)return new(HttpStatusCode.Forbidden);
                body=new{model,done=true,done_reason="stop",message=new{content="Test fixture only"}};
            }
            return new(HttpStatusCode.OK){Content=new StringContent(JsonSerializer.Serialize(body),System.Text.Encoding.UTF8,"application/json")};
        }
    }
}
