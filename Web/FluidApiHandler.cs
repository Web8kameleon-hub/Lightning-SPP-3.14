using System;
using System.IO;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Threading;
using System.Buffers.Binary;
using System.Collections.Generic;
using wwwmmm.FluidEngine.Core.Binary;
using wwwmmm.FluidEngine.Core.Context;
using wwwmmm.FluidEngine.Core.Registry;

namespace wwwmmm.Web;

public static class FluidApiHandler
{
    private static readonly SemaphoreSlim Slots = new(2, 2);
    private const int MaxBodyBytes = 1024 * 1024;
    private const int MaxOutputBytes = 8 * 1024 * 1024;
    public static async Task HandleAsync(HttpListenerContext context, string path)
    {
        var pipelineName = path.Substring("/api/fluid/".Length).Trim('/');
        if (context.Request.HttpMethod != "POST")
        { context.Response.StatusCode = 405; context.Response.Headers["Allow"] = "POST"; await WriteJson(context.Response, new { error = "POST required." }); return; }
        if (pipelineName.ToLowerInvariant() is not ("bitpipeline" or "bitunpacker"))
        { context.Response.StatusCode = 404; await WriteJson(context.Response, new { error = "Unknown or unavailable binary pipeline." }); return; }
        if (!await Slots.WaitAsync(0))
        { context.Response.StatusCode = 429; context.Response.Headers["Retry-After"] = "5"; await WriteJson(context.Response, new { error = "Binary pipeline is busy." }); return; }

        try
        {
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            using var body = new MemoryStream(); var buffer = new byte[8192]; int count;
            while ((count = await context.Request.InputStream.ReadAsync(buffer, deadline.Token)) != 0)
            {
                if (body.Length + count > MaxBodyBytes)
                { context.Response.StatusCode = 413; await WriteJson(context.Response, new { error = "Binary request exceeds 1 MiB." }); return; }
                await body.WriteAsync(buffer.AsMemory(0, count), deadline.Token);
            }
            using var doc = JsonDocument.Parse(body.ToArray());
            var inputData = doc.RootElement.GetProperty("inputData").GetString();
            var decoded = Convert.FromBase64String(inputData ?? throw new FormatException("Binary input required."));
            if (pipelineName.Equals("bitunpacker", StringComparison.OrdinalIgnoreCase) && decoded.Length >= 7 &&
                BinaryPrimitives.ReadInt32LittleEndian(decoded.AsSpan(3, 4)) > MaxOutputBytes)
            { context.Response.StatusCode = 413; await WriteJson(context.Response, new { error = "Binary output exceeds 8 MiB." }); return; }
            var descriptor = PipelineRegistry.Get(pipelineName);

            var ctx = new PipelineContext();
            byte[] output;

            switch (pipelineName.ToLowerInvariant())
            {
                case "bitpipeline":
                    var bit = new BitPipeline();
                    output = await bit.ExecuteAsync(decoded, ctx, deadline.Token);
                    break;
                case "bitunpacker":
                    var unpacker = new BitUnpacker();
                    output = await unpacker.ExecuteAsync(decoded, ctx, deadline.Token);
                    break;
                default:
                    throw new NotSupportedException($"Pipeline '{pipelineName}' nuk është i lidhur me HTTP ende.");
            }

            await WriteJson(context.Response, new
            {
                traceId = ctx.TraceId,
                pipeline = descriptor.Name,
                output = Convert.ToBase64String(output),
                executionTimeMs = Math.Round(ctx.GlobalTimer.Elapsed.TotalMilliseconds, 3),
                steps = ctx.StepTraces
            });
        }
        catch (OperationCanceledException)
        { context.Response.StatusCode = 408; await WriteJson(context.Response, new { error = "Binary request deadline exceeded." }); }
        catch (Exception ex) when (ex is JsonException or FormatException or InvalidOperationException or IOException or KeyNotFoundException)
        {
            context.Response.StatusCode = 400;
            await WriteJson(context.Response, new { error = "Invalid binary request or frame.", pipeline = pipelineName });
        }
        finally { Slots.Release(); }
    }

    private static async Task WriteJson(HttpListenerResponse res, object obj)
    {
        var json = JsonSerializer.Serialize(obj, new JsonSerializerOptions { WriteIndented = true });
        var bytes = Encoding.UTF8.GetBytes(json);
        res.ContentType = "application/json";
        res.ContentLength64 = bytes.Length;
        await res.OutputStream.WriteAsync(bytes, 0, bytes.Length);
        res.Close();
    }
}
