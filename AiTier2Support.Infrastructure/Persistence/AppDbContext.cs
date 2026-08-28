using AiTier2Support.Application.Common;
using AiTier2Support.Domain.Incidents;
using Microsoft.EntityFrameworkCore;

namespace AiTier2Support.Infrastructure.Persistence;

public sealed class AppDbContext : DbContext, IApplicationDbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Incident> Incidents => Set<Incident>();
    public DbSet<AgentRun> AgentRuns => Set<AgentRun>();
    public DbSet<AgentMessage> AgentMessages => Set<AgentMessage>();
    public DbSet<ToolExecution> ToolExecutions => Set<ToolExecution>();
    public DbSet<Evidence> Evidence => Set<Evidence>();
    public DbSet<AgentAction> AgentActions => Set<AgentAction>();
    public DbSet<ApprovalRequest> ApprovalRequests => Set<ApprovalRequest>();
    public DbSet<IncidentReport> IncidentReports => Set<IncidentReport>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Incident>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Title).HasMaxLength(200);
            e.HasIndex(x => x.Status);
            e.HasIndex(x => x.CreatedAt);
            e.HasOne(x => x.Report).WithOne(x => x.Incident).HasForeignKey<IncidentReport>(x => x.IncidentId);
        });

        modelBuilder.Entity<AgentRun>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.IncidentId);
            e.HasOne(x => x.Incident).WithMany(x => x.AgentRuns).HasForeignKey(x => x.IncidentId);
        });

        modelBuilder.Entity<AgentMessage>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.AgentRunId);
            e.HasOne(x => x.AgentRun).WithMany(x => x.Messages).HasForeignKey(x => x.AgentRunId);
        });

        modelBuilder.Entity<ToolExecution>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.AgentRunId);
            e.HasOne(x => x.AgentRun).WithMany(x => x.ToolExecutions).HasForeignKey(x => x.AgentRunId);
        });

        modelBuilder.Entity<Evidence>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.IncidentId);
            e.HasOne(x => x.Incident).WithMany(x => x.Evidence).HasForeignKey(x => x.IncidentId);
        });

        modelBuilder.Entity<AgentAction>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.IncidentId);
            e.HasOne(x => x.Incident).WithMany(x => x.Actions).HasForeignKey(x => x.IncidentId);
            e.HasOne(x => x.ApprovalRequest).WithOne(x => x.AgentAction).HasForeignKey<ApprovalRequest>(x => x.AgentActionId);
        });

        modelBuilder.Entity<ApprovalRequest>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.Status);
        });

        modelBuilder.Entity<IncidentReport>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.IncidentId);
        });
    }

    public Task<Incident?> GetIncidentAsync(Guid id, CancellationToken cancellationToken) =>
        Incidents
            .Include(i => i.AgentRuns).ThenInclude(r => r.ToolExecutions)
            .Include(i => i.AgentRuns).ThenInclude(r => r.Messages)
            .Include(i => i.Evidence)
            .Include(i => i.Actions).ThenInclude(a => a.ApprovalRequest)
            .Include(i => i.Report)
            .FirstOrDefaultAsync(i => i.Id == id, cancellationToken);

    public Task<IReadOnlyList<Incident>> GetIncidentsAsync(CancellationToken cancellationToken) =>
        Incidents.OrderByDescending(i => i.CreatedAt).ToListAsync(cancellationToken)
            .ContinueWith(t => (IReadOnlyList<Incident>)t.Result, cancellationToken);

    public async Task AddIncidentAsync(Incident incident, CancellationToken cancellationToken) =>
        await Incidents.AddAsync(incident, cancellationToken);

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
        base.SaveChangesAsync(cancellationToken);

    async Task IApplicationDbContext.SaveChangesAsync(CancellationToken cancellationToken)
    {
        await SaveChangesAsync(cancellationToken);
    }
}
