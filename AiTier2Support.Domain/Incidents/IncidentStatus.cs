namespace AiTier2Support.Domain.Incidents;

public enum IncidentStatus
{
    Open = 0,
    Investigating = 1,
    AwaitingApproval = 2,
    Remediating = 3,
    Verifying = 4,
    Resolved = 5,
    Escalated = 6
}
