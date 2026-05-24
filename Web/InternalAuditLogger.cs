using System.Text;

namespace wwwmmm.Web;

internal static class InternalAuditLogger
{
    private const long MaxLogBytes = 5 * 1024 * 1024; // 5 MB
    private const int MaxRotatedFiles = 5;

    private static readonly string LogDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "LightningSPP",
        "logs");

    private static readonly string LogFilePath = Path.Combine(LogDirectory, "internal-audit.log");
    private static readonly object _lock = new();

    public static void Info(string message) => Write("INFO", message);
    public static void Warn(string message) => Write("WARN", message);
    public static void Error(string message) => Write("ERROR", message);

    private static void Write(string level, string message)
    {
        try
        {
            lock (_lock)
            {
                Directory.CreateDirectory(LogDirectory);
                RotateIfNeeded();
                var line = $"{DateTime.UtcNow:O}|{level}|{message}";
                File.AppendAllText(LogFilePath, line + Environment.NewLine, Encoding.UTF8);
            }
        }
        catch
        {
            // Logging must not block runtime.
        }
    }

    private static void RotateIfNeeded()
    {
        if (!File.Exists(LogFilePath)) return;
        var info = new FileInfo(LogFilePath);
        if (info.Length < MaxLogBytes) return;

        // Shift existing rotated files
        for (int i = MaxRotatedFiles - 1; i >= 1; i--)
        {
            var older = $"{LogFilePath}.{i}";
            var newer = $"{LogFilePath}.{i + 1}";
            if (File.Exists(older))
            {
                if (File.Exists(newer)) File.Delete(newer);
                File.Move(older, newer);
            }
        }

        var rotated = $"{LogFilePath}.1";
        if (File.Exists(rotated)) File.Delete(rotated);
        File.Move(LogFilePath, rotated);
    }
}
