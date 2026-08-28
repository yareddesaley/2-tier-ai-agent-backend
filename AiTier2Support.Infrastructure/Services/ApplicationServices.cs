using AiTier2Support.Application.Agents;
using AiTier2Support.Application.Common;
using AiTier2Support.Application.Incidents;
using AiTier2Support.Domain.Incidents;
using AiTier2Support.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AiTier2Support.Infrastructure.Services;

public sealed class InvestigationService : IInvestigationService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IApplicationDbContext _db;
    private readonly ILogger<InvestigationService> _logger;

    public InvestigationService(
        IServiceScopeFactory scopeFactory,
        IApplicationDbContext db,
        ILogger<InvestigationService> logger)
    {
        _scopeFactory = scopeFactory;
        _db = db;
        _logger = logger;
    }

    public async Task<AgentInvestigationResult> StartInvestigationAsync(Guid incidentId, CancellationToken cancellationToken)
    {
        var incident = await _db.GetIncidentAsync(incidentId, cancellationToken)
            ?? throw new KeyNotFoundException($"Incident {incidentId} not found.");

        if (incident.Status is IncidentStatus.Investigating or IncidentStatus.Remediating or IncidentStatus.Verifying)
        {
            return new AgentInvestigationResult { Status = incident.Status.ToString() };
        }

        if (incident.Status is IncidentStatus.Resolved or IncidentStatus.Escalated)
            throw new InvalidOperationException($"Cannot investigate incident in status {incident.Status}.");

        IncidentStateMachine.Transition(incident, IncidentStatus.Investigating);
        await _db.SaveChangesAsync(cancellationToken);

        _ = Task.Run(async () =>
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var orchestrator = scope.ServiceProvider.GetRequiredService<IAgentOrchestrator>();
                await orchestrator.InvestigateAsync(incidentId, CancellationToken.None);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Background investigation failed for incident {IncidentId}", incidentId);
            }
        }, CancellationToken.None);

        return new AgentInvestigationResult { Status = "Investigating" };
    }
}

public sealed class ActionService : IActionService
{
    private readonly AppDbContext _db;
    private readonly IAgentOrchestrator _orchestrator;

    public ActionService(AppDbContext db, IAgentOrchestrator orchestrator)
    {
        _db = db;
        _orchestrator = orchestrator;
    }

    public async Task ApproveAsync(Guid actionId, string? reviewer, CancellationToken cancellationToken)
    {
        await _orchestrator.ContinueAfterApprovalAsync(actionId, true, reviewer, cancellationToken);
    }

    public async Task RejectAsync(Guid actionId, string? reviewer, string? notes, CancellationToken cancellationToken)
    {
        var action = await _db.AgentActions.Include(a => a.ApprovalRequest)
            .FirstOrDefaultAsync(a => a.Id == actionId, cancellationToken);
        if (action?.ApprovalRequest is not null && !string.IsNullOrWhiteSpace(notes))
            action.ApprovalRequest.ReviewNotes = notes;

        await _orchestrator.ContinueAfterApprovalAsync(actionId, false, reviewer, cancellationToken);
    }

    public async Task<IReadOnlyList<AgentActionDto>> GetActionsForIncidentAsync(Guid incidentId, CancellationToken cancellationToken)
    {
        var actions = await _db.AgentActions
            .Include(a => a.ApprovalRequest)
            .Where(a => a.IncidentId == incidentId)
            .OrderByDescending(a => a.CreatedAt)
            .ToListAsync(cancellationToken);

        return actions.Select(IncidentMapper.ToAction).ToList();
    }
}
