using System.Globalization;
using System.Text.Json;

namespace wwwmmm.Documents;

internal static class DocumentCommands
{
    internal static async Task<int> RunAsync(string[] args, CancellationToken token = default)
    {
        try
        {
            var pdf = new PdfDocumentService();
            var devices = new LinuxDeviceService();
            if (args.Length >= 4 && args[0] == "document" && args[1] == "pdf-create")
            {
                if (args.Length > 35) throw new ArgumentException("At most 32 images are supported.");
                var images = new List<byte[]>();
                foreach (var path in args.Skip(3)) images.Add(await DocumentFiles.ReadAsync(path, PdfDocumentService.MaxInputBytes, token));
                await DocumentFiles.SaveNewAsync(args[2], pdf.CreateFromImages(images, cancellationToken: token), ".pdf", token);
                Console.WriteLine(JsonSerializer.Serialize(new { saved = Path.GetFullPath(args[2]), pages = images.Count }));
                return 0;
            }
            if (args.Length == 5 && args[0] == "document" && args[1] == "pdf-render")
            {
                if (!int.TryParse(args[3], NumberStyles.None, CultureInfo.InvariantCulture, out var page))
                    throw new ArgumentException("Page must be a positive integer.");
                var result = await pdf.RenderPageAsync(await DocumentFiles.ReadAsync(args[2], PdfDocumentService.MaxInputBytes, token), page, token);
                await DocumentFiles.SaveNewAsync(args[4], result.Png, ".png", token);
                Console.WriteLine(JsonSerializer.Serialize(new { saved = Path.GetFullPath(args[4]), page = result.Page, totalPages = result.TotalPages }));
                return 0;
            }
            if (args.Length == 2 && args[0] == "device" && args[1] is "list-printers" or "list-scanners")
            {
                Console.WriteLine(args[1] == "list-printers" ? await devices.ListPrintersAsync(token) : await devices.ListScannersAsync(token));
                return 0;
            }
            if (args.Length == 3 && args[0] == "device" && args[1] == "print-status")
            {
                var schedulerListing = await devices.GetPrintQueueAsync(args[2], token);
                Console.WriteLine(JsonSerializer.Serialize(new { jobId = args[2], schedulerListing, physicalCompletionVerified = false }));
                return 0;
            }
            if (args.Length == 4 && args[0] == "device" && args[1] == "print")
            {
                var result = await devices.SubmitPdfAsync(args[2], await DocumentFiles.ReadAsync(args[3], PdfDocumentService.MaxInputBytes, token), token);
                Console.WriteLine(JsonSerializer.Serialize(result));
                return 0;
            }
            if (args.Length == 5 && args[0] == "device" && args[1] == "scan")
            {
                if (!int.TryParse(args[3], NumberStyles.None, CultureInfo.InvariantCulture, out var dpi))
                    throw new ArgumentException("DPI must be an integer.");
                await DocumentFiles.SaveNewAsync(args[4], await devices.ScanAsync(args[2], dpi, token), ".png", token);
                Console.WriteLine(JsonSerializer.Serialize(new { saved = Path.GetFullPath(args[4]), device = args[2], dpi }));
                return 0;
            }
            Console.Error.WriteLine("Usage: document pdf-create <new.pdf> <image>... | document pdf-render <input.pdf> <page> <new.png> | device list-printers | device list-scanners | device scan <sane-device> <dpi> <new.png> | device print <cups-queue> <input.pdf> | device print-status <job-id>");
            return 2;
        }
        catch (OperationCanceledException)
        {
            Console.Error.WriteLine("Operation cancelled. Check the print queue before retrying a submission.");
            return 1;
        }
        catch (Exception ex) when (ex is DocumentException or ArgumentException or PlatformNotSupportedException)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Console.Error.WriteLine("Document file could not be read or saved. Existing output files are never overwritten.");
            return 1;
        }
    }
}
