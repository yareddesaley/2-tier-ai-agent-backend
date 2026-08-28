using AiTier2Support.Domain.Incidents;

namespace AiTier2Support.Domain.Incidents;

public static class IncidentStateMachine
{
    private static readonly Dictionary<IncidentStatus, HashSet<IncidentStatus>> AllowedTransitions = new()
    {
        [IncidentStatus.Open] = [IncidentStatus.Investigating, IncidentStatus.Escalated],
        [IncidentStatus.Investigating] = [IncidentStatus.AwaitingApproval, IncidentStatus.Remediating, IncidentStatus.Escalated, IncidentStatus.Verifying],
        [IncidentStatus.AwaitingApproval] = [IncidentStatus.Remediating, IncidentStatus.Escalated, IncidentStatus.Investigating],
        [IncidentStatus.Remediating] = [IncidentStatus.Verifying, IncidentStatus.Escalated, IncidentStatus.AwaitingApproval],
        [IncidentStatus.Verifying] = [IncidentStatus.Resolved, IncidentStatus.Escalated, IncidentStatus.Investigating],
        [IncidentStatus.Resolved] = [],
        [IncidentStatus.Escalated] = []
    };

    public static bool CanTransition(IncidentStatus from, IncidentStatus to) =>
        AllowedTransitions.TryGetValue(from, out var targets) && targets.Contains(to);

    public static void Transition(Incident incident, IncidentStatus to)
    {
        if (!CanTransition(incident.Status, to))
            throw new InvalidOperationException($"Cannot transition incident from {incident.Status} to {to}.");

        incident.Status = to;
        incident.UpdatedAt = DateTime.UtcNow;
    }
}
