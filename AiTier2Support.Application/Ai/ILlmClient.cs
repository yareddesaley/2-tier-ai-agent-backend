namespace AiTier2Support.Application.Ai;

public sealed class LlmMessage
{
    public string Role { get; init; } = string.Empty;
    public string Content { get; init; } = string.Empty;
    public IReadOnlyList<LlmToolCall>? ToolCalls { get; init; }
    public string? ToolCallId { get; init; }
    public string? ToolName { get; init; }
}

public sealed class LlmToolCall
{
    public string Id { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string ArgumentsJson { get; init; } = "{}";
    public string? ThoughtSignature { get; set; }
}

public sealed class LlmToolDefinition
{
    public string Name { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string ParametersJsonSchema { get; init; } = "{}";
}

public sealed class LlmRequest
{
    public string SystemPrompt { get; init; } = string.Empty;
    public IReadOnlyList<LlmMessage> Messages { get; init; } = [];
    public IReadOnlyList<LlmToolDefinition> Tools { get; init; } = [];
    public bool RequireStructuredOutput { get; init; }
    public string? StructuredOutputSchema { get; init; }
}

public sealed class LlmResponse
{
    public string? Text { get; init; }
    public IReadOnlyList<LlmToolCall> ToolCalls { get; init; } = [];
    public string? StructuredJson { get; init; }
    public bool IsComplete { get; init; }
}

public interface ILlmClient
{
    Task<LlmResponse> GenerateAsync(LlmRequest request, CancellationToken cancellationToken);
}
