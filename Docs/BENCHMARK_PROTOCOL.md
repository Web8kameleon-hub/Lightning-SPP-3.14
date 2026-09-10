# Lightning SPP Benchmark Protocol

## Purpose

This protocol separates customer-facing measured results from engineering targets and universal guarantees. A benchmark result is publishable only when it includes the commit, runtime, hardware, operating system, payload, profile, warm-up, repetitions and statistical summary.

## Commands

```bash
dotnet restore
dotnet build -c Release --no-restore
dotnet test Tests/LightningSPP.Tests/LightningSPP.Tests.csproj -c Release --no-build
dotnet run -c Release --no-build -- speed
```

## Release gates

| Workload | Input | Statistic | CI gate |
|---|---:|---:|---:|
| Instant | deterministic 4 KiB | median of 5 | <= 50 ms |
| UltraFast | deterministic 4 KiB | median of 5 | <= 100 ms |
| Lightning | deterministic 4 KiB | median of 5 | <= 150 ms |
| Full real pipeline | deterministic 8 KiB file | wall clock | <= 1500 ms |

The gates detect regressions on shared CI runners. They are deliberately wider than dedicated-machine reference results and must not replace a published benchmark report.

## Repository reference record

The v3.14 documentation records 0.3 ms Instant, 3.7 ms UltraFast, 15 ms Lightning and 3.1M operations/s cloud throughput. These values are reference records, not guarantees for every device. A new release may repeat them only alongside a generated environment record.

## Required environment record

```text
commit_sha:
version:
utc_timestamp:
dotnet_runtime:
os:
cpu:
logical_cores:
memory:
power_mode:
containerized:
payload_sha256:
payload_bytes:
profile:
warmup_runs:
measured_runs:
median_ms:
p95_ms:
p99_ms:
throughput_ops_s:
peak_working_set_mb:
```

## Comparative claims

Claims such as “4x faster than Adobe Scan” or “50% cheaper than Google Vision” require a separate competitor protocol containing equivalent inputs, feature parity, product versions, regional pricing, timestamps and raw results. They must not be published from internal Lightning-only measurements.
