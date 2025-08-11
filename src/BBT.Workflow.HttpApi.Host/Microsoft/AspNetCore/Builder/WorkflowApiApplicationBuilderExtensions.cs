using System.Globalization;
using System.Text.Json;
using BBT.Aether.Domain.Services;
using BBT.Aether.Threading;
using BBT.Workflow.BackgroundJobs;
using BBT.Workflow.Data;
using BBT.Workflow.Runtime;
using Dapr.Jobs.Extensions;
using Microsoft.AspNetCore.Localization;
using Microsoft.EntityFrameworkCore;

namespace Microsoft.AspNetCore.Builder;

public static class WorkflowApiApplicationBuilderExtensions
{
    public static WebApplication UseApiHostModule(this WebApplication app)
    {
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

        // var supportedCultures = new[] { "en-US", "tr-TR" };
        // var localizationOptions = new RequestLocalizationOptions()
        // {
        //     DefaultRequestCulture = new RequestCulture("en-US"),
        //     SupportedCultures = supportedCultures.Select(c => new CultureInfo(c)).ToList(),
        //     SupportedUICultures = supportedCultures.Select(c => new CultureInfo(c)).ToList()
        // };
        // app.UseRequestLocalization(localizationOptions);
        app.UseAetherApiVersioning();
        app.UseRouting();
        app.MapControllers();
        app.UseExceptionHandler();
        app.MapAppHealthChecks();

        app.MapDaprScheduledJobHandler(async (string jobName, ReadOnlyMemory<byte> jobPayload, JobDispatcher dispatcher,
            CancellationToken cancellationToken) =>
        {
            await dispatcher.DispatchAsync(jobName, jobPayload, cancellationToken);
        });
        
        SeedTestData(app.Services);

        return app;
    }

    private static void SeedTestData(IServiceProvider serviceProvider)
    {
        AsyncHelper.RunSync(async () =>
        {
            using var scope = serviceProvider.CreateScope();

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

    public static IApplicationBuilder UseRuntime(this IApplicationBuilder app)
    {
        return app
            .UseMiddleware<WorkflowRuntimeMiddleware>();
    }
}