namespace AgentSharp.Llm;

/// <summary>
/// Events emitted during streaming LLM responses.
/// The agent loop consumes these to render text in real-time
/// and accumulate tool call data.
/// </summary>
public abstract record StreamEvent;

/// <summary>
/// A chunk of text from the assistant's response.
/// </summary>
public record TextDelta(string Text) : StreamEvent;

/// <summary>
/// A tool use block has started streaming.
/// </summary>
public record ToolUseStart(string Id, string Name) : StreamEvent;

/// <summary>
/// A partial JSON fragment for the current tool's input.
/// Accumulate these and parse after ToolUseEnd.
/// </summary>
public record ToolInputDelta(string PartialJson) : StreamEvent;

/// <summary>
/// The current tool use block is complete. Parse accumulated input JSON now.
/// </summary>
public record ToolUseEnd : StreamEvent;

/// <summary>
/// The full response is complete. Contains the stop reason.
/// </summary>
public record StreamDone(string StopReason) : StreamEvent;

/// <summary>
/// Usage information from the response.
/// </summary>
public record UsageInfo(int InputTokens, int OutputTokens) : StreamEvent;
