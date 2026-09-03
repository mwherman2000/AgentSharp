using System.Diagnostics;
using OpenTelemetry;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace AgentSharp.Telemetry;

/// <summary>
/// OpenTelemetry tracing for the agent loop -- structured spans covering the same
/// events the RequestTrace/ToolsTrace/HistoryTrace Console.WriteLine calls report,
/// but with proper span hierarchy (turn -> LLM call -> tool call) and attribution,
/// so concurrent sub-agents (AgentOrchestrator.RunParallelAsync) don't garble each
/// other's trace output the way interleaved Console.WriteLine text does.
///
/// Disabled by default. Set AGENT_ENABLE_OTEL=1 to emit spans via the console
/// exporter at startup, or switch to exporting to Jaeger at any point in a session
/// via the /jaeger REPL command. When no provider is active, ActivitySource.
/// StartActivity returns null with negligible overhead, so every call site guarded
/// by "activity?." is a no-op.
/// </summary>
public static class AgentTelemetry
{
    public const string SourceName = "AgentSharp";

    /// <summary>Default OTLP/gRPC endpoint Jaeger (and most local collectors) listen on.</summary>
    public const string DefaultJaegerEndpoint = "http://localhost:4317";

    public static readonly ActivitySource Source = new(SourceName, "0.1.0");

    private static TracerProvider? _provider;

    /// <summary>
    /// Wires up the console exporter if AGENT_ENABLE_OTEL is set to a truthy value.
    /// Leaves tracing off (the default) otherwise. Call Shutdown() once at process
    /// exit to flush whichever provider -- this one, or one from a later
    /// SwitchToJaeger call -- ends up active.
    /// </summary>
    public static void Initialize()
    {
        var enabled = Environment.GetEnvironmentVariable("AGENT_ENABLE_OTEL");
        if (string.IsNullOrEmpty(enabled) ||
            enabled.Equals("0", StringComparison.OrdinalIgnoreCase) ||
            enabled.Equals("false", StringComparison.OrdinalIgnoreCase))
            return;

        _provider = BuildProvider(useConsole: true, otlpEndpoint: null);
    }

    /// <summary>
    /// Switches the active exporter to OTLP, pointed at a locally running Jaeger (or
    /// any OTLP/gRPC-compatible collector) -- tearing down whatever provider was
    /// previously active (console, or an earlier SwitchToJaeger call) and replacing
    /// it. Works even if AGENT_ENABLE_OTEL was never set: this both enables tracing
    /// and redirects it in one step, from that point in the session onward.
    /// </summary>
    public static void SwitchToJaeger(string endpoint = DefaultJaegerEndpoint)
    {
        _provider?.Dispose();
        _provider = BuildProvider(useConsole: false, otlpEndpoint: endpoint);
    }

    /// <summary>Flushes and disposes the active provider, if any. Call once at process exit.</summary>
    public static void Shutdown() => _provider?.Dispose();

    private static TracerProvider BuildProvider(bool useConsole, string? otlpEndpoint)
    {
        var builder = Sdk.CreateTracerProviderBuilder()
            .SetResourceBuilder(ResourceBuilder.CreateDefault().AddService(SourceName))
            .AddSource(SourceName);

        if (useConsole)
            builder.AddConsoleExporter();

        if (otlpEndpoint is not null)
            builder.AddOtlpExporter(o => o.Endpoint = new Uri(otlpEndpoint));

        return builder.Build();
    }
}
