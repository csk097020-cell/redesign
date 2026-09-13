using Sentry;

namespace MomentaryMomentos.Services;

/// <summary>
/// Reporting for failures that must survive a Release build.
///
/// The rest of the app logs with <c>System.Diagnostics.Debug.WriteLine</c>, which the compiler
/// strips from Release — so a store-installed app emitted nothing at all when an upload failed.
/// These helpers write to stdout (visible in logcat, filter on "MoMo") in every configuration
/// and forward to Sentry, which is already initialised in MauiProgram.
/// </summary>
public static class Diagnostics
{
    private const string Tag = "[MoMo]";

    /// <summary>Records a step on the way to a failure. Shows up as breadcrumbs on the Sentry event.</summary>
    public static void Trace(string operation, string message)
    {
        Console.WriteLine($"{Tag} {operation}: {message}");
        try
        {
            SentrySdk.AddBreadcrumb(message, category: operation);
        }
        catch
        {
            // Never let telemetry break the flow it is observing.
        }
    }

    /// <summary>Reports a failure with the context needed to tell the causes apart in the wild.</summary>
    public static void Report(Exception ex, string operation, IDictionary<string, string>? context = null)
    {
        Console.WriteLine($"{Tag} {operation} FAILED: {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}");

        try
        {
            SentrySdk.CaptureException(ex, scope =>
            {
                scope.SetTag("operation", operation);
                if (context is null) return;
                foreach (var (key, value) in context)
                    scope.SetExtra(key, value);
            });
        }
        catch
        {
            // Never let telemetry break the flow it is observing.
        }
    }
}
