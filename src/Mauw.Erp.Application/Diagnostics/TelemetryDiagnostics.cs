using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Mauw.Erp.Application.Diagnostics;

/// <summary>
/// Central hub for custom Meter instances and business metrics.
/// Tracing coverage is provided automatically by OTel instrumentations
/// (AspNetCore, HttpClient, Npgsql). Use ActivitySource only for explicit
/// custom spans at endpoint/background-job boundaries, not inside services.
/// </summary>
public static class TelemetryDiagnostics
{
    public const string ServiceName = "Mauw.Erp";
    public const string ServiceVersion = "1.0.0";

    // ActivitySource is intentionally kept here for optional use at boundary
    // layers (endpoints, background jobs). Do NOT use inside service methods.
    public static readonly ActivitySource ActivitySource = new(ServiceName, ServiceVersion);

    // --- Metrics ---
    public static readonly Meter Meter = new(ServiceName, ServiceVersion);

    /// <summary>Total number of orders successfully created.</summary>
    public static readonly Counter<long> OrdersCreated =
        Meter.CreateCounter<long>(
            name: "mauw.orders.created",
            unit: "{order}",
            description: "Total number of orders successfully created.");

    /// <summary>Duration of business operations in milliseconds.</summary>
    public static readonly Histogram<double> OperationDuration =
        Meter.CreateHistogram<double>(
            name: "mauw.operation.duration_ms",
            unit: "ms",
            description: "Duration of critical business operations.");
}
