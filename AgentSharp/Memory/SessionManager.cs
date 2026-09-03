using System.Text.Json;
using System.Text.Json.Serialization;
using AgentSharp.Agent;
using AgentSharp.Llm;

namespace AgentSharp.Memory;

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

    /// <summary>
    /// Save a conversation to disk. Returns null (instead of throwing) if
    /// <paramref name="sessionId"/> isn't a usable file name or the write fails
    /// (e.g. permission denied) -- a bad /save shouldn't crash the REPL.
    /// </summary>
    public async Task<string?> SaveAsync(ConversationHistory history, string? sessionId = null)
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
            Messages = history.Messages.Select(SerializeMessage).ToList()
        };

        var path = GetSessionPath(safeId);
        var json = JsonSerializer.Serialize(session, JsonOptions);

        // Write to a unique temp file and rename into place so a concurrent Load,
        // or a crash mid-write, never sees a partially-written session file.
        var tempPath = $"{path}.{Guid.NewGuid():N}.tmp";
        try
        {
            await File.WriteAllTextAsync(tempPath, json);
            File.Move(tempPath, path, overwrite: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }

        return safeId;
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
                history.AddAssistantMessage(chatMsg);
            else if (chatMsg.Content.Any(b => b is ToolResultBlock))
                history.AddToolResults(chatMsg.Content.OfType<ToolResultBlock>());
            else
                history.AddUserMessage(chatMsg.GetText());
        }

        return history;
    }

    /// <summary>
    /// List all saved sessions.
    /// </summary>
    public IReadOnlyList<SessionInfo> ListSessions()
    {
        if (!Directory.Exists(_sessionsDir))
            return [];

        var sessions = new List<SessionInfo>();
        foreach (var file in Directory.GetFiles(_sessionsDir, "*.json").OrderByDescending(f => f))
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
                        MessageCount = session.MessageCount
                    });
                }
            }
            catch
            {
                // Skip corrupt session files
            }
        }
        return sessions;
    }

    private string GetSessionPath(string sessionId) =>
        Path.Combine(_sessionsDir, $"{sessionId}.json");

    private static SerializedMessage SerializeMessage(ChatMessage msg) => new()
    {
        Role = msg.Role,
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

        return new ChatMessage { Role = msg.Role, Content = blocks };
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
    public required List<SerializedMessage> Messages { get; init; }
}

public record SerializedMessage
{
    public required string Role { get; init; }
    public required List<JsonElement> Content { get; init; }
}

public record SessionInfo
{
    public required string Id { get; init; }
    public DateTime CreatedAt { get; init; }
    public int MessageCount { get; init; }
}
