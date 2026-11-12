using System.IO.Compression;
using System.Text.Json;
using LightningSPP.Licensing;
using wwwmmm.Batch;
using wwwmmm.Packaging;
using wwwmmm.Provenance;
using Xunit;

namespace LightningSPP.Tests;

public sealed class ReleaseSafetyRegressionTests
{
    private static string DirectoryForTest()
    {
        var path = Path.Combine(Path.GetTempPath(), "spp-release-safety-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    [Fact]
    public async Task MissingWorkspaceCannotProduceSuccessfulVerificationOrReport()
    {
        var root = DirectoryForTest();
        try
        {
            var missing = Path.Combine(root, "missing");
            Assert.Equal(1, await wwwmmm.Flow.FlowBatch.VerifyAsync(["flow", "verify", missing], default));
            Assert.Equal(1, wwwmmm.Flow.FlowBatch.Report(["flow", "report", missing]));
            Assert.False(Directory.Exists(missing));
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public async Task PackagePreservesDestinationAndRejectsCollidingMemberNames()
    {
        var root = DirectoryForTest();
        try
        {
            var first = Path.Combine(root, "a", "same.txt");
            var second = Path.Combine(root, "b", "same.txt");
            Directory.CreateDirectory(Path.GetDirectoryName(first)!);
            Directory.CreateDirectory(Path.GetDirectoryName(second)!);
            await File.WriteAllTextAsync(first, "first source");
            await File.WriteAllTextAsync(second, "second source");
            var output = Path.Combine(root, "existing.zip");
            await File.WriteAllTextAsync(output, "preserve me");
            await Assert.ThrowsAsync<IOException>(() => DocumentPackager.CreateAsync(output, [first]));
            Assert.Equal("preserve me", await File.ReadAllTextAsync(output));
            Assert.Empty(Directory.GetFiles(root, "*.tmp"));
            var newOutput = Path.Combine(root, "new.zip");
            await Assert.ThrowsAsync<ArgumentException>(() => DocumentPackager.CreateAsync(newOutput, [first, second]));
            Assert.False(File.Exists(newOutput));
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public async Task PackageVerifierRejectsDuplicateArchiveEntries()
    {
        var root = DirectoryForTest();
        try
        {
            var member = Path.Combine(root, "a.txt");
            await File.WriteAllTextAsync(member, "source");
            var output = Path.Combine(root, "archive.zip");
            await DocumentPackager.CreateAsync(output, [member]);
            using (var archive = ZipFile.Open(output, ZipArchiveMode.Update))
            {
                using var writer = new StreamWriter(archive.CreateEntry("a.txt").Open());
                writer.Write("duplicate");
            }
            var result = await DocumentPackager.VerifyAsync(output);
            Assert.False(result.IsValid);
            Assert.Contains(result.Problems, p => p.Contains("duplicate", StringComparison.Ordinal));
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public async Task TrailReviewsRequireRealExtractionAndKeepItsRulesIdentity()
    {
        var root = DirectoryForTest();
        try
        {
            var path = Path.Combine(root, "trail.jsonl");
            var trail = ProvenanceTrail.Open(path);
            await trail.AppendExtractionAsync("doc1", "rules1", "r", "total", "10", 1, 0);
            await trail.AppendExtractionAsync("doc2", "rules2", "r", "total", "20", 2, 0);
            var review = await trail.AppendReviewAsync("doc1", "r", "total", "10", 1, 0, "reviewer", ReviewDecision.Accepted);
            Assert.Equal("rules1", review.RuleSetSha256);
            await Assert.ThrowsAsync<ArgumentException>(() => trail.AppendReviewAsync("doc1", "r", "total", "999", 1, 0, "reviewer", ReviewDecision.Accepted));
            Assert.True((await trail.VerifyAsync()).IsValid);
            var lines = await File.ReadAllLinesAsync(path);
            var record = JsonSerializer.Deserialize<ProvenanceRecord>(lines[0])!;
            lines[0] = JsonSerializer.Serialize(record with { Value = "tampered" });
            await File.WriteAllLinesAsync(path, lines);
            await Assert.ThrowsAsync<InvalidOperationException>(() => trail.AppendExtractionAsync("doc3", "rules", "r", "total", "30", 1, 0));
            Assert.Equal(lines, await File.ReadAllLinesAsync(path));
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public async Task BatchRefusesConcurrentWriterBeforeProcessing()
    {
        var root = DirectoryForTest();
        try
        {
            var workspace = BatchWorkspace.Open(root);
            using var lease = new FileStream(Path.Combine(root, "run.lease"), FileMode.OpenOrCreate, FileAccess.Write, FileShare.None);
            var invoked = false;
            await Assert.ThrowsAsync<IOException>(() => new BatchRunner(workspace).RunAsync([], (_, _) =>
            {
                invoked = true;
                return Task.FromResult(BatchDocumentResult.Ok(1, 1, []));
            }));
            Assert.False(invoked);
            Assert.False(File.Exists(workspace.IndexPath));
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public void AsymmetricIssuanceRejectsExpiredSigningKid()
    {
        var names = new[] { "LSPP_LICENSE_SIGNING_PRIVATE_KEYS", "LSPP_LICENSE_SIGNING_ACTIVE_KID", "LSPP_LICENSE_SIGNING_KEY_EXPIRATIONS" };
        var old = names.ToDictionary(n => n, Environment.GetEnvironmentVariable);
        try
        {
            using var key = System.Security.Cryptography.ECDsa.Create(System.Security.Cryptography.ECCurve.NamedCurves.nistP256);
            Environment.SetEnvironmentVariable(names[0], "expired=" + Convert.ToBase64String(key.ExportPkcs8PrivateKey()));
            Environment.SetEnvironmentVariable(names[1], "expired");
            Environment.SetEnvironmentVariable(names[2], "expired=" + DateTime.UtcNow.AddDays(-1).ToString("yyyy-MM-dd"));
            Assert.Throws<InvalidOperationException>(() => LicenseKey.Generate(LicenseKey.LicenseTier.Unlimited, "fixture@example.test", "Fixture"));
        }
        finally { foreach (var pair in old) Environment.SetEnvironmentVariable(pair.Key, pair.Value); }
    }

    [Fact]
    public async Task BatchRebuildsCountsWhenManifestWasPersistedBeforeCheckpoint()
    {
        var root = DirectoryForTest();
        try
        {
            var source = Path.Combine(root, "source.pdf");
            await File.WriteAllTextAsync(source, "test fixture bytes");
            var workspace = BatchWorkspace.Open(Path.Combine(root, "work"));
            var runner = new BatchRunner(workspace);
            await runner.RunAsync([source], (_, _) => Task.FromResult(BatchDocumentResult.Ok(1, 1, [])));
            workspace.SaveCheckpoint(new BatchCheckpoint(0, 0, 0, DateTime.UtcNow.ToString("O")));
            var resumed = await runner.RunAsync([source], (_, _) => throw new Exception("Must not repeat durable work"));
            Assert.Equal(0, resumed.Processed);
            Assert.Equal(1, resumed.Completed);
            Assert.Equal(0, resumed.Failed);
            Assert.Equal(0, resumed.Remaining);
        }
        finally { Directory.Delete(root, true); }
    }
}
