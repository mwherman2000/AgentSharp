using System.Text.Json;
using System.Text.Json.Serialization;
using AgentSharpLib.Agent;
using AgentSharpLib.Llm;

namespace AgentSharpLib.Memory;

/// <summary>
/// Manages conversation sessions -- save, load, list, and resume.
/// Sessions are stored as JSON files in ~/.agentsharp/sessions/.
/// </summary>
public class SessionManager
{
    private readonly string _sessionsDir;

    public SessionManager(string? baseDir = null)
    {
        baseDir ??= Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".agentsharp");
        _sessionsDir = Path.Combine(baseDir, "sessions");
        Directory.CreateDirectory(_sessionsDir);
    }

    /// <summary>True if a session saved under <paramref name="sessionId"/> exists (the id is cleaned the same way as when saving).</summary>
    public bool Exists(string sessionId)
    {
        var safeId = Path.GetFileName(sessionId);
        return !string.IsNullOrEmpty(safeId) && File.Exists(GetSessionPath(safeId));
    }

    /// <summary>
    /// Save a conversation to disk. Returns null (instead of throwing) if
    /// <paramref name="sessionId"/> isn't a usable file name or the write fails
    /// (e.g. permission denied) -- a bad /save shouldn't crash the REPL.
    /// </summary>
    public async Task<string?> SaveAsync(ConversationHistory history, string? sessionId = null, HandoffState? handoff = null)
    {
        if (Prepare(history, sessionId, handoff) is not { } prepared)
            return null;

        // Write to a unique temp file and rename into place so a concurrent Load,
        // or a crash mid-write, never sees a partially-written session file.
        var tempPath = $"{prepared.Path}.{Guid.NewGuid():N}.tmp";
        try
        {
            await File.WriteAllTextAsync(tempPath, prepared.Json);
            File.Move(tempPath, prepared.Path, overwrite: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }

        return prepared.SafeId;
    }

    /// <summary>
    /// <see cref="SaveAsync"/> for callers that cannot await -- the automatic saves taken in the
    /// middle of a handoff or a /compact. Same file, same rules; returns null on the same failures.
    /// </summary>
    public string? Save(ConversationHistory history, string? sessionId = null, HandoffState? handoff = null)
    {
        if (Prepare(history, sessionId, handoff) is not { } prepared)
            return null;

        var tempPath = $"{prepared.Path}.{Guid.NewGuid():N}.tmp";
        try
        {
            File.WriteAllText(tempPath, prepared.Json);
            File.Move(tempPath, prepared.Path, overwrite: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }

        return prepared.SafeId;
    }

    /// <summary>The id, file path and JSON for a save, or null if <paramref name="sessionId"/> isn't usable.</summary>
    private (string SafeId, string Path, string Json)? Prepare(ConversationHistory history, string? sessionId, HandoffState? handoff)
    {
        sessionId ??= Guid.NewGuid().ToString("N")[..8];

        // Path.GetFileName strips any directory portion, so an id like
        // "../../etc/passwd" or an absolutely-rooted path can't Path.Combine its
        // way outside the sessions directory (a leading '/' makes the second
        // Path.Combine argument rooted, silently discarding _sessionsDir).
        var safeId = Path.GetFileName(sessionId);
        if (string.IsNullOrEmpty(safeId))
            return null;

        var session = new SessionData
        {
            Id = safeId,
            CreatedAt = DateTime.UtcNow,
            MessageCount = history.Count,
            PromptOffset = history.PromptOffset,
            TurnOffset = history.TurnOffset,
            HandoffStack = handoff?.Stack.ToList(),
            HandoffPersona = handoff?.Persona,
            Messages = history.Messages.Select(SerializeMessage).ToList()
        };

        return (safeId, GetSessionPath(safeId), JsonSerializer.Serialize(session, JsonOptions));
    }

    /// <summary>
    /// Load a conversation from disk.
    /// </summary>
    public async Task<ConversationHistory?> LoadAsync(string sessionId)
    {
        // Same sanitization as SaveAsync -- an id can't escape the sessions directory.
        var safeId = Path.GetFileName(sessionId);
        if (string.IsNullOrEmpty(safeId))
            return null;

        var path = GetSessionPath(safeId);
        if (!File.Exists(path))
            return null;

        List<ChatMessage> chatMessages;
        int promptOffset, turnOffset;
        try
        {
            var json = await File.ReadAllTextAsync(path);
            var session = JsonSerializer.Deserialize<SessionData>(json, JsonOptions);
            if (session is null) return null;

            // DeserializeMessage is called here, inside the same try, not in a
            // separate loop below -- it's JSON-valid-but-malformed content (e.g. a
            // tool_use block missing "input") that this needs to catch too, not just
            // a parse failure. GetProperty throws KeyNotFoundException for a missing
            // field and GetString()/GetBoolean() throw InvalidOperationException for
            // a field of the wrong JSON type, both plausible from a hand-edited or
            // version-skewed session file.
            chatMessages = session.Messages.Select(DeserializeMessage).ToList();
            promptOffset = session.PromptOffset;
            turnOffset = session.TurnOffset;
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException
            or KeyNotFoundException or InvalidOperationException)
        {
            // Corrupt/partially-written/unreadable/malformed session file -- treat
            // like "not found" rather than surfacing a raw deserialization exception.
            return null;
        }

        var history = new ConversationHistory();
        foreach (var chatMsg in chatMessages)
        {
            if (chatMsg.Role == MessageRole.Assistant)
                history.AddRestored(chatMsg);
            else if (chatMsg.Content.Any(b => b is ToolResultBlock))
                history.AddRestored(ChatMessage.ToolResults(chatMsg.Content.OfType<ToolResultBlock>()) with { Timestamp = chatMsg.Timestamp });
            else
                history.AddRestored(ChatMessage.UserText(chatMsg.GetText(), chatMsg.Kind) with { Timestamp = chatMsg.Timestamp });
        }

        history.RestoreOffsets(promptOffset, turnOffset);
        return history;
    }

    /// <summary>
    /// The handoff state saved with a session: who handed the conversation over, so it can be
    /// returned after loading. Null if the session was never handed over (including every
    /// session saved before handoff existed), or can't be read.
    /// </summary>
    public async Task<HandoffState?> LoadHandoffAsync(string sessionId)
    {
        var safeId = Path.GetFileName(sessionId);
        if (string.IsNullOrEmpty(safeId))
            return null;

        var path = GetSessionPath(safeId);
        if (!File.Exists(path))
            return null;

        try
        {
            var session = JsonSerializer.Deserialize<SessionData>(await File.ReadAllTextAsync(path), JsonOptions);
            return session is { HandoffStack.Count: > 0, HandoffPersona: not null }
                ? new HandoffState(session.HandoffStack, session.HandoffPersona)
                : null;
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>
    /// List all saved sessions, most recently saved first.
    /// </summary>
    public IReadOnlyList<SessionInfo> ListSessions()
    {
        if (!Directory.Exists(_sessionsDir))
            return [];

        var sessions = new List<SessionInfo>();
        foreach (var file in Directory.GetFiles(_sessionsDir, "*.json"))
        {
            try
            {
                var json = File.ReadAllText(file);
                var session = JsonSerializer.Deserialize<SessionData>(json, JsonOptions);
                if (session is not null)
                {
                    sessions.Add(new SessionInfo
                    {
                        Id = session.Id,
                        CreatedAt = session.CreatedAt,
                        MessageCount = session.MessageCount,
                        QuestionCount = CountQuestions(session)
                    });
                }
            }
            catch
            {
                // Skip corrupt session files
            }
        }
        // CreatedAt is stamped on every save, so this is newest-saved first.
        return sessions.OrderByDescending(s => s.CreatedAt).ToList();
    }

    /// <summary>
    /// How many prompts the session holds, counted the way <see cref="ConversationHistory.QuestionCount"/> does: user messages
    /// with text (not tool results), plus any /compact dropped from the front. Works on older sessions too.
    /// </summary>
    private static int CountQuestions(SessionData session) =>
        session.PromptOffset + session.Messages.Count(m => m.Role == MessageRole.User && m.Content.Any(HasText));

    private static bool HasText(JsonElement block) =>
        block.ValueKind == JsonValueKind.Object
        && block.TryGetProperty("type", out var type) && type.GetString() == "text"
        && block.TryGetProperty("text", out var text) && !string.IsNullOrEmpty(text.GetString());

    private string GetSessionPath(string sessionId) =>
        Path.Combine(_sessionsDir, $"{sessionId}.json");

    private static bool IsUserText(ChatMessage msg) =>
        msg.Role == MessageRole.User && msg.Content.OfType<TextBlock>().Any(b => b.Text.Length > 0);

    private static SerializedMessage SerializeMessage(ChatMessage msg) => new()
    {
        Role = msg.Role,
        Timestamp = msg.Timestamp,
        Kind = IsUserText(msg) || msg.Kind is PromptKind.Err || msg.Kind.IsLocalRecord() ? msg.Kind.ToCode() : null,
        Content = msg.Content.Select(SerializeContentBlock).ToList()
    };

    private static JsonElement SerializeContentBlock(ContentBlock block)
    {
        // Must serialize with runtime type to include the abstract "type" property
        return block switch
        {
            TextBlock tb => JsonSerializer.SerializeToElement(
                new { type = "text", text = tb.Text }, JsonOptions),
            ToolUseBlock tub => JsonSerializer.SerializeToElement(
                new { type = "tool_use", id = tub.Id, name = tub.Name, input = tub.Input }, JsonOptions),
            ToolResultBlock trb => JsonSerializer.SerializeToElement(
                new { type = "tool_result", tool_use_id = trb.ToolUseId, content = trb.Content, is_error = trb.IsError }, JsonOptions),
            _ => JsonSerializer.SerializeToElement(new { type = "unknown" }, JsonOptions)
        };
    }

    private static ChatMessage DeserializeMessage(SerializedMessage msg)
    {
        var blocks = new List<ContentBlock>();
        foreach (var element in msg.Content)
        {
            var type = element.GetProperty("type").GetString();
            switch (type)
            {
                case "text":
                    blocks.Add(new TextBlock { Text = element.GetProperty("text").GetString()! });
                    break;
                case "tool_use":
                    blocks.Add(new ToolUseBlock
                    {
                        Id = element.GetProperty("id").GetString()!,
                        Name = element.GetProperty("name").GetString()!,
                        Input = element.GetProperty("input").Clone()
                    });
                    break;
                case "tool_result":
                    blocks.Add(new ToolResultBlock
                    {
                        ToolUseId = element.GetProperty("tool_use_id").GetString()!,
                        Content = element.GetProperty("content").GetString()!,
                        IsError = element.TryGetProperty("is_error", out var ie) && ie.GetBoolean()
                    });
                    break;
            }
        }

        return new ChatMessage { Role = msg.Role, Content = blocks, Timestamp = msg.Timestamp, Kind = PromptKinds.Parse(msg.Kind) };
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };
}

public record SessionData
{
    public required string Id { get; init; }
    public DateTime CreatedAt { get; init; }
    public int MessageCount { get; init; }
    /// <summary>Prompts and turns /compact dropped from the front; absent (0) in a session never compacted.</summary>
    public int PromptOffset { get; init; }
    public int TurnOffset { get; init; }
    /// <summary>Personas that handed the conversation over, most recent last; absent if it was never handed over.</summary>
    public List<string>? HandoffStack { get; init; }
    /// <summary>The persona in charge when saved; present only with <see cref="HandoffStack"/>.</summary>
    public string? HandoffPersona { get; init; }
    public required List<SerializedMessage> Messages { get; init; }
}

public record SerializedMessage
{
    public required string Role { get; init; }
    /// <summary>Absent in sessions saved before timestamps were recorded.</summary>
    public DateTimeOffset? Timestamp { get; init; }
    /// <summary>"std" (typed by the user), "sys" (loop nudge) or "chk" (/save and /load checkpoint) on user text messages; absent otherwise and in older sessions (read as std).</summary>
    public string? Kind { get; init; }
    public required List<JsonElement> Content { get; init; }
}

public record SessionInfo
{
    public required string Id { get; init; }
    public DateTime CreatedAt { get; init; }
    public int MessageCount { get; init; }
    /// <summary>Prompts in the session (the number the REPL and transcripts count), a more meaningful size than messages.</summary>
    public int QuestionCount { get; init; }
}
