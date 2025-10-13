using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using BBT.Workflow.Functions;
using BBT.Workflow.Instances;
using BBT.Workflow.Runtime;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace BBT.Workflow.Controllers.Instances;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}")]
[ServiceFilter(typeof(ResponseHeaderFilter))]
public sealed class FunctionController(
    IFunctionAppService functionAppService,
    IInstanceQueryAppService queryAppService) : ControllerBase
{
    [HttpPatch("{domain}/functions")]
    public async Task<IActionResult> GetDomainFunctions(
        [FromRoute] string domain,
        [FromQuery] bool? async = false,
        CancellationToken cancellationToken = default)
    {
        var response = await functionAppService.GetDomainFunctions(domain, cancellationToken);
        return Ok(response);
    }

    [HttpGet("{domain}/functions/{function}")]
    public async Task<IActionResult> GetFunctionByKey(
        [FromRoute] string domain,
        [FromRoute] string function,
        [FromQuery] bool? async = false,
        CancellationToken cancellationToken = default)
    {
        var response = await functionAppService.GetFunctionByFunctionKey(function, RuntimeSysSchemaInfo.Functions,
            domain, cancellationToken);
        return Ok(response);
    }

    [HttpGet("{domain}/workflows/{workflow}/functions/{function}")]
    public async Task<IActionResult> GetFunctionByKey(
        [FromRoute] string domain,
        [FromRoute] string function,
        [FromRoute] string workflow,
        [FromQuery] bool? async = false,
        CancellationToken cancellationToken = default)
    {
        var response = await functionAppService.GetFunctionByFunctionKey(function, workflow, domain, cancellationToken);
        return Ok(response);
    }

    [HttpGet("{domain}/workflows/{workflow}/instances/{instance}/functions/{function}")]
    public async Task<IActionResult> GetFunctionWithInstance(
        [FromRoute] string domain,
        [FromRoute] string function,
        [FromRoute] string workflow,
        [FromRoute] string instance,
        [FromQuery] string? version = null,
        [FromQuery] string[]? extension = null,
        CancellationToken cancellationToken = default)
    {
        switch (function.ToLowerInvariant())
        {
            case Definitions.Functions.FunctionTypeConst.Longpooling:
                var inputLongpooling = new GetInstanceStateInput
                {
                    Domain = domain,
                    Workflow = workflow,
                    Instance = instance,
                    Version = version,
                    Extension = extension
                };
                var response = await queryAppService.GetInstanceStateAsync(inputLongpooling, cancellationToken);
                return Ok(response.Data);
            default:
                return Ok(
        await functionAppService.GetFunctionByInstance(function, workflow, domain, instance, cancellationToken));
        }

    }
}