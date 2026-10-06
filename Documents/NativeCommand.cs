using System.ComponentModel;
using System.Diagnostics;
using System.Text;

namespace wwwmmm.Documents;

public sealed class DocumentException(string message) : Exception(message);

internal static class NativeCommand
{
    internal static async Task<string> RunAsync(string executable, IEnumerable<string> arguments,
        TimeSpan timeout, CancellationToken cancellationToken)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(timeout);
        var start = new ProcessStartInfo(executable)
        {
            UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true,
            CreateNoWindow = true
        };
        start.Environment["LC_ALL"] = "C";
        foreach (var argument in arguments) start.ArgumentList.Add(argument);
        using var process = new Process { StartInfo = start };
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!process.Start()) throw new DocumentException("Could not start the required native tool.");
            var output = ReadBoundedAsync(process.StandardOutput, deadline);
            var errors = ReadBoundedAsync(process.StandardError, deadline);
            await Task.WhenAll(output, errors, process.WaitForExitAsync(deadline.Token));
            if (process.ExitCode != 0)
                throw new DocumentException($"Native tool failed with exit code {process.ExitCode}. No successful operation was confirmed.");
            return await output;
        }
        catch (Win32Exception)
        {
            throw new DocumentException("Required native tool is unavailable. Install its runtime dependency and configure the device/service.");
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new DocumentException("Native operation timed out or exceeded its output limit. A submitted print job may still exist; check the queue before retrying.");
        }
        finally
        {
            try
            {
                if (process.Id > 0 && !process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                    await process.WaitForExitAsync(CancellationToken.None);
                }
            }
            catch (InvalidOperationException) { }
        }
    }

    private static async Task<string> ReadBoundedAsync(StreamReader reader, CancellationTokenSource deadline)
    {
        var output = new StringBuilder();
        var buffer = new char[4096];
        int count;
        while ((count = await reader.ReadAsync(buffer.AsMemory(), deadline.Token)) > 0)
        {
            if (output.Length + count > 64 * 1024)
            {
                deadline.Cancel();
                throw new DocumentException("Native tool output exceeded its limit. No successful operation was confirmed.");
            }
            output.Append(buffer, 0, count);
        }
        return output.ToString();
    }
}
