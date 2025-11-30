using BBT.Aether.AspNetCore.Controllers;
using BBT.Aether.AspNetCore.Results;
using BBT.Workflow.Domain.Shared;
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
    IInstanceQueryAppService queryAppService) : AetherControllerBase
{
    [HttpPatch("{domain}/functions")]
    public async Task<IActionResult> GetDomainFunctions(
        [FromRoute] string domain,
        [FromQuery] bool? async = false,
        CancellationToken cancellationToken = default)
    {
        var response = await functionAppService.GetDomainFunctionsAsync(domain, cancellationToken);
        return FromResult(response);
    }

    [HttpGet("{domain}/functions/{function}")]
    public async Task<IActionResult> GetFunctionByKey(
        [FromRoute] string domain,
        [FromRoute] string function,
        [FromQuery] bool? async = false,
        CancellationToken cancellationToken = default)
    {
        var response = await functionAppService.GetFunctionByFunctionKeyAsync(
            function, RuntimeSysSchemaInfo.Functions, domain, cancellationToken);
        return FromResult(response);
    }

    [HttpGet("{domain}/workflows/{workflow}/functions/{function}")]
    public async Task<IActionResult> GetFunctionByKey(
        [FromRoute] string domain,
        [FromRoute] string function,
        [FromRoute] string workflow,
        [FromQuery] bool? async = false,
        CancellationToken cancellationToken = default)
    {
        var response = await functionAppService.GetFunctionByFunctionKeyAsync(
            function, workflow, domain, cancellationToken);
        return FromResult(response);
    }

    [HttpGet("{domain}/workflows/{workflow}/instances/{instance}/functions/{function}")]
    public async Task<IActionResult> GetFunctionWithInstance(
        [FromRoute] string domain,
        [FromRoute] string function,
        [FromRoute] string workflow,
        [FromRoute] string instance,
        [FromQuery] FunctionQueryParemeters parameters,
        [FromHeader(Name = "If-None-Match")] string? ifNoneMatch,
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
                    Extensions = parameters.Extensions
                };
                var response = await queryAppService.GetInstanceStateAsync(inputLongpooling, cancellationToken);
                return response.ToActionResult(HttpContext);

            case Definitions.Functions.FunctionTypeConst.View:
                var inputView = new GetViewInput
                {
                    Domain = domain,
                    Workflow = workflow,
                    Instance = instance,
                    Version = parameters.Version
                };
                var responseView = await queryAppService.GetPlatformSpecificViewAsync(
                    inputView,
                    parameters.Platform,
                    parameters.TransitionKey,
                    cancellationToken);
                if (!responseView.IsSuccess)
                {
                    return responseView.ToActionResult(HttpContext);
                }
                // Return only the content as requested, without Type and Target
                return Ok(responseView.Value!);

            case Definitions.Functions.FunctionTypeConst.Data:
                var inputData = new GetInstanceDataInput
                {
                    Domain = domain,
                    Workflow = workflow,
                    Instance = instance,
                    IfNoneMatch = ifNoneMatch,
                    Extensions = parameters.Extensions
                };
                var responseData = await queryAppService.GetInstanceDataAsync(inputData, cancellationToken);
                // Handle 304 via ToActionResult, but also set ETag header if present
                if (responseData.Result.IsSuccess && !string.IsNullOrEmpty(responseData.Result.Value!.Etag))
                {
                    HttpContext.Response.Headers[HeadersConstants.ETag] = responseData.Result.Value.Etag;
                }
                return FromResult(responseData.Result);

            default:
                var functionResponse = await functionAppService.GetFunctionByInstanceAsync(
                    function, workflow, domain, instance, cancellationToken);
                return FromResult(functionResponse);
        }
    }
}