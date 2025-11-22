using BBT.Aether.Domain.Entities;
using BBT.Aether.Domain.EntityFrameworkCore;
using BBT.Aether.Domain.EntityFrameworkCore.Modeling;
using BBT.Aether.Domain.Events;
using BBT.Aether.Persistence;
using BBT.Workflow.Instances;
using BBT.Workflow.Schemas;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace BBT.Workflow.Data;

/// <summary>
/// Database context for workflow engine persistence.
/// Supports multi-schema architecture for multi-tenancy.
/// </summary>
public class WorkflowDbContext(
    DbContextOptions<WorkflowDbContext> options,
    ICurrentSchema currentSchema,
    IServiceProvider? serviceProvider = null)
    : AetherDbContext<WorkflowDbContext>(options), 
        IDbContextSchema, IHasEfCoreInbox, IHasEfCoreOutbox, IHasEfCoreBackgroundJobs
{
    /// <inheritdoc />
    public string? SchemaName => currentSchema?.Name;

    /// <summary>
    /// Gets or sets the workflow instances.
    /// </summary>
    public virtual DbSet<Instance> Instances { get; set; }
    
    /// <summary>
    /// Gets or sets the instance correlations for subflow relationships.
    /// </summary>
    public virtual DbSet<InstanceCorrelation> InstanceCorrelations { get; set; }
    
    /// <summary>
    /// Gets or sets the instance data versions.
    /// </summary>
    public virtual DbSet<InstanceData> InstancesData { get; set; }
    
    /// <summary>
    /// Gets or sets the instance actions (deprecated).
    /// </summary>
    public virtual DbSet<InstanceAction> InstanceActions { get; set; }
    
    /// <summary>
    /// Gets or sets the instance task execution records.
    /// </summary>
    public virtual DbSet<InstanceTask> InstanceTasks { get; set; }
    
    /// <summary>
    /// Gets or sets the instance transition execution records.
    /// </summary>
    public virtual DbSet<InstanceTransition> InstanceTransitions { get; set; }
    
    /// <summary>
    /// Gets or sets the instance background jobs.
    /// </summary>
    public virtual DbSet<InstanceJob> InstanceJobs { get; set; }
    
    /// <summary>
    /// Gets or sets the inbox messages
    /// </summary>
    public virtual DbSet<InboxMessage> InboxMessages { get; set; }
    
    /// <summary>
    /// Gets or sets the outbox messages
    /// </summary>
    public virtual DbSet<OutboxMessage> OutboxMessages { get; set; }
    
    /// <summary>
    /// Gets or sets the background jobs
    /// </summary>
    public virtual DbSet<BackgroundJobInfo> BackgroundJobs { get; set; }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        builder.HasDefaultSchema(SchemaName);
        
        base.OnModelCreating(builder);

        builder.ConfigureWorkflow(SchemaName);
        builder.ConfigureInbox(SchemaName);
        builder.ConfigureOutbox(SchemaName);
        builder.ConfigureBackgroundJob(SchemaName);
    }

    
}