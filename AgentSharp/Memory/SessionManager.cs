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
    /// Save a conversation to disk.
    /// </summary>
    public async Task<string> SaveAsync(ConversationHistory history, string? sessionId = null)
    {
        sessionId ??= Guid.NewGuid().ToString("N")[..8];
        var session = new SessionData
        {
            Id = sessionId,
            CreatedAt = DateTime.UtcNow,
            MessageCount = history.Count,
            Messages = history.Messages.Select(SerializeMessage).ToList()
        };

        var path = GetSessionPath(sessionId);
        var json = JsonSerializer.Serialize(session, JsonOptions);

        // Write to a unique temp file and rename into place so a concurrent Load,
        // or a crash mid-write, never sees a partially-written session file.
        var tempPath = $"{path}.{Guid.NewGuid():N}.tmp";
        await File.WriteAllTextAsync(tempPath, json);
        File.Move(tempPath, path, overwrite: true);

        return sessionId;
    }

    /// <summary>
    /// Load a conversation from disk.
    /// </summary>
    public async Task<ConversationHistory?> LoadAsync(string sessionId)
    {
        var path = GetSessionPath(sessionId);
        if (!File.Exists(path))
            return null;

        SessionData? session;
        try
        {
            var json = await File.ReadAllTextAsync(path);
            session = JsonSerializer.Deserialize<SessionData>(json, JsonOptions);
        }
        catch (JsonException)
        {
            // Corrupt or partially-written session file -- treat like "not found"
            // rather than crashing the REPL.
            return null;
        }
        if (session is null) return null;

        var history = new ConversationHistory();
        foreach (var msg in session.Messages)
        {
            var chatMsg = DeserializeMessage(msg);
            if (chatMsg.Role == MessageRole.Assistant)
                history.AddAssistantMessage(chatMsg);
            else if (chatMsg.HasToolUse || chatMsg.Content.Any(b => b is ToolResultBlock))
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
