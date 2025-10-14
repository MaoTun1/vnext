using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using BBT.Workflow.Functions;
using BBT.Workflow.Instances;
using BBT.Workflow.Instances.DTOs;
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
 [FromQuery] FunctionQueryParemeters parameters,
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
                    Version = parameters.Version,
                    Extension = parameters.Extension
                };
                var response = await queryAppService.GetInstanceStateAsync(inputLongpooling, cancellationToken);
                return Ok(response.Data);
            case Definitions.Functions.FunctionTypeConst.View:
                var inputView = new GetViewInput
                {
                    Domain = domain,
                    Workflow = workflow,
                    Instance = instance,
                    Version = parameters.Version
                };
                var responseView = await queryAppService.GetPlatformSpecificViewAsync(inputView, parameters.Platform, cancellationToken);

                // Return only the content as requested, without Type and Target
                return Ok(responseView.Data.Content);
            default:
                return Ok(
        await functionAppService.GetFunctionByInstance(function, workflow, domain, instance, cancellationToken));
        }

    }
}