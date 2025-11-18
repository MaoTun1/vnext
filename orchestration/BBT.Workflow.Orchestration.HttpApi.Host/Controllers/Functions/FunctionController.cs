using System.ComponentModel.DataAnnotations;
using System.Linq.Dynamic.Core;
using BBT.Workflow.Definitions;
using BBT.Workflow.Domain.Shared;
using BBT.Workflow.Functions;
using BBT.Workflow.HttpApi.Shared;
using BBT.Workflow.Instances;
using BBT.Workflow.Instances.DTOs;
using BBT.Workflow.Runtime;
using Humanizer;
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
        GetInstanceListInput getInstanceListInput = new GetInstanceListInput()
        {
            Domain = domain,
            Page = parameters.Page,
            PageSize = parameters.PageSize,
            PageUrl = $"{domain}/workflows/{workflow}/functions/{function}",
            Sort = parameters.Sort,
            Workflow = workflow,
            Filter = function.ToLowerInvariant() == Definitions.Functions.FunctionTypeConst.Data ? parameters.filter : []
        };
        var instanceListResult = await queryAppService.GetInstanceListAsync(getInstanceListInput, cancellationToken);
        switch (function.ToLowerInvariant())
        {
            case Definitions.Functions.FunctionTypeConst.Longpooling:
                PaginationResult<GetInstanceStateOutput> longOutputs = new PaginationResult<GetInstanceStateOutput>();
                longOutputs.Pagination = instanceListResult.Value!.Pagination;
                longOutputs.Data = new List<GetInstanceStateOutput>();
                foreach (var instance in instanceListResult.Value.Data)
                {
                    var inputLongpooling = new GetInstanceStateInput
                    {
                        Domain = domain,
                        Workflow = workflow,
                        Instance = instance.Key!.ToString(),
                        Version = instance.FlowVersion
                    };
                    var responseLongp = await queryAppService.GetInstanceStateAsync(inputLongpooling, cancellationToken);
                    if (!responseLongp.IsSuccess)
                    {
                        return responseLongp.ToActionResult();
                    }
                    longOutputs.Data.Add(responseLongp.Value!);
                }
                return Ok(longOutputs);
            case Definitions.Functions.FunctionTypeConst.View:
                PaginationResult<GetViewOutput> viewOutputs = new PaginationResult<GetViewOutput>();
                viewOutputs.Pagination = instanceListResult.Value!.Pagination;
                viewOutputs.Data = new List<GetViewOutput>();
                foreach (var instance in instanceListResult.Value.Data)
                {
                    var inputView = new GetViewInput
                    {
                        Domain = domain,
                        Workflow = workflow,
                        Instance = instance.Key!.ToString(),
                        Version = instance.FlowVersion
                    };
                    var responseView = await queryAppService.GetPlatformSpecificViewAsync(
                        inputView,
                        string.Empty,
                        string.Empty,
                        cancellationToken);
                    if (!responseView.IsSuccess)
                    {
                        return responseView.ToActionResult();
                    }
                    viewOutputs.Data.Add(responseView.Value!);
                }


                // Return only the content as requested, without Type and Target
                return Ok(viewOutputs);
            case Definitions.Functions.FunctionTypeConst.Data:
                PaginationResult<GetInstanceDataOutput> dataOutputs = new PaginationResult<GetInstanceDataOutput>();
                dataOutputs.Pagination = instanceListResult.Value!.Pagination;
                dataOutputs.Data = new List<GetInstanceDataOutput>();
                foreach (var instance in instanceListResult.Value.Data)
                {
                    var inputData = new GetInstanceDataInput
                    {
                        Domain = domain,
                        Workflow = workflow,
                        Instance = instance.Key!.ToString(),
                    };
                    var responseData = await queryAppService.GetInstanceDataAsync(inputData, cancellationToken);

                    dataOutputs.Data.Add(responseData.Result.Value!);
                }
               return Ok(dataOutputs);

            default:
             PaginationResult<Dictionary<string,dynamic?>> funcOutputs = new PaginationResult<Dictionary<string,dynamic?>>();
                funcOutputs.Pagination = instanceListResult.Value!.Pagination;
                funcOutputs.Data = new List<Dictionary<string, dynamic?>>();
                foreach(var instance in instanceListResult.Value.Data)
                {
                    funcOutputs.Data.Add(await functionAppService.GetFunctionByInstance(function, workflow, domain, instance.Key!.ToString(), cancellationToken));
                }
               return Ok(funcOutputs);
        }
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
                return response.ToActionResult();
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
                    return responseView.ToActionResult();
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

                return responseData.ToActionResult();
            default:
                return Ok(
                    await functionAppService.GetFunctionByInstance(function, workflow, domain, instance, cancellationToken)
                    );
        }
    }
}