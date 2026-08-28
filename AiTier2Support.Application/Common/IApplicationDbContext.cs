using AiTier2Support.Domain.Incidents;

namespace AiTier2Support.Application.Common;

public interface IApplicationDbContext
{
    Task<Incident?> GetIncidentAsync(Guid id, CancellationToken cancellationToken);
    Task<IReadOnlyList<Incident>> GetIncidentsAsync(CancellationToken cancellationToken);
    Task AddIncidentAsync(Incident incident, CancellationToken cancellationToken);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
