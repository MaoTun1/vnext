using BBT.Aether.Domain.Services;
using BBT.Aether.Threading;
using BBT.Workflow.Data;
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
        app.UseAetherAmbientServiceProvider();
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
        app.UseSecurityHeaders();
        app.UseCurrentUser();
        app.UseStaticFiles();
        app.UseAetherApiVersioning();
        app.UseAetherUnitOfWork();
        app.UseRouting();
        app.UseWorkflowHttpMetrics(); // Track comprehensive HTTP metrics automatically
        app.UseHttpMetrics(); // Track basic Prometheus HTTP metrics
        app.MapMetrics(); // Expose /metrics endpoint for Prometheus scraping
        app.MapControllers();
        app.UseExceptionHandler();
        app.UseDaprScheduledJobHandler();
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