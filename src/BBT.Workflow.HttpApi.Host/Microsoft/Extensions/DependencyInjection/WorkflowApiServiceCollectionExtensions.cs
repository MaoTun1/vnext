using System.IO.Compression;
using System.Net;
using System.Text.Json.Serialization;
using BBT.Aether.AspNetCore.ExceptionHandling;
using BBT.Aether.Domain.Services;
using BBT.Aether.ExceptionHandling;
using BBT.Workflow;
using BBT.Workflow.BackgroundJobs;
using BBT.Workflow.BackgroundJobs.Handlers;
using BBT.Workflow.Caching;
using BBT.Workflow.Data;
using BBT.Workflow.ExceptionHandling;
using BBT.Workflow.Headers;
using BBT.Workflow.Runtime;
using BBT.Workflow.Schemas;
using BBT.Workflow.Scripting;
using BBT.Workflow.Tasks;
using Dapr.Jobs.Extensions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Microsoft.Extensions.DependencyInjection;

public static class WorkflowApiServiceCollectionExtensions
{
    public static IServiceCollection AddApiHostModule(
        this IServiceCollection services)
    {
        var configuration = services.GetConfiguration();

        ConfigureClient(services);
        ConfigureModules(services, configuration);
        ConfigureDbContext(services, configuration);
        ConfigureMapper(services);
        ConfigureTelemetry(services, configuration);
        ConfigureDistributedCache(services);
        ConfigureRedis(services);
        ConfigureHealthChecks(services);
        ConfigureRoute(services);
        ConfigureExceptionHandling(services);
        ConfigureJobHandlers(services);
        ConfigureHost(services);
        return services;
    }

    private static void ConfigureClient(IServiceCollection services)
    {
        services.AddDaprClient();
        services.AddDaprJobsClient();
        // TODO: A strategy should be determined on a task basis. 
        // HTTP client with connection pooling 
        services.AddHttpClient<HttpTaskExecutor>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(30);
        })
        .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
        {
            MaxConnectionsPerServer = 10,
            UseCookies = false
        });
    }

    private static void ConfigureModules(IServiceCollection services, IConfiguration configuration)
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
        
        services.AddSingleton<IDataSeedService, WorkflowDataSeedService>();
    }

    private static void ConfigureMapper(IServiceCollection services)
    {
        services.AddAetherAutoMapperMapper(
        [
            typeof(Program), // ApiHost
            typeof(WorkflowDomainModuleServiceCollectionExtensions), // Domain
            typeof(WorkflowApplicationModuleServiceCollectionExtensions) // Application
        ]);
    }

    private static void ConfigureTelemetry(IServiceCollection services, IConfiguration configuration)
    {
        services.AddFrameworkTelemetry(configuration);
    }

    private static void ConfigureDistributedCache(IServiceCollection services)
    {
        // Option 1: Use .NET Core Distributed Cache
        services.AddNetCoreDistributedCache(sc =>
        {
            // Configure your preferred distributed cache implementation
            sc.AddDistributedMemoryCache(); // Default in-memory
        });
    }

    private static void ConfigureRedis(IServiceCollection services)
    {
        // Option 2: Use Dapr State Store Cache
        // services.AddDaprStateStoreCache();

        // Add Redis Configuration
        services.AddRedis();
    }

    private static void ConfigureHealthChecks(IServiceCollection services)
    {
        services.AddAppHealthChecks();
    }

    private static void ConfigureRoute(IServiceCollection services)
    {
        // Add services to the container
        services.AddEndpointsApiExplorer();

        // Add API Versioning using the custom configuration
        services.AddAetherApiVersioning(apiTitle: "vNext API");

        // Add services to the container.
        services.AddControllers()
            .AddJsonOptions(options =>
            {
                options.JsonSerializerOptions.WriteIndented = false;
                options.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
                // options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
            });
    }

    private static void ConfigureExceptionHandling(IServiceCollection services)
    {
        services.Configure<AetherExceptionHttpStatusCodeOptions>(opt =>
        {
            opt.Map(WorkflowErrorCodes.NotFoundDomain, HttpStatusCode.BadRequest);
            opt.Map(WorkflowErrorCodes.ConflictWorkflow, HttpStatusCode.Conflict);
            opt.Map(WorkflowErrorCodes.RuntimeSchemaInvalidState, HttpStatusCode.BadRequest);
        });
        services.Remove<IExceptionToErrorInfoConverter>();
        services.AddTransient<IExceptionToErrorInfoConverter, WorkflowExceptionToErrorInfoConverter>();
    }
    
    private static void ConfigureJobHandlers(IServiceCollection services)
    {
        // Job Handlers
        services.AddScoped<IJobHandler, FlowTimeoutJobHandler>();
        services.AddScoped<IJobHandler, AutoTransitionJobHandler>();
        services.AddScoped<IJobHandler, TransitionTimerJobHandler>();
    }

    private static void ConfigureHost(IServiceCollection services)
    {
        services.AddScoped<WorkflowRuntimeMiddleware>();
        services.AddScoped<ResponseHeaderFilter>();
        services.AddHostedService<CacheInitializationHostedService>();
        services.AddScoped<IHeaderService, HttpContextHeaderService>();
        services.AddHostedService<ScriptingInitializationService>();
    }
}