using System.Security.Cryptography;
using wwwmmm.Batch;
using wwwmmm.Packaging;
using Xunit;

namespace LightningSPP.Tests;

public class FlowBatchAndPackageTests
{
    private static DocumentProcessor CountingProcessor(List<string> processed) =>
        (path, _) =>
        {
            lock (processed) processed.Add(path);
            return Task.FromResult(BatchDocumentResult.Ok(pages: 1, fields: 2, Array.Empty<string>()));
        };

    private static string NewDirectory(string prefix)
    {
        var directory = Path.Combine(Path.GetTempPath(), $"{prefix}-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        return directory;
    }

    private static string[] SeedDocuments(string directory, int count)
    {
        var paths = new string[count];
        for (var i = 0; i < count; i++)
        {
            paths[i] = Path.Combine(directory, $"doc{i:D2}.pdf");
            File.WriteAllBytes(paths[i], new byte[] { 0x25, 0x50, 0x44, 0x46, (byte)i });
        }
        Array.Sort(paths, StringComparer.Ordinal);
        return paths;
    }

    [Fact]
    public async Task Batch_InterruptedRunResumesWithoutReprocessing()
    {
        var inputsDir = NewDirectory("spp-batch-in");
        var workspaceDir = NewDirectory("spp-batch-ws");

        try
        {
            var inputs = SeedDocuments(inputsDir, 5);
            var workspace = BatchWorkspace.Open(workspaceDir);
            var runner = new BatchRunner(workspace);
            var processed = new List<string>();

            // Simulated interruption: stop after two documents.
            var first = await runner.RunAsync(inputs, CountingProcessor(processed), maxDocuments: 2);
            Assert.Equal(2, first.Processed);
            Assert.Equal(2, first.Completed);
            Assert.Equal(3, first.Remaining);
            Assert.Equal(2, processed.Count);

            // Resume finishes the rest.
            var second = await runner.RunAsync(inputs, CountingProcessor(processed), maxDocuments: null);
            Assert.Equal(3, second.Processed);
            Assert.Equal(5, second.Completed);
            Assert.Equal(0, second.Remaining);
            Assert.Equal(5, processed.Count);
            Assert.Equal(5, processed.Distinct(StringComparer.Ordinal).Count());

            // A further run has nothing left to do.
            var third = await runner.RunAsync(inputs, CountingProcessor(processed), maxDocuments: null);
            Assert.Equal(0, third.Processed);
            Assert.Equal(5, third.Completed);

            Assert.Equal(5, workspace.LoadEntries().Count);
            Assert.All(workspace.LoadEntries(), entry => Assert.Equal(BatchStatus.Completed, entry.Status));
        }
        finally
        {
            Directory.Delete(inputsDir, recursive: true);
            Directory.Delete(workspaceDir, recursive: true);
        }
    }

    [Fact]
    public async Task Batch_VerifyDetectsModifiedSource()
    {
        var inputsDir = NewDirectory("spp-batch-int");
        var workspaceDir = NewDirectory("spp-batch-int-ws");

        try
        {
            var inputs = SeedDocuments(inputsDir, 3);
            var runner = new BatchRunner(BatchWorkspace.Open(workspaceDir));
            await runner.RunAsync(inputs, CountingProcessor(new List<string>()));

            var before = await runner.VerifyAsync();
            Assert.True(before.IsValid, string.Join("; ", before.Problems));
            Assert.Equal(3, before.Entries);

            await File.AppendAllTextAsync(inputs[1], "tampered");

            var after = await runner.VerifyAsync();
            Assert.False(after.IsValid);
            Assert.Equal(1, after.Mismatches);
            Assert.Contains(after.Problems, problem => problem.Contains("doc01.pdf", StringComparison.Ordinal));
        }
        finally
        {
            Directory.Delete(inputsDir, recursive: true);
            Directory.Delete(workspaceDir, recursive: true);
        }
    }

    [Fact]
    public async Task Batch_FailingDocumentDoesNotStopTheRun()
    {
        var inputsDir = NewDirectory("spp-batch-fail");
        var workspaceDir = NewDirectory("spp-batch-fail-ws");

        try
        {
            var inputs = SeedDocuments(inputsDir, 3);
            var workspace = BatchWorkspace.Open(workspaceDir);
            var runner = new BatchRunner(workspace);

            var summary = await runner.RunAsync(inputs, (path, _) =>
                Task.FromResult(path.EndsWith("doc01.pdf", StringComparison.Ordinal)
                    ? BatchDocumentResult.Failed("no text layer")
                    : BatchDocumentResult.Ok(1, 1, Array.Empty<string>())));

            Assert.Equal(3, summary.Processed);
            Assert.Equal(2, summary.Completed);
            Assert.Equal(1, summary.Failed);

            var failed = Assert.Single(workspace.LoadEntries(), e => e.Status == BatchStatus.Failed);
            Assert.Contains("no text layer", failed.Error, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(inputsDir, recursive: true);
            Directory.Delete(workspaceDir, recursive: true);
        }
    }

    [Fact]
    public async Task Package_IsReproducibleAndVerifiable()
    {
        var directory = NewDirectory("spp-package");
        try
        {
            var first = Path.Combine(directory, "a.txt");
            var second = Path.Combine(directory, "b.txt");
            await File.WriteAllTextAsync(first, "alpha");
            await File.WriteAllTextAsync(second, "beta");

            var archiveOne = Path.Combine(directory, "one.zip");
            var archiveTwo = Path.Combine(directory, "two.zip");
            var fixedTime = new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.Zero);

            await DocumentPackager.CreateAsync(archiveOne, new[] { first, second }, fixedTime);
            await DocumentPackager.CreateAsync(archiveTwo, new[] { second, first }, fixedTime);

            var hashOne = Convert.ToHexString(SHA256.HashData(await File.ReadAllBytesAsync(archiveOne)));
            var hashTwo = Convert.ToHexString(SHA256.HashData(await File.ReadAllBytesAsync(archiveTwo)));
            Assert.Equal(hashOne, hashTwo);

            var verification = await DocumentPackager.VerifyAsync(archiveOne);
            Assert.True(verification.IsValid, string.Join("; ", verification.Problems));
            Assert.Equal(2, verification.Members);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task Package_VerifyDetectsModifiedMember()
    {
        var directory = NewDirectory("spp-package-tamper");
        try
        {
            var member = Path.Combine(directory, "data.txt");
            await File.WriteAllTextAsync(member, "original");
            var archivePath = Path.Combine(directory, "pkg.zip");
            await DocumentPackager.CreateAsync(archivePath, new[] { member });

            using (var archive = System.IO.Compression.ZipFile.Open(
                       archivePath, System.IO.Compression.ZipArchiveMode.Update))
            {
                var entry = archive.GetEntry("data.txt");
                Assert.NotNull(entry);
                entry!.Delete();
                var replacement = archive.CreateEntry("data.txt");
                await using var stream = replacement.Open();
                await using var writer = new StreamWriter(stream);
                await writer.WriteAsync("tampered");
            }

            var verification = await DocumentPackager.VerifyAsync(archivePath);
            Assert.False(verification.IsValid);
            Assert.Contains(verification.Problems, p => p.Contains("data.txt", StringComparison.Ordinal));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
