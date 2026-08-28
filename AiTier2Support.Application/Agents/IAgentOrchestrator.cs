using AiTier2Support.Application.Agents;

namespace AiTier2Support.Application.Agents;

public interface IAgentOrchestrator
{
    Task<AgentInvestigationResult> InvestigateAsync(Guid incidentId, CancellationToken cancellationToken);
    Task ContinueAfterApprovalAsync(Guid actionId, bool approved, string? reviewer, CancellationToken cancellationToken);
}

public sealed class AgentInvestigationResult
{
    public Guid AgentRunId { get; init; }
    public string Status { get; init; } = string.Empty;
    public AgentDiagnosis? Diagnosis { get; init; }
    public bool RequiresApproval { get; init; }
    public Guid? PendingActionId { get; init; }
}

public sealed class AgentDiagnosis
{
    public string Summary { get; init; } = string.Empty;
    public string RootCause { get; init; } = string.Empty;
    public IReadOnlyList<string> Evidence { get; init; } = [];
    public IReadOnlyList<string> AlternativeCauses { get; init; } = [];
    public double Confidence { get; init; }
    public string RecommendedAction { get; init; } = string.Empty;
    public string RiskLevel { get; init; } = "low";
}
