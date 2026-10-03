using wwwmmm.PublicAPI;
using wwwmmm.Services;

namespace wwwmmm.Tests;

/// <summary>
/// Teste të thjeshta për Lightning SPP System
/// </summary>
public static class SimpleTests
{
    public static async Task RunAllTests()
    {
        Console.WriteLine("⚡ Running Lightning SPP Tests...\n");

        var passed = 0;
        var failed = 0;

        // Test 1: Scan Service
        if (await TestScanService())
        {
            Console.WriteLine("✓ Test 1: Scan Service - PASSED");
            passed++;
        }
        else
        {
            Console.WriteLine("✗ Test 1: Scan Service - FAILED");
            failed++;
        }

        // Test 2: Processing Service
        if (await TestProcessingService())
        {
            Console.WriteLine("✓ Test 2: Processing Service - PASSED");
            passed++;
        }
        else
        {
            Console.WriteLine("✗ Test 2: Processing Service - FAILED");
            failed++;
        }

        // Test 3: Print Service
        if (await TestPrintService())
        {
            Console.WriteLine("✓ Test 3: Print Service - PASSED");
            passed++;
        }
        else
        {
            Console.WriteLine("✗ Test 3: Print Service - FAILED");
            failed++;
        }

        // Test 4: Full Pipeline
        if (await TestFullPipeline())
        {
            Console.WriteLine("✓ Test 4: Full Pipeline - PASSED");
            passed++;
        }
        else
        {
            Console.WriteLine("✗ Test 4: Full Pipeline - FAILED");
            failed++;
        }

        // Test 5: Batch Processing
        if (await TestBatchProcessing())
        {
            Console.WriteLine("✓ Test 5: Batch Processing - PASSED");
            passed++;
        }
        else
        {
            Console.WriteLine("✗ Test 5: Batch Processing - FAILED");
            failed++;
        }

        Console.WriteLine($"\n📊 Test Results: {passed} passed, {failed} failed");
        Console.WriteLine(failed == 0 ? "✓ All tests passed!" : "⚠️  Some tests failed");
        if (failed > 0)
            throw new InvalidOperationException($"CLI validation failed: {failed} tests failed.");
    }

    private static async Task<bool> TestScanService()
    {
        try
        {
            var sourcePath = CreateTempTestFile("scan-service", 4096);
            var result = await LightningAPI.QuickScanAsync(sourcePath);
            return result.Success && result.Data.Length > 0;
        }
        catch
        {
            return false;
        }
    }

    private static async Task<bool> TestProcessingService()
    {
        try
        {
            var testData = GenerateTestData(1000);
            var result = await LightningAPI.HybridProcessAsync(testData);
            return result.Success && result.ProcessedData.Length > 0;
        }
        catch
        {
            return false;
        }
    }

    private static async Task<bool> TestPrintService()
    {
        try
        {
            var testData = GenerateTestData(1000);
            var result = await LightningAPI.StigmaPrintAsync(testData);
            return result.Success;
        }
        catch
        {
            return false;
        }
    }

    private static async Task<bool> TestFullPipeline()
    {
        try
        {
            var sourcePath = CreateTempTestFile("pipeline", 8192);
            var result = await LightningAPI.ExecuteLightningPipelineAsync(sourcePath);
            return result.Success && result.Steps.Count > 0;
        }
        catch
        {
            return false;
        }
    }

    private static async Task<bool> TestBatchProcessing()
    {
        try
        {
            var sources = new List<string>
            {
                CreateTempTestFile("batch-1", 2048),
                CreateTempTestFile("batch-2", 3072)
            };

            var results = await LightningAPI.BatchProcessAsync(sources);
            return results.Count == sources.Count && results.All(r => r.Success);
        }
        catch
        {
            return false;
        }
    }

    private static byte[] GenerateTestData(int size)
    {
        var data = new byte[size];
        Random.Shared.NextBytes(data);
        return data;
    }

    private static string CreateTempTestFile(string prefix, int size)
    {
        var filePath = Path.Combine(Path.GetTempPath(), $"lightning-spp-{prefix}-{Guid.NewGuid():N}.bin");
        File.WriteAllBytes(filePath, GenerateTestData(size));
        return filePath;
    }
}
