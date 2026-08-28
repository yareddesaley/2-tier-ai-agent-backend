using AiTier2Support.Domain.Actions;
using AiTier2Support.Domain.Incidents;

namespace AiTier2Support.Application.Incidents;

public sealed record CreateIncidentRequest(
    string Title,
    string Description,
    IncidentSeverity Severity,
    string ScenarioId);

public sealed record IncidentSummaryDto(
    Guid Id,
    string Title,
    string Description,
    string Severity,
    string Status,
    string ScenarioId,
    DateTime CreatedAt,
    double? Confidence,
    string? RecommendedAction);

public sealed record IncidentDetailDto(
    Guid Id,
    string Title,
    string Description,
    string Severity,
    string Status,
    string ScenarioId,
    DateTime CreatedAt,
    string? RootCause,
    string? RecommendedAction,
    double? Confidence,
    string? RiskLevel,
    AgentDiagnosisDto? Diagnosis,
    IReadOnlyList<EvidenceDto> Evidence,
    IReadOnlyList<TimelineEventDto> Timeline,
    IReadOnlyList<AgentActionDto> Actions,
    IncidentReportDto? Report,
    string? EscalationReason,
    string? EscalationNextStep);

public sealed record AgentDiagnosisDto(
    string Summary,
    string RootCause,
    IReadOnlyList<string> Evidence,
    IReadOnlyList<string> AlternativeCauses,
    double Confidence,
    string RecommendedAction,
    string RiskLevel);

public sealed record EvidenceDto(
    Guid Id,
    string Source,
    string Tool,
    string Observation,
    DateTime ObservedAt);

public sealed record TimelineEventDto(
    string Event,
    string Status,
    DateTime Timestamp,
    string? Detail);

public sealed record AgentActionDto(
    Guid Id,
    string ActionType,
    string Reason,
    string RiskLevel,
    double Confidence,
    string Status,
    ApprovalRequestDto? Approval,
    string? VerificationJson);

public sealed record ApprovalRequestDto(
    Guid Id,
    string Status,
    string? ReviewedBy,
    DateTime? ReviewedAt);

public sealed record IncidentReportDto(
    Guid Id,
    string Content,
    DateTime GeneratedAt);

public sealed record DashboardStatsDto(
    int Total,
    int Open,
    int Investigating,
    int AwaitingApproval,
    int Resolved,
    int Escalated,
    IReadOnlyList<IncidentSummaryDto> RecentIncidents);

public interface IIncidentService
{
    Task<IncidentSummaryDto> CreateAsync(CreateIncidentRequest request, CancellationToken cancellationToken);
    Task<IReadOnlyList<IncidentSummaryDto>> GetAllAsync(CancellationToken cancellationToken);
    Task<IncidentDetailDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<DashboardStatsDto> GetDashboardStatsAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<TimelineEventDto>> GetTimelineAsync(Guid id, CancellationToken cancellationToken);
    Task<IReadOnlyList<EvidenceDto>> GetEvidenceAsync(Guid id, CancellationToken cancellationToken);
    Task<IncidentReportDto?> GetReportAsync(Guid id, CancellationToken cancellationToken);
}
