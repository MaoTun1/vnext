using BBT.Aether.Domain.EntityFrameworkCore;
using BBT.Workflow.Instances;
using BBT.Workflow.Schemas;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace BBT.Workflow.Data;

public class WorkflowDbContext(
    DbContextOptions<WorkflowDbContext> options,
    ICurrentSchema currentSchema)
    : AetherDbContext<WorkflowDbContext>(options), IDbContextSchema
{
    public string? SchemaName => currentSchema?.Name;

    //Instances
    public virtual DbSet<Instance> Instances { get; set; }
    public virtual DbSet<InstanceCorrelation> InstanceCorrelations { get; set; }
    public virtual DbSet<InstanceData> InstancesData { get; set; }
    public virtual DbSet<InstanceAction> InstanceActions { get; set; }
    public virtual DbSet<InstanceTask> InstanceTasks { get; set; }
    public virtual DbSet<InstanceTransition> InstanceTransitions { get; set; }
    public virtual DbSet<InstanceJob> InstanceJobs { get; set; }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        builder.HasDefaultSchema(SchemaName);
        
        base.OnModelCreating(builder);

        builder.ConfigureWorkflow(SchemaName);
    }
}