using System.Text;
using System.Text.Json;
using wwwmmm.Classify;
using wwwmmm.Crm;
using wwwmmm.Workflow;
using wwwmmm.Web;
using Xunit;

namespace LightningSPP.Tests;

public sealed class CrmClassificationTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "spp-crm-" + Guid.NewGuid().ToString("N"));
    private WorkspaceScope Scope => new(root);
    private static Taxonomy Taxonomy => new("reviewed-test-taxonomy", "1", "unclassified", 2,
        [new("invoice", "Test rule", ["invoice", "amount due"], [], 10)]);
    public void Dispose() { if (Directory.Exists(root)) Directory.Delete(root, true); }

    [Fact]
    public async Task RecordsAuditAndActualDocumentSurviveReopeningWithoutSeedData()
    {
        var scope = Scope; var crm = new CrmService(scope);
        Assert.Equal(0, crm.Summary().Customers);
        var customer = await crm.CreateCustomerAsync("Fixture Customer", "fixture@example.test", "Fixture Co");
        Assert.Equal(customer, await crm.CreateCustomerAsync(customer.Name, "FIXTURE@example.test", customer.Company));
        await Assert.ThrowsAsync<InvalidOperationException>(() => crm.CreateCustomerAsync("Conflict", customer.Email, null));
        await crm.AddContactAsync(customer.Id, "Contact", "contact@example.test", null, "reviewer");
        await crm.SetStatusAsync(customer.Id, "inactive");
        var path = await scope.ImportAsync("invoice.txt", "Invoice amount due"u8.ToArray(), default);
        var input = await scope.ReadAsync(path, default);
        var linked = await crm.LinkDocumentAsync(customer.Id, path, input.Source.Sha256, "invoice", "utf8");
        Assert.True(linked.Created);
        Assert.False((await crm.LinkDocumentAsync(customer.Id, path, input.Source.Sha256, "invoice", "utf8")).Created);
        var reopened = new CrmService(new(root));
        var detail = reopened.GetDetail(customer.Id);
        Assert.Equal("inactive", detail.Customer.Status);
        Assert.Single(detail.Contacts); Assert.Single(detail.Documents); Assert.Equal(4, detail.Activities.Count);
        Assert.Equal(new CrmSummary(1, 0, 1, 1, 0, 4), reopened.Summary());
        Assert.False(File.Exists(Path.Combine(root, ".lightning", "crm", "transaction.json")));
    }

    [Fact]
    public async Task DocumentLinkRejectsInventedHashesChangedContentAndTraversal()
    {
        var scope = Scope; var crm = new CrmService(scope);
        var customer = await crm.CreateCustomerAsync("Fixture", "fixture@example.test", null);
        var path = await scope.ImportAsync("source.txt", "actual document"u8.ToArray(), default);
        await Assert.ThrowsAsync<IOException>(() => crm.LinkDocumentAsync(customer.Id, path, new string('0', 64), null, "utf8"));
        var input = await scope.ReadAsync(path, default);
        await crm.LinkDocumentAsync(customer.Id, path, input.Source.Sha256, null, "utf8");
        await File.WriteAllTextAsync(scope.Resolve(path), "changed");
        var changed = await scope.ReadAsync(path, default);
        await Assert.ThrowsAsync<InvalidOperationException>(() => crm.LinkDocumentAsync(customer.Id, path, changed.Source.Sha256, null, "utf8"));
        await Assert.ThrowsAsync<ArgumentException>(() => crm.LinkDocumentAsync(customer.Id, "../escape", input.Source.Sha256, null, "none"));
        Assert.Single(crm.GetDetail(customer.Id).Documents);
    }

    [Fact]
    public async Task SeparateWorkspaceCannotReadCustomerAndCancelledWriteCreatesNothing()
    {
        var crm = new CrmService(Scope);
        using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => crm.CreateCustomerAsync("Fixture", "cancelled@example.test", null, token: cancelled.Token));
        Assert.Equal(0, crm.Summary().Customers);
        var customer = await crm.CreateCustomerAsync("Fixture", "fixture@example.test", null);
        var otherRoot = Path.Combine(root, "other");
        var other = new CrmService(new(otherRoot));
        Assert.Throws<ArgumentException>(() => other.RequireCustomer(customer.Id));
        Assert.Equal(0, other.Summary().Customers);
    }

    [Fact]
    public async Task LeaseRejectsConcurrentOwnerAndStaleUpdateCannotOverwrite()
    {
        var scope = Scope; var crm = new CrmService(scope);
        var customer = await crm.CreateCustomerAsync("Fixture", "fixture@example.test", null);
        using (var lease = new FileStream(Path.Combine(root, ".lightning", "crm", "writer.lease"), FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            Assert.Throws<IOException>(() => new CrmService(scope));
        var oldHash = WorkflowJson.Hash(JsonSerializer.SerializeToUtf8Bytes(customer, CrmJson.Options));
        await crm.SetStatusAsync(customer.Id, "inactive");
        var repository = new CrmRepository(scope);
        await Assert.ThrowsAsync<IOException>(() => repository.CommitAsync([
            CrmRepository.Record("customers", customer.Id, customer with { Company = "Stale change" }, oldHash)], default));
        Assert.Equal("inactive", crm.RequireCustomer(customer.Id).Status);
        Assert.Null(crm.RequireCustomer(customer.Id).Company);
    }

    [Fact]
    public async Task PendingJournalRecoversRecordAndAuditBeforeReading()
    {
        var scope = Scope; var crm = new CrmService(scope);
        var customer = await crm.CreateCustomerAsync("Fixture", "fixture@example.test", null);
        var changed = customer with { Status = "inactive" };
        var activity = new CrmActivity("act_recovery", customer.Id, "customer-status", "Fixture recovery", customer.UpdatedUnix);
        var writes = new[] { CrmRepository.Record("customers", customer.Id, changed), CrmRepository.Record("activities", activity.Id, activity) };
        // Test fixture for interruption after the journal was committed but before publication.
        var journal = Path.Combine(root, ".lightning", "crm", "transaction.json");
        await File.WriteAllBytesAsync(journal, JsonSerializer.SerializeToUtf8Bytes(new { version = 1, writes }, CrmJson.Options));
        var reopened = new CrmService(new(root));
        Assert.Equal("inactive", reopened.RequireCustomer(customer.Id).Status);
        Assert.Contains(reopened.GetDetail(customer.Id).Activities, x => x.Id == activity.Id);
        Assert.False(File.Exists(journal));
    }

    [Fact]
    public async Task CorruptRecordsAndCorruptJournalNeverBecomeEmptySuccess()
    {
        var crm = new CrmService(Scope);
        var customer = await crm.CreateCustomerAsync("Fixture", "fixture@example.test", null);
        var directory = Path.Combine(root, ".lightning", "crm");
        await File.WriteAllTextAsync(Path.Combine(directory, "customers", customer.Id + ".json"), "{}");
        Assert.Throws<IOException>(() => crm.Summary());
        await File.WriteAllTextAsync(Path.Combine(directory, "transaction.json"), "broken");
        Assert.Throws<IOException>(() => new CrmService(Scope));
        Assert.Equal("broken", await File.ReadAllTextAsync(Path.Combine(directory, "transaction.json")));
    }

    [Fact]
    public async Task RecoveryRejectsTraversalBeforePublishingAnyRecord()
    {
        var scope = Scope; _ = new CrmService(scope);
        var customer = new CrmCustomer("cus_fixture", "Fixture", "fixture@example.test", null, "active", 1, 1);
        var writes = new[] { CrmRepository.Record("customers", customer.Id, customer), CrmRepository.Record("../outside", customer.Id, customer) };
        await File.WriteAllBytesAsync(Path.Combine(root, ".lightning", "crm", "transaction.json"),
            JsonSerializer.SerializeToUtf8Bytes(new { version = 1, writes }, CrmJson.Options));
        Assert.Throws<IOException>(() => new CrmService(scope));
        Assert.Empty(Directory.GetFiles(Path.Combine(root, ".lightning", "crm", "customers")));
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(0)]
    public void InvalidClassificationThresholdIsRejected(double threshold) =>
        Assert.Throws<ArgumentException>(() => new DocumentClassifier(Taxonomy with { MinimumScore = threshold }));

    [Fact]
    public void ClassifierUsesRuleEvidenceWithoutProbabilityAndSnapshotsTaxonomy()
    {
        var taxonomy = Taxonomy; var classifier = new DocumentClassifier(taxonomy);
        taxonomy.Categories[0].Keywords[0] = "modified";
        classifier.Taxonomy.Categories[0].Keywords[0] = "also modified";
        var result = classifier.Classify("source.txt", "INVOICE: amount due");
        Assert.Equal("invoice", result.Category); Assert.Equal(2, result.Score);
        Assert.Equal(new[] { "text:invoice", "text:amount due" }, result.Evidence);
        Assert.True(classifier.Classify("source.txt", "syntax and uninvoiced").IsFallback);
    }

    [Fact]
    public async Task WorkflowPersistsClassificationEvidenceAndRequiresSeparateCrmConsent()
    {
        var scope = Scope;
        var path = await scope.ImportCanonicalAsync("source.txt", "Invoice amount due"u8.ToArray(), default);
        var taxonomy = await scope.ImportCanonicalAsync("taxonomy.json", JsonSerializer.SerializeToUtf8Bytes(Taxonomy, ClassifyJson.Options), default);
        var registry = WorkflowApi.Registry(new([]));
        var permissions = new HashSet<string>(["files.read", "reports.write"]);
        var args = WorkflowJson.Value(new { path, taxonomy });
        var result = await registry.Get("documents.classify").ExecuteAsync(args, new(scope, "fixturejob", "classify", permissions), default);
        Assert.Equal("invoice", result.Data.GetProperty("category").GetString());
        Assert.Equal("utf8", result.Data.GetProperty("textSource").GetString());
        Assert.True(result.Data.GetProperty("requiresReview").GetBoolean());
        Assert.Equal(2, result.Sources.Length);
        var manifestPath = result.Data.GetProperty("evidencePath").GetString()!;
        using var manifest = JsonDocument.Parse(await File.ReadAllBytesAsync(scope.Resolve(manifestPath)));
        Assert.Equal(result.Data.GetProperty("sha256").GetString(), manifest.RootElement.GetProperty("data").GetProperty("sha256").GetString());
        var plan = new WorkflowPlan("CRM", [new("customer", "crm.create-customer", WorkflowJson.Value(new { name = "Fixture", email = "fixture@example.test" }), [])]);
        Assert.Throws<UnauthorizedAccessException>(() => registry.Validate(plan, permissions));
        permissions.Add("crm.write"); _ = registry.Validate(plan, permissions);
        var completed = await registry.Get("crm.create-customer").ExecuteAsync(plan.Steps[0].Arguments, new(scope, "fixturejob", "customer", permissions), default);
        Assert.Equal("fixture@example.test", completed.Data.GetProperty("email").GetString());
    }

    [Fact]
    public async Task CanonicalImportAndClassifiedFoldersStorePayloadOnlyOnce()
    {
        var scope = Scope; var bytes = "one canonical document"u8.ToArray();
        var path = await scope.ImportCanonicalAsync("first.txt", bytes, default);
        var second = await scope.ImportCanonicalAsync("second.txt", bytes, default);
        Assert.Equal(path, await scope.ImportCanonicalAsync("first.txt", bytes, default));
        var input = await scope.ReadAsync(path, default);
        Assert.Equal(bytes, input.Bytes);
        Assert.Equal(bytes, (await scope.ReadAsync(second, default)).Bytes);
        var originalDirectory = Path.Combine(root, ".lightning", "originals");
        Assert.Single(Directory.GetFiles(originalDirectory));
        Assert.False(File.Exists(scope.Resolve(path, false)));
        var registry = WorkflowApi.Registry(new([]));
        var context = new ToolContext(scope, "fixturejob", "file", new HashSet<string>(["files.read", "reports.write"]));
        var filed = await registry.Get("documents.file-classified").ExecuteAsync(
            WorkflowJson.Value(new { path, category = "reviewed", sha256 = input.Source.Sha256 }), context, default);
        Assert.False(filed.Data.GetProperty("payloadCopied").GetBoolean());
        Assert.Single(Directory.GetFiles(originalDirectory));
        var listing = await registry.Get("files.list").ExecuteAsync(WorkflowJson.Value(new { directory = "documents" }), context, default);
        var listed = listing.Data.GetProperty("files").EnumerateArray().Select(x => x.GetString()).ToArray();
        Assert.Contains(path, listed); Assert.Contains(second, listed);
        await File.WriteAllTextAsync(Path.Combine(originalDirectory, input.Source.Sha256), "corrupted");
        await Assert.ThrowsAsync<IOException>(() => scope.ReadAsync(path, default));
    }

    [Fact]
    public async Task FilmMemoryUsesExistingPipelineAndRetainsOnlyExecutionMetadata()
    {
        var scope = Scope; var path = await scope.ImportCanonicalAsync("source.txt", "private fixture payload"u8.ToArray(), default);
        var registry = WorkflowApi.Registry(new([])); var runner = new WorkflowRunner(registry);
        var permissions = new HashSet<string>(["files.read"]);
        var job = await runner.CreateAsync(scope, "fixture-owner", new("read source", [new("read", "files.read", WorkflowJson.Value(new { path }), [])]), permissions, default);
        var result = await runner.RunAsync(scope, "fixture-owner", job.Id, permissions, default);
        Assert.Equal("SUCCEEDED", result.State);
        var filmPath = Path.Combine(root, ".lightning", "film", job.Id + "-read.json");
        var film = await File.ReadAllTextAsync(filmPath);
        Assert.DoesNotContain("private fixture payload", film); Assert.DoesNotContain("source.txt", film);
        using var parsed = JsonDocument.Parse(film);
        Assert.Equal(result.Steps[0].ResultHash, parsed.RootElement.GetProperty("resultHash").GetString());
        await runner.RunAsync(scope, "fixture-owner", job.Id, permissions, default);
        Assert.Equal(film, await File.ReadAllTextAsync(filmPath));
        Assert.Single(Directory.GetFiles(Path.GetDirectoryName(filmPath)!));
    }
}
