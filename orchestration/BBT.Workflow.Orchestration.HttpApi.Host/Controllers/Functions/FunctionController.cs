using BBT.Workflow.Definitions;
using BBT.Workflow.Domain.Shared;
using BBT.Workflow.Functions;
using BBT.Workflow.HttpApi.Shared;
using BBT.Workflow.Instances;
using BBT.Workflow.Instances.DTOs;
using BBT.Workflow.Runtime;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace BBT.Workflow.Controllers.Instances;

/// <summary>
/// Controller for handling workflow function operations
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}")]
[ServiceFilter(typeof(ResponseHeaderFilter))]
public sealed class FunctionController(IFunctionAppService functionAppService,
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
        [FromQuery] FunctionListQueryParameters parameters,
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
        [FromQuery] FunctionListQueryParameters parameters,
        [FromQuery] bool? async = false,
        CancellationToken cancellationToken = default)
    {
        var getInstanceListInput = new GetInstanceListInput
        {
            Domain = domain,
            Page = parameters.Page,
            PageSize = parameters.PageSize,
            PageUrl = $"{domain}/workflows/{workflow}/functions/{function}",
            Sort = parameters.Sort,
            Workflow = workflow,
            Filter = function.ToLowerInvariant() == Definitions.Functions.FunctionTypeConst.Data ? parameters.Filter : Array.Empty<string>()
        };
        
        var instanceListResult = await queryAppService.GetInstanceListAsync(getInstanceListInput, cancellationToken);
        
        if (!instanceListResult.IsSuccess)
        {
            return instanceListResult.ToActionResult();
        }

        // Process based on function type
        return function.ToLowerInvariant() switch
        {
            Definitions.Functions.FunctionTypeConst.Longpooling =>
                await ProcessLongpoolingFunctionList(domain, workflow, instanceListResult.Value!, cancellationToken),
            Definitions.Functions.FunctionTypeConst.View =>
                await ProcessViewFunctionList(domain, workflow, instanceListResult.Value!, cancellationToken),
            Definitions.Functions.FunctionTypeConst.Data =>
                await ProcessDataFunctionList(domain, workflow, instanceListResult.Value!, cancellationToken),
            _ =>
                await ProcessCustomFunctionList(function, workflow, domain, instanceListResult.Value!, cancellationToken)
        };
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
        return function.ToLowerInvariant() switch
        {
            Definitions.Functions.FunctionTypeConst.Longpooling =>
                await ProcessLongpoolingFunction(domain, workflow, instance, parameters.Version, parameters.Extensions, cancellationToken),
            Definitions.Functions.FunctionTypeConst.View =>
                await ProcessViewFunction(domain, workflow, instance, parameters.Version, parameters.Platform, parameters.TransitionKey, cancellationToken),
            Definitions.Functions.FunctionTypeConst.Data =>
                await ProcessDataFunction(domain, workflow, instance, ifNoneMatch, parameters.Extensions, cancellationToken),
            _ =>
                Ok(await functionAppService.GetFunctionByInstance(function, workflow, domain, instance, cancellationToken))
        };
    }

    #region Private Helper Methods

    private async Task<IActionResult> ProcessLongpoolingFunction(
        string domain,
        string workflow,
        string instance,
        string? version,
        string[]? extensions,
        CancellationToken cancellationToken)
    {
        var input = new GetInstanceStateInput
        {
            Domain = domain,
            Workflow = workflow,
            Instance = instance,
            Version = version,
            Extensions = extensions
        };
        var response = await queryAppService.GetInstanceStateAsync(input, cancellationToken);
        return response.ToActionResult();
    }

    private async Task<IActionResult> ProcessLongpoolingFunctionList(
        string domain,
        string workflow,
        PaginationResult<GetInstanceOutput> instanceListResult,
        CancellationToken cancellationToken)
    {
        var outputs = new PaginationResult<GetInstanceStateOutput>
        {
            Pagination = instanceListResult.Pagination,
            Data = new List<GetInstanceStateOutput>()
        };

        foreach (var instance in instanceListResult.Data)
        {
            var result = await ProcessLongpoolingFunction(
                domain,
                workflow,
                instance.Key!.ToString(),
                instance.FlowVersion,
                null,
                cancellationToken);

            if (result is not OkObjectResult okResult)
            {
                return result;
            }

            outputs.Data.Add((GetInstanceStateOutput)okResult.Value!);
        }

        return Ok(outputs);
    }

    private async Task<IActionResult> ProcessViewFunction(
        string domain,
        string workflow,
        string instance,
        string? version,
        string? platform,
        string? transitionKey,
        CancellationToken cancellationToken)
    {
        var input = new GetViewInput
        {
            Domain = domain,
            Workflow = workflow,
            Instance = instance,
            Version = version
        };

        var response = await queryAppService.GetPlatformSpecificViewAsync(
            input,
            platform ?? string.Empty,
            transitionKey ?? string.Empty,
            cancellationToken);

        if (!response.IsSuccess)
        {
            return response.ToActionResult();
        }

        return Ok(response.Value!);
    }

    private async Task<IActionResult> ProcessViewFunctionList(
        string domain,
        string workflow,
        PaginationResult<GetInstanceOutput> instanceListResult,
        CancellationToken cancellationToken)
    {
        var outputs = new PaginationResult<GetViewOutput>
        {
            Pagination = instanceListResult.Pagination,
            Data = new List<GetViewOutput>()
        };

        foreach (var instance in instanceListResult.Data)
        {
            var result = await ProcessViewFunction(
                domain,
                workflow,
                instance.Key!.ToString(),
                instance.FlowVersion,
                string.Empty,
                string.Empty,
                cancellationToken);

            if (result is not OkObjectResult okResult)
            {
                return result;
            }

            outputs.Data.Add((GetViewOutput)okResult.Value!);
        }

        return Ok(outputs);
    }

    private async Task<IActionResult> ProcessDataFunction(
        string domain,
        string workflow,
        string instance,
        string? ifNoneMatch,
        string[]? extensions,
        CancellationToken cancellationToken)
    {
        var input = new GetInstanceDataInput
        {
            Domain = domain,
            Workflow = workflow,
            Instance = instance,
            IfNoneMatch = ifNoneMatch,
            Extensions = extensions
        };

        var response = await queryAppService.GetInstanceDataAsync(input, cancellationToken);

        if (response.Result.IsSuccess && !string.IsNullOrEmpty(response.Result.Value!.Etag))
        {
            HttpContext.Response.Headers[HeadersConstants.ETag] = response.Result.Value.Etag;
        }

        return response.ToActionResult();
    }

    private async Task<IActionResult> ProcessDataFunctionList(
        string domain,
        string workflow,
        PaginationResult<GetInstanceOutput> instanceListResult,
        CancellationToken cancellationToken)
    {
        var outputs = new PaginationResult<GetInstanceDataOutput>
        {
            Pagination = instanceListResult.Pagination,
            Data = new List<GetInstanceDataOutput>()
        };

        foreach (var instance in instanceListResult.Data)
        {
            var input = new GetInstanceDataInput
            {
                Domain = domain,
                Workflow = workflow,
                Instance = instance.Key!.ToString(),
            };

            var response = await queryAppService.GetInstanceDataAsync(input, cancellationToken);
            outputs.Data.Add(response.Result.Value!);
        }

        return Ok(outputs);
    }

    private async Task<IActionResult> ProcessCustomFunctionList(
        string function,
        string workflow,
        string domain,
        PaginationResult<GetInstanceOutput> instanceListResult,
        CancellationToken cancellationToken)
    {
        var outputs = new PaginationResult<Dictionary<string, dynamic?>>
        {
            Pagination = instanceListResult.Pagination,
            Data = new List<Dictionary<string, dynamic?>>()
        };

        foreach (var instance in instanceListResult.Data)
        {
            outputs.Data.Add(
                await functionAppService.GetFunctionByInstance(function, workflow, domain, instance.Key!.ToString(), cancellationToken)
            );
        }

        return Ok(outputs);
    }

    #endregion
}