using System.Globalization;
using System.Text;

namespace wwwmmm.Services;

internal static class LightningMetrics
{
    private const double SloAvailabilityTargetPct = 99.5;
    private const double SloScanAvgTargetMs = 25.0;
    private const double SloProcessAvgTargetMs = 30.0;
    private const double SloPrintAvgTargetMs = 25.0;

    private static readonly object _sync = new();

    private static long _pipelineRequests;
    private static long _pipelineSuccess;
    private static long _pipelineFailure;

    private static long _scanCalls;
    private static long _scanTotalMs;

    private static long _processCalls;
    private static long _processTotalMs;

    private static long _printCalls;
    private static long _printTotalMs;

    public static void RecordPipeline(bool success)
    {
        lock (_sync)
        {
            _pipelineRequests++;
            if (success) _pipelineSuccess++;
            else _pipelineFailure++;
        }
    }

    public static void RecordScan(long ms)
    {
        lock (_sync)
        {
            _scanCalls++;
            _scanTotalMs += ms;
        }
    }

    public static void RecordProcess(long ms)
    {
        lock (_sync)
        {
            _processCalls++;
            _processTotalMs += ms;
        }
    }

    public static void RecordPrint(long ms)
    {
        lock (_sync)
        {
            _printCalls++;
            _printTotalMs += ms;
        }
    }

    public static string ToText()
    {
        lock (_sync)
        {
            var availability = _pipelineRequests == 0
                ? 100.0
                : (_pipelineSuccess * 100.0) / _pipelineRequests;
            var scanAvg = AvgDouble(_scanTotalMs, _scanCalls);
            var processAvg = AvgDouble(_processTotalMs, _processCalls);
            var printAvg = AvgDouble(_printTotalMs, _printCalls);

            var availabilityBreach = availability < SloAvailabilityTargetPct;
            var latencyBreach = scanAvg > SloScanAvgTargetMs || processAvg > SloProcessAvgTargetMs || printAvg > SloPrintAvgTargetMs;

            var sb = new StringBuilder();
            sb.AppendLine("ok=true");
            sb.AppendLine($"pipelineRequests={_pipelineRequests}");
            sb.AppendLine($"pipelineSuccess={_pipelineSuccess}");
            sb.AppendLine($"pipelineFailure={_pipelineFailure}");
            var ic = CultureInfo.InvariantCulture;
            sb.AppendLine($"availabilityPct={availability.ToString("F2", ic)}");
            sb.AppendLine($"sloAvailabilityTargetPct={SloAvailabilityTargetPct.ToString("F2", ic)}");
            sb.AppendLine($"sloAvailabilityBreached={availabilityBreach.ToString().ToLowerInvariant()}");
            sb.AppendLine($"scanCalls={_scanCalls}");
            sb.AppendLine($"scanAvgMs={scanAvg.ToString("F2", ic)}");
            sb.AppendLine($"sloScanAvgTargetMs={SloScanAvgTargetMs.ToString("F2", ic)}");
            sb.AppendLine($"processCalls={_processCalls}");
            sb.AppendLine($"processAvgMs={processAvg.ToString("F2", ic)}");
            sb.AppendLine($"sloProcessAvgTargetMs={SloProcessAvgTargetMs.ToString("F2", ic)}");
            sb.AppendLine($"printCalls={_printCalls}");
            sb.AppendLine($"printAvgMs={printAvg.ToString("F2", ic)}");
            sb.AppendLine($"sloPrintAvgTargetMs={SloPrintAvgTargetMs.ToString("F2", ic)}");
            sb.AppendLine($"sloLatencyBreached={latencyBreach.ToString().ToLowerInvariant()}");
            sb.AppendLine($"alertsActive={(availabilityBreach || latencyBreach).ToString().ToLowerInvariant()}");
            return sb.ToString();
        }
    }

    public static string AlertsToText()
    {
        lock (_sync)
        {
            var availability = _pipelineRequests == 0
                ? 100.0
                : (_pipelineSuccess * 100.0) / _pipelineRequests;
            var scanAvg = AvgDouble(_scanTotalMs, _scanCalls);
            var processAvg = AvgDouble(_processTotalMs, _processCalls);
            var printAvg = AvgDouble(_printTotalMs, _printCalls);

            var alerts = new List<string>();
            var ic2 = CultureInfo.InvariantCulture;
            if (availability < SloAvailabilityTargetPct)
                alerts.Add($"availability_breach current={availability.ToString("F2", ic2)} target={SloAvailabilityTargetPct.ToString("F2", ic2)}");
            if (scanAvg > SloScanAvgTargetMs)
                alerts.Add($"scan_latency_breach current={scanAvg.ToString("F2", ic2)} target={SloScanAvgTargetMs.ToString("F2", ic2)}");
            if (processAvg > SloProcessAvgTargetMs)
                alerts.Add($"process_latency_breach current={processAvg.ToString("F2", ic2)} target={SloProcessAvgTargetMs.ToString("F2", ic2)}");
            if (printAvg > SloPrintAvgTargetMs)
                alerts.Add($"print_latency_breach current={printAvg.ToString("F2", ic2)} target={SloPrintAvgTargetMs.ToString("F2", ic2)}");

            var sb = new StringBuilder();
            sb.AppendLine("ok=true");
            sb.AppendLine($"alertsCount={alerts.Count}");
            sb.AppendLine($"alertsActive={(alerts.Count > 0).ToString().ToLowerInvariant()}");
            foreach (var a in alerts)
                sb.AppendLine($"alert={a}");
            if (alerts.Count == 0)
                sb.AppendLine("alert=none");
            return sb.ToString();
        }
    }

    private static double AvgDouble(long total, long count)
    {
        if (count == 0) return 0;
        return total / (double)count;
    }

    private static string Avg(long total, long count)
    {
        if (count == 0) return "0";
        return (total / (double)count).ToString("F2", CultureInfo.InvariantCulture);
    }
}
