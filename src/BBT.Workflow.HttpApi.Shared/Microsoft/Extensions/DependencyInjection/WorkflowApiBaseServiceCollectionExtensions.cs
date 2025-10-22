using System.Net;
using System.Text.Json.Serialization;
using BBT.Aether.AspNetCore.ExceptionHandling;
using BBT.Aether.Domain.Services;
using BBT.Aether.ExceptionHandling;
using BBT.Aether.Http;
using BBT.Workflow;
using BBT.Workflow.Data;
using BBT.Workflow.Events.Distributed;
using BBT.Workflow.ExceptionHandling;
using BBT.Workflow.Headers;
using BBT.Workflow.HttpApi.Shared;
using BBT.Workflow.Monitoring;
using BBT.Workflow.Runtime;
using BBT.Workflow.Schemas;
using BBT.Workflow.Tasks;
using Dapr.Jobs.Extensions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Base service collection extensions shared across all Workflow APIs
/// </summary>
public static class WorkflowApiBaseServiceCollectionExtensions
{
    /// <summary>
    /// Adds base Workflow API services common to all host applications
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddWorkflowApiBase(this IServiceCollection services)
    {
        var configuration = services.GetConfiguration();
        
        ConfigureBaseModules(services, configuration);
        ConfigureDbContext(services, configuration);
        ConfigureMapper(services);
        ConfigureTelemetry(services, configuration);
        ConfigureRedis(services);
        ConfigureDistributedCache(services, configuration);
        ConfigureDistributedLock(services, configuration);
        ConfigureRoute(services);
        ConfigureExceptionHandling(services);
        ConfigureBaseHost(services);
        
        return services;
    }

    /// <summary>
    /// Adds Dapr client services
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddWorkflowDaprClients(this IServiceCollection services)
    {
        services.AddDaprClient();
        services.AddDaprJobsClient();
        return services;
    }

    /// <summary>
    /// Adds HTTP client with default configuration for task execution
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddWorkflowHttpClient(this IServiceCollection services)
    {
        // Default HTTP client with SSL validation enabled
        services.AddHttpClient(HttpTaskExecutor.DefaultHttpClientName, client =>
        {
            // Default timeout - will be overridden per request
            client.Timeout = TimeSpan.FromSeconds(30);
        })
        .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
        {
            MaxConnectionsPerServer = 10,
            UseCookies = false
        });

