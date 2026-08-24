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
/// exporter. When disabled, ActivitySource.StartActivity returns null with
/// negligible overhead, so every call site guarded by "activity?." is a no-op.
/// </summary>
public static class AgentTelemetry
{
    public const string SourceName = "AgentSharp";

    public static readonly ActivitySource Source = new(SourceName, "0.1.0");

    /// <summary>
    /// Wires up the console exporter if AGENT_ENABLE_OTEL is set to a truthy value.
    /// Returns null (nothing to dispose) when tracing is disabled, which is the
    /// default -- callers should still hold the result in a `using` so tracing can
    /// be enabled later without an API change.
    /// </summary>
    public static TracerProvider? Initialize()
    {
        var enabled = Environment.GetEnvironmentVariable("AGENT_ENABLE_OTEL");
        if (string.IsNullOrEmpty(enabled) ||
            enabled.Equals("0", StringComparison.OrdinalIgnoreCase) ||
            enabled.Equals("false", StringComparison.OrdinalIgnoreCase))
            return null;

        return Sdk.CreateTracerProviderBuilder()
            .SetResourceBuilder(ResourceBuilder.CreateDefault().AddService(SourceName))
            .AddSource(SourceName)
            .AddConsoleExporter()
            .Build();
    }
}
