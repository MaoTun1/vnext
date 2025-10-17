using BBT.Aether.Domain.Services;
using BBT.Aether.Threading;
using BBT.Workflow.Data;
using BBT.Workflow.HttpApi.Shared;
using BBT.Workflow.Middlewares;
using BBT.Workflow.Runtime;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Prometheus;

namespace Microsoft.AspNetCore.Builder;

/// <summary>
/// Base application builder extensions shared across all Workflow APIs
/// </summary>
public static class WorkflowApiBaseApplicationBuilderExtensions
{
    /// <summary>
    /// Configures the application pipeline with base Workflow API middleware
    /// </summary>
    /// <param name="app">The web application</param>
    /// <returns>The web application for chaining</returns>
    public static WebApplication UseWorkflowApiBase(this WebApplication app)
    {
        // Configure ProblemDetailsMapper with the registered factory from DI
        var problemDetailsFactory = app.Services.GetRequiredService<ProblemDetailsFactory>();
        ProblemDetailsMapper.Configure(problemDetailsFactory);
        
        if (app.Environment.IsDevelopment())
        {
            app.UseDeveloperExceptionPage();
        }
        else
        {
            app.UseHsts();
        }

        app.UseAppResponseCompression();
        app.UseCloudEvents();
        app.MapSubscribeHandler();
        app.UseHttpsRedirection();
        app.UseRuntime();
        app.UseCorrelationId();
        app.UseTraceContext(); // Add OpenTelemetry trace context to response headers
        app.UseSecurityHeaders();
        app.UseCurrentUser();
        app.UseStaticFiles();
        app.UseAetherApiVersioning();
        app.UseRouting();
        app.UseWorkflowHttpMetrics(); // Track comprehensive HTTP metrics automatically
        app.UseHttpMetrics(); // Track basic Prometheus HTTP metrics
        app.MapMetrics(); // Expose /metrics endpoint for Prometheus scraping
        app.MapControllers();
        app.UseExceptionHandler();

        return app;
    }

    /// <summary>
    /// Adds Workflow runtime middleware to the pipeline
    /// </summary>
    /// <param name="app">The application builder</param>
    /// <returns>The application builder for chaining</returns>
    public static IApplicationBuilder UseRuntime(this IApplicationBuilder app)
    {
        return app.UseMiddleware<WorkflowRuntimeMiddleware>();
    }

    /// <summary>
    /// Seeds test data if configured
    /// </summary>
    /// <param name="services">The service provider</param>
    public static void SeedTestData(IServiceProvider services)
    {
        AsyncHelper.RunSync(async () =>
        {
            using var scope = services.CreateScope();

            var dbContext = scope.ServiceProvider.GetRequiredService<WorkflowDbContext>();
            if (dbContext.Database.IsRelational())
            {
                await dbContext.Database.EnsureCreatedAsync();
            }

            await scope.ServiceProvider
                .GetRequiredService<IDataSeedService>()
                .SeedAsync(new SeedContext());
        });
    }
} 