        // HTTP client with SSL validation disabled
        services.AddHttpClient(HttpTaskExecutor.NoSslValidationHttpClientName, client =>
        {
            // Default timeout - will be overridden per request
            client.Timeout = TimeSpan.FromSeconds(30);
        })
        .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
        {
            MaxConnectionsPerServer = 10,
            UseCookies = false,
            ServerCertificateCustomValidationCallback = (message, cert, chain, sslPolicyErrors) => true
        });
        
        return services;
    }

    private static void ConfigureBaseModules(IServiceCollection services, IConfiguration configuration)
    {
        services.AddAetherCore(options =>
        {
            options.Environment ??= Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development";
            options.ApplicationName ??= configuration.GetValue<string?>("ApplicationName") ?? "vNext";
        });
        services.AddInfrastructureModule();
        services.AddAetherAspNetCore();
    }

    private static void ConfigureDbContext(IServiceCollection services, IConfiguration configuration)
    {
        services.AddAetherDbContext<WorkflowDbContext>(options =>
        {
            options.UseNpgsql(configuration.GetConnectionString("Default"), npgsqlOptions =>
            {
                npgsqlOptions.MigrationsHistoryTable("__Workflow_Migrations");
                // Enable retrying failed database operations
                npgsqlOptions.EnableRetryOnFailure(
                    maxRetryCount: 3,
                    maxRetryDelay: TimeSpan.FromSeconds(30),
                    errorCodesToAdd: null);
            }).ConfigureWarnings(w => w.Ignore(RelationalEventId.PendingModelChangesWarning));

            options.ReplaceService<IModelCacheKeyFactory, DynamicSchemaModelCacheKeyFactory>();
            options.ReplaceService<IMigrationsAssembly, DbSchemaAwareMigrationAssembly>();
        });
        
        services.Configure<DaprEventPublisherOptions>(
            configuration.GetSection(DaprEventPublisherOptions.SectionName));
        
        services.AddDistributedDomainEventPublisher<DaprDistributedDomainEventPublisher>();
        
        services.AddSingleton<IDataSeedService, WorkflowDataSeedService>();
    }

    private static void ConfigureMapper(IServiceCollection services)
    {
        services.AddAetherAutoMapperMapper(
        [
            typeof(WorkflowApiBaseServiceCollectionExtensions), // HttpApi.Shared
            typeof(WorkflowDomainModuleServiceCollectionExtensions), // Domain
            typeof(WorkflowApplicationModuleServiceCollectionExtensions) // Application
        ]);
    }

    private static void ConfigureTelemetry(IServiceCollection services, IConfiguration configuration)
    {
        // Use vNext custom OpenTelemetry configuration instead of Aether framework telemetry
        //services.AddFrameworkTelemetry(configuration);
         services.AddVNextTelemetry(configuration);
    }

    private static void ConfigureDistributedCache(IServiceCollection services, IConfiguration configuration)
    {
        services.AddDaprDistributedCache(configuration["DAPR_STATE_STORE_NAME"]!);
    }
    
    private static void ConfigureDistributedLock(IServiceCollection services, IConfiguration configuration)
    {
        services.AddDaprDistributedLock(configuration["DAPR_LOCK_STORE_NAME"]!);
    }

    private static void ConfigureRedis(IServiceCollection services)
    {
        services.AddRedis();
    }

    private static void ConfigureRoute(IServiceCollection services)
    {
        services.AddEndpointsApiExplorer();
        services.AddAetherApiVersioning(apiTitle: "vNext API");

        services.AddControllers()
            .AddJsonOptions(options =>
            {
                options.JsonSerializerOptions.WriteIndented = false;
                options.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
            });
    }

    private static void ConfigureExceptionHandling(IServiceCollection services)
    {
        // Configure Aether's error code to HTTP status code mapping
        // This is the central place for all error code mappings in the application
        // Both exception handling and Result pattern use this configuration
        services.Configure<AetherExceptionHttpStatusCodeOptions>(opt =>
        {
            // General errors
            opt.Map(WorkflowErrorCodes.Locked, HttpStatusCode.Conflict);
            opt.Map(WorkflowErrorCodes.ValidationErrors, HttpStatusCode.BadRequest);
            
            // Instance errors
            opt.Map(WorkflowErrorCodes.NotFoundDomain, HttpStatusCode.BadRequest);
            opt.Map(WorkflowErrorCodes.ConflictWorkflow, HttpStatusCode.Conflict);
            opt.Map(WorkflowErrorCodes.RuntimeSchemaInvalidState, HttpStatusCode.BadRequest);
            opt.Map(WorkflowErrorCodes.TransitionLocked, HttpStatusCode.Conflict);
            opt.Map(WorkflowErrorCodes.AutoTransitionConditionNotMet, HttpStatusCode.BadRequest);
            opt.Map(WorkflowErrorCodes.UnauthorizedTransition, HttpStatusCode.Forbidden);
            opt.Map(WorkflowErrorCodes.InvalidState, HttpStatusCode.BadRequest);
            opt.Map(WorkflowErrorCodes.NotFoundTransition, HttpStatusCode.NotFound);
            opt.Map(WorkflowErrorCodes.NotFoundInitialState, HttpStatusCode.NotFound);
            opt.Map(WorkflowErrorCodes.NotFoundWorkflow, HttpStatusCode.NotFound);
            
            // Execution errors
            opt.Map(WorkflowErrorCodes.ExecutionStepFailed, HttpStatusCode.BadRequest);
            
            // Task errors
            opt.Map(WorkflowErrorCodes.TaskContextCreation, HttpStatusCode.InternalServerError);
            opt.Map(WorkflowErrorCodes.TaskExecution, HttpStatusCode.InternalServerError);
        });
        
        services.AddSingleton<ProblemDetailsFactory>();
        
        // Replace Aether's default exception converter with our custom one
        services.Remove<IExceptionToErrorInfoConverter>();
        services.AddTransient<IExceptionToErrorInfoConverter, WorkflowExceptionToErrorInfoConverter>();
        
        // Replace Aether's default exception handler with our ProblemDetails handler
        services.Remove<IExceptionHandler>();
        services.AddExceptionHandler<ProblemDetailsExceptionHandler>();
    }

    private static void ConfigureBaseHost(IServiceCollection services)
    {
        services.AddScoped<WorkflowRuntimeMiddleware>();
        services.AddScoped<ResponseHeaderFilter>();
        services.AddScoped<IHeaderService, HttpContextHeaderService>();
    }
} 