using BBT.Aether.Domain.EntityFrameworkCore;
using BBT.Aether.Domain.EntityFrameworkCore.Modeling;
using BBT.Aether.Domain.Events;
using BBT.Aether.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BBT.Workflow.Data;

public class MessagingDbContext(
    DbContextOptions<MessagingDbContext> options)
    : AetherDbContext<MessagingDbContext>(options),
        IHasEfCoreInbox, IHasEfCoreOutbox
{
    /// <summary>
    /// Gets or sets the inbox messages
    /// </summary>
    public virtual DbSet<InboxMessage> InboxMessages { get; set; }

    /// <summary>
    /// Gets or sets the outbox messages
    /// </summary>
    public virtual DbSet<OutboxMessage> OutboxMessages { get; set; }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        builder.HasDefaultSchema("sys_queues");
        base.OnModelCreating(builder);

        builder.ConfigureInbox("sys_queues");
        builder.ConfigureOutbox("sys_queues");
    }
}