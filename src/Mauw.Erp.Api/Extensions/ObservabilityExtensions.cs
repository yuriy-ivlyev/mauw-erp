using Mauw.Erp.Application.Diagnostics;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Mauw.Erp.Api.Extensions;

public static class ObservabilityExtensions
{
    /// <summary>
    /// Registers OpenTelemetry tracing, metrics and logging.
    /// Configuration is driven by standard OTel environment variables:
    ///   OTEL_SERVICE_NAME, OTEL_EXPORTER_OTLP_ENDPOINT, OTEL_EXPORTER_OTLP_PROTOCOL
    /// Defaults: service name = "mauw-erp-backend", endpoint = http://localhost:4317 (grpc).
    /// </summary>
    public static IHostApplicationBuilder AddObservability(this IHostApplicationBuilder builder)
    {
        var resourceBuilder = ResourceBuilder.CreateDefault()
            .AddService(
                serviceName: builder.Configuration["OTEL_SERVICE_NAME"] ?? TelemetryDiagnostics.ServiceName,
                serviceVersion: TelemetryDiagnostics.ServiceVersion)
            .AddTelemetrySdk()
            .AddEnvironmentVariableDetector();

        builder.Services
            .AddOpenTelemetry()
            .WithTracing(tracing => tracing
                .SetResourceBuilder(resourceBuilder)
                .AddAspNetCoreInstrumentation(opts =>
                {
                    opts.RecordException = true;
                    // Exclude health/swagger noise
                    opts.Filter = ctx =>
                        !ctx.Request.Path.StartsWithSegments("/swagger") &&
                        !ctx.Request.Path.StartsWithSegments("/health");
                })
                .AddHttpClientInstrumentation()
                .AddSource("Npgsql")   // Npgsql 10.x activity source name
                .AddSource(TelemetryDiagnostics.ActivitySource.Name)
                .AddOtlpExporter())  // reads OTEL_EXPORTER_OTLP_ENDPOINT automatically
            .WithMetrics(metrics => metrics
                .SetResourceBuilder(resourceBuilder)
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddRuntimeInstrumentation()
                .AddMeter(TelemetryDiagnostics.Meter.Name)
                .AddOtlpExporter());

        // Logging: ship structured logs to the OTLP collector; keep the default console
        // provider so development stays readable without a collector running.
        builder.Logging.AddOpenTelemetry(logging =>
        {
            logging.SetResourceBuilder(resourceBuilder);
            logging.IncludeFormattedMessage = true;
            logging.IncludeScopes = true;
            logging.ParseStateValues = true;
            logging.AddOtlpExporter();
        });

        // JSON console output for structured log ingestion in container runtimes.
        builder.Logging.AddJsonConsole(opts =>
            opts.JsonWriterOptions = new System.Text.Json.JsonWriterOptions { Indented = false });

        return builder;
    }
}
