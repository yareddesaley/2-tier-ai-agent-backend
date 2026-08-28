using System.Text.Json;

namespace AiTier2Support.Application.Tools;

public sealed class ToolResult
{
    public bool Success { get; init; } = true;
    public object? Data { get; init; }
    public string? Error { get; init; }

    public string ToJson() => JsonSerializer.Serialize(Success ? Data : new { error = Error });
}

public interface IAgentTool
{
    string Name { get; }
    string Description { get; }
    string ParametersJsonSchema { get; }
    Task<ToolResult> ExecuteAsync(JsonElement arguments, CancellationToken cancellationToken);
}

public interface IAgentToolRegistry
{
    IReadOnlyList<IAgentTool> GetAllTools();
    IReadOnlyList<IAgentTool> GetInvestigationTools();
    IReadOnlyList<IAgentTool> GetActionTools();
    IAgentTool? GetTool(string name);
}
