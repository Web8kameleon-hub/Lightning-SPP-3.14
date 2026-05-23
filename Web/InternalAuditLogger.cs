using System.Text;

namespace wwwmmm.Web;

internal static class InternalAuditLogger
{
    private static readonly string LogDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "LightningSPP",
        "logs");

    private static readonly string LogFilePath = Path.Combine(LogDirectory, "internal-audit.log");

    public static void Info(string message)
    {
        Write("INFO", message);
    }

    public static void Warn(string message)
    {
        Write("WARN", message);
    }

    public static void Error(string message)
    {
        Write("ERROR", message);
    }

    private static void Write(string level, string message)
    {
        try
        {
            Directory.CreateDirectory(LogDirectory);
            var line = $"{DateTime.UtcNow:O}|{level}|{message}";
            File.AppendAllText(LogFilePath, line + Environment.NewLine, Encoding.UTF8);
        }
        catch
        {
            // Logging must not block runtime.
        }
    }
}
