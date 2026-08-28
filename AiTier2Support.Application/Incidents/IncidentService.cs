using AiTier2Support.Application.Common;
using AiTier2Support.Application.ReferenceEnvironment;
using AiTier2Support.Domain.Incidents;
using FluentValidation;

namespace AiTier2Support.Application.Incidents;

public sealed class IncidentService : IIncidentService
{
    private readonly IApplicationDbContext _db;
    private readonly IReferenceEnvironment _environment;
    private readonly IValidator<CreateIncidentRequest> _validator;

    public IncidentService(
        IApplicationDbContext db,
        IReferenceEnvironment environment,
        IValidator<CreateIncidentRequest> validator)
    {
        _db = db;
        _environment = environment;
        _validator = validator;
    }

    public async Task<IncidentSummaryDto> CreateAsync(CreateIncidentRequest request, CancellationToken cancellationToken)
    {
        await _validator.ValidateAndThrowAsync(request, cancellationToken);
        _environment.ResetScenario(request.ScenarioId);

        var incident = new Incident
        {
            Title = request.Title,
            Description = request.Description,
            Severity = request.Severity,
            ScenarioId = request.ScenarioId,
            Status = IncidentStatus.Open
        };

        await _db.AddIncidentAsync(incident, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
        return IncidentMapper.ToSummary(incident);
    }

    public async Task<IReadOnlyList<IncidentSummaryDto>> GetAllAsync(CancellationToken cancellationToken)
    {
        var incidents = await _db.GetIncidentsAsync(cancellationToken);
        return incidents.OrderByDescending(i => i.CreatedAt).Select(IncidentMapper.ToSummary).ToList();
    }

    public async Task<IncidentDetailDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var incident = await _db.GetIncidentAsync(id, cancellationToken);
        if (incident is null) return null;

        var timeline = BuildTimeline(incident);
        return new IncidentDetailDto(
            incident.Id, incident.Title, incident.Description,
            incident.Severity.ToString(), incident.Status.ToString(),
            incident.ScenarioId, incident.CreatedAt,
            incident.RootCause, incident.RecommendedAction,
            incident.Confidence, incident.RiskLevel?.ToString(),
            IncidentMapper.ParseDiagnosis(incident.DiagnosisJson),
            incident.Evidence.OrderByDescending(e => e.ObservedAt).Select(IncidentMapper.ToEvidence).ToList(),
            timeline,
            incident.Actions.Select(IncidentMapper.ToAction).ToList(),
            incident.Report is null ? null : new IncidentReportDto(
                incident.Report.Id, incident.Report.Content, incident.Report.GeneratedAt),
            incident.EscalationReason, incident.EscalationNextStep);
    }

    public async Task<DashboardStatsDto> GetDashboardStatsAsync(CancellationToken cancellationToken)
    {
        var incidents = await _db.GetIncidentsAsync(cancellationToken);
        return new DashboardStatsDto(
            incidents.Count,
            incidents.Count(i => i.Status == IncidentStatus.Open),
            incidents.Count(i => i.Status == IncidentStatus.Investigating),
            incidents.Count(i => i.Status == IncidentStatus.AwaitingApproval),
            incidents.Count(i => i.Status == IncidentStatus.Resolved),
            incidents.Count(i => i.Status == IncidentStatus.Escalated),
            incidents.OrderByDescending(i => i.CreatedAt).Take(5).Select(IncidentMapper.ToSummary).ToList());
    }

    public async Task<IReadOnlyList<TimelineEventDto>> GetTimelineAsync(Guid id, CancellationToken cancellationToken)
    {
        var incident = await _db.GetIncidentAsync(id, cancellationToken);
        return incident is null ? [] : BuildTimeline(incident);
    }

    public async Task<IReadOnlyList<EvidenceDto>> GetEvidenceAsync(Guid id, CancellationToken cancellationToken)
    {
        var incident = await _db.GetIncidentAsync(id, cancellationToken);
        return incident?.Evidence.OrderByDescending(e => e.ObservedAt).Select(IncidentMapper.ToEvidence).ToList() ?? [];
    }

    public async Task<IncidentReportDto?> GetReportAsync(Guid id, CancellationToken cancellationToken)
    {
        var incident = await _db.GetIncidentAsync(id, cancellationToken);
        if (incident?.Report is null) return null;
        return new IncidentReportDto(incident.Report.Id, incident.Report.Content, incident.Report.GeneratedAt);
    }

    private static List<TimelineEventDto> BuildTimeline(Incident incident)
    {
        var events = new List<TimelineEventDto>
        {
            new("Incident created", "completed", incident.CreatedAt, incident.Title)
        };

        foreach (var run in incident.AgentRuns.OrderBy(r => r.CreatedAt))
        {
            events.Add(new("Agent started", "completed", run.CreatedAt, null));
            foreach (var tool in run.ToolExecutions.OrderBy(t => t.Sequence))
            {
                events.Add(new(FormatToolName(tool.ToolName),
                    tool.Success ? "completed" : "failed",
                    tool.CreatedAt, tool.Success ? null : tool.ErrorMessage));
            }
            if (run.CompletedAt.HasValue)
            {
                events.Add(new($"Agent {run.Status}", run.Status.ToString().ToLowerInvariant(),
                    run.CompletedAt.Value, run.FailureReason));
            }
        }

        foreach (var action in incident.Actions.OrderBy(a => a.CreatedAt))
        {
            events.Add(new($"Action: {action.ActionType}", action.Status.ToString().ToLowerInvariant(),
                action.ExecutedAt ?? action.CreatedAt, action.Reason));
            if (action.ApprovalRequest is not null)
            {
                events.Add(new("Approval required", action.ApprovalRequest.Status.ToString().ToLowerInvariant(),
                    action.ApprovalRequest.CreatedAt, action.Reason));
            }
        }

        if (incident.Status == IncidentStatus.Resolved)
            events.Add(new("Incident resolved", "completed", incident.UpdatedAt ?? incident.CreatedAt, null));
        if (incident.Status == IncidentStatus.Escalated)
            events.Add(new("Incident escalated", "escalated", incident.UpdatedAt ?? incident.CreatedAt, incident.EscalationReason));

        return events.OrderBy(e => e.Timestamp).ToList();
    }

    private static string FormatToolName(string name) => name switch
    {
        "check_service_health" => "Checked service health",
        "get_service_metrics" => "Retrieved service metrics",
        "search_application_logs" => "Searched application logs",
        "get_database_metrics" => "Checked database metrics",
        "get_recent_deployment" => "Checked recent deployment",
        "get_deployment_details" => "Retrieved deployment details",
        "verify_service_health" => "Verified service health",
        "restart_worker" => "Restarted worker",
        "rollback_deployment" => "Rolled back deployment",
        _ => name
    };
}
