using AiTier2Support.Application.Agents;
using AiTier2Support.Domain.Actions;
using AiTier2Support.Domain.Approvals;
using AiTier2Support.Domain.Incidents;

namespace AiTier2Support.Application.Incidents;

public interface IInvestigationService
{
    Task<AgentInvestigationResult> StartInvestigationAsync(Guid incidentId, CancellationToken cancellationToken);
}

public interface IActionService
{
    Task ApproveAsync(Guid actionId, string? reviewer, CancellationToken cancellationToken);
    Task RejectAsync(Guid actionId, string? reviewer, string? notes, CancellationToken cancellationToken);
    Task<IReadOnlyList<AgentActionDto>> GetActionsForIncidentAsync(Guid incidentId, CancellationToken cancellationToken);
}

public static class IncidentMapper
{
    public static IncidentSummaryDto ToSummary(Incident i) => new(
        i.Id, i.Title, i.Description, i.Severity.ToString(), i.Status.ToString(),
        i.ScenarioId, i.CreatedAt, i.Confidence, i.RecommendedAction);

    public static EvidenceDto ToEvidence(Evidence e) => new(
        e.Id, e.Source, e.Tool, e.Observation, e.ObservedAt);

    public static AgentActionDto ToAction(AgentAction a) => new(
        a.Id, a.ActionType, a.Reason, a.RiskLevel.ToString(), a.Confidence,
        a.Status.ToString(),
        a.ApprovalRequest is null ? null : new ApprovalRequestDto(
            a.ApprovalRequest.Id,
            a.ApprovalRequest.Status.ToString(),
            a.ApprovalRequest.ReviewedBy,
            a.ApprovalRequest.ReviewedAt),
        a.VerificationJson);

    public static AgentDiagnosisDto? ParseDiagnosis(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try
        {
            var d = System.Text.Json.JsonSerializer.Deserialize<AgentDiagnosis>(json,
                new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (d is null) return null;
            return new AgentDiagnosisDto(d.Summary, d.RootCause, d.Evidence, d.AlternativeCauses,
                d.Confidence, d.RecommendedAction, d.RiskLevel);
        }
        catch { return null; }
    }
}
