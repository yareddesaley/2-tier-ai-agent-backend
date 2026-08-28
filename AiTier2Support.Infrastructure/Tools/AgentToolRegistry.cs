using AiTier2Support.Application.Tools;

namespace AiTier2Support.Infrastructure.Tools;

public sealed class AgentToolRegistry : IAgentToolRegistry
{
    private readonly Dictionary<string, IAgentTool> _tools;

    public AgentToolRegistry(IEnumerable<IAgentTool> tools)
    {
        _tools = tools.ToDictionary(t => t.Name, StringComparer.OrdinalIgnoreCase);
    }

    public IReadOnlyList<IAgentTool> GetAllTools() => _tools.Values.ToList();

    public IReadOnlyList<IAgentTool> GetInvestigationTools() =>
        _tools.Values.Where(t => t.Name is not ("restart_worker" or "rollback_deployment" or "submit_diagnosis")).ToList();

    public IReadOnlyList<IAgentTool> GetActionTools() =>
        _tools.Values.Where(t => t.Name is "restart_worker" or "rollback_deployment" or "verify_service_health").ToList();

    public IAgentTool? GetTool(string name) =>
        _tools.TryGetValue(name, out var tool) ? tool : null;
}
