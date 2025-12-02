using BBT.Aether.AspNetCore.Events;
using BBT.Aether.Events;
using BBT.Aether.MultiSchema;
using BBT.Aether.Uow;
using Microsoft.AspNetCore.Mvc;

namespace BBT.Workflow.Workers.Inbox.Controllers;

[Route("events/{name}/{version}")]
public class DaprEventController(
    IInboxStore inboxStore,
    IUnitOfWorkManager unitOfWorkManager,
    IEventSerializer serializer,
    ICurrentSchema currentSchema,
    ILogger<EventsController> logger)
    : EventsController(inboxStore, unitOfWorkManager, serializer, currentSchema, logger)
{
    /// <summary>
    /// Handles incoming events from Dapr.
    /// This action simply delegates to the base ProcessEventAsync method.
    /// </summary>
    /// <param name="name">Event name from route</param>
    /// <param name="version">Event version from route</param>
    /// <param name="cancellationToken">Cancellation token</param>
    [HttpPost]
    public async Task<IActionResult> Post(
        [FromRoute] string name,
        [FromRoute] string version,
        CancellationToken cancellationToken)
    {
        return await ProcessEventAsync(name, version, cancellationToken);
    }
}