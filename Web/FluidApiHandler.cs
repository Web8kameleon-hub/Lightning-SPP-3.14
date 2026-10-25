using System;
using System.IO;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using wwwmmm.FluidEngine.Core.Binary;
using wwwmmm.FluidEngine.Core.Context;
using wwwmmm.FluidEngine.Core.Registry;

namespace wwwmmm.Web;

public static class FluidApiHandler
{
    public static async Task HandleAsync(HttpListenerContext context, string path)
    {
        var pipelineName = path.Substring("/api/fluid/".Length).Trim('/');
        var descriptor = PipelineRegistry.Get(pipelineName);

        try
        {
            var body = await new StreamReader(context.Request.InputStream).ReadToEndAsync();
            using var doc = JsonDocument.Parse(body);
            var inputData = doc.RootElement.GetProperty("inputData").GetString();

            var ctx = new PipelineContext();
            byte[] output;

            switch (pipelineName.ToLowerInvariant())
            {
                case "bitpipeline":
                    var bit = new BitPipeline();
                    output = await bit.ExecuteAsync(Convert.FromBase64String(inputData!), ctx, default);
                    break;
                case "bitunpacker":
                    var unpacker = new BitUnpacker();
                    output = await unpacker.ExecuteAsync(Convert.FromBase64String(inputData!), ctx, default);
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
        catch (Exception ex)
        {
            context.Response.StatusCode = 400;
            await WriteJson(context.Response, new { error = ex.Message, pipeline = pipelineName });
        }
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
