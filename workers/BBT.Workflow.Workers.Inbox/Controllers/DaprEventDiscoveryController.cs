using BBT.Aether.AspNetCore.Events;
using BBT.Aether.Events;
using Microsoft.AspNetCore.Mvc;

namespace BBT.Workflow.Workers.Inbox.Controllers;

[Route("dapr")]
public class DaprEventDiscoveryController(IDistributedEventInvokerRegistry invokerRegistry)
    : DaprDiscoveryController(invokerRegistry)
{
    /// <summary>
    /// Returns Dapr subscription configuration for all registered event handlers.
    /// This endpoint is called by Dapr runtime to discover subscriptions.
    /// </summary>
    /// <returns>JSON result containing subscription configuration</returns>
    [HttpGet("subscribe", Order = int.MinValue)]
    [ApiExplorerSettings(IgnoreApi = true)]
    public IActionResult Subscribe()
    {
        return GetSubscriptions();
    }
}