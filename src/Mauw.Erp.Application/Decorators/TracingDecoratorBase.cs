using System.Diagnostics;
using Mauw.Erp.Application.Diagnostics;
using Mauw.Erp.Application.Services;

namespace Mauw.Erp.Application.Decorators;

/// <summary>
/// Base class for tracing decorators.
/// Starts an OTel Activity for each operation and propagates failure status.
/// </summary>
public abstract class TracingDecoratorBase
{
    protected static async Task<OperationResult<T>> TraceAsync<T>(
        string operationName,
        Func<Task<OperationResult<T>>> operation,
        Action<Activity>? configureTags = null)
    {
        using var activity = TelemetryDiagnostics.ActivitySource.StartActivity(operationName);
        configureTags?.Invoke(activity!);

        var result = await operation();

        if (!result.Success)
            activity?.SetStatus(ActivityStatusCode.Error, result.Error);

        return result;
    }
}
