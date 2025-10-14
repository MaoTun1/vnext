using BBT.Workflow.Telemetry;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using System.Text.RegularExpressions;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Service collection extensions for configuring vNext OpenTelemetry.
/// </summary>
public static class VNextTelemetryServiceCollectionExtensions
{
    /// <summary>
    /// Adds vNext telemetry (OpenTelemetry-based logging, tracing, and metrics).
    /// Replaces Aether framework telemetry with direct OpenTelemetry integration.
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <param name="configuration">The configuration</param>
    /// <param name="environment">The host environment (optional, will be resolved from DI if not provided)</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddVNextTelemetry(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment? environment = null)
    {
        // Register IHttpContextAccessor for log enrichment
        services.AddHttpContextAccessor();

        // Bind telemetry options
        var telemetryOptions = new VNextTelemetryOptions();
        configuration.GetSection(VNextTelemetryOptions.SectionName).Bind(telemetryOptions);
        services.Configure<VNextTelemetryOptions>(
            configuration.GetSection(VNextTelemetryOptions.SectionName));

        // Get environment name from ASPNETCORE_ENVIRONMENT
        var environmentName = environment?.EnvironmentName ?? 
                             System.Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? 
                             "Production";

        // Compile excluded paths regex patterns
        var excludedPathPatterns = telemetryOptions.Logging.ExcludedPaths
            .Select(pattern => new Regex(pattern, RegexOptions.Compiled | RegexOptions.IgnoreCase))
            .ToList();

        // Configure OpenTelemetry
        services.AddOpenTelemetry()
            .ConfigureResource(resource =>
            {
                resource
                    .AddService(
                        serviceName: telemetryOptions.ServiceName,
                        serviceVersion: telemetryOptions.ServiceVersion,
                        serviceInstanceId: System.Environment.MachineName)
                    .AddAttributes(new Dictionary<string, object>
                    {
                        ["deployment.environment"] = environmentName,
                        ["service.namespace"] = "vnext"
                    });
            })
            .WithTracing(tracing =>
            {
                tracing
                    .AddAspNetCoreInstrumentation(options =>
                    {
                        // Filter out excluded paths using regex patterns
                        options.Filter = httpContext =>
                        {
                            var path = httpContext.Request.Path.Value ?? string.Empty;
                            
                            // Check if path matches any excluded pattern
                            foreach (var pattern in excludedPathPatterns)
                            {
                                if (pattern.IsMatch(path))
                                {
                                    return false;
                                }
                            }
                            
                            return true;
                        };

                        // Enrich spans with additional data
                        options.EnrichWithHttpRequest = (activity, httpRequest) =>
                        {
                            // Add configured headers
                            foreach (var header in telemetryOptions.Logging.Enrichers.Headers)
                            {
                                if (httpRequest.Headers.TryGetValue(header, out var value))
                                {
                                    activity.SetTag(header, value.ToString());
                                }
                            }

                            // Add custom attributes from configuration
                            foreach (var attr in telemetryOptions.Logging.Enrichers.CustomAttributes)
                            {
                                activity.SetTag(attr.Key, attr.Value);
                            }
                        };

                        options.EnrichWithHttpResponse = (activity, httpResponse) =>
                        {
                            activity.SetTag("http.response.content_length", httpResponse.ContentLength);
                        };
                    })
                    .AddHttpClientInstrumentation(options =>
                    {
                        // Filter out OTLP exporter, health check requests, and optionally Dapr internal operations
                        options.FilterHttpRequestMessage = request =>
                        {
                            var uri = request.RequestUri?.ToString() ?? string.Empty;
                            // Filter Dapr internal operations if configured
                            if (telemetryOptions.Tracing.EnableExcludedPaths)
                            {
                                foreach (var pattern in excludedPathPatterns)
                                {
                                    if (pattern.IsMatch(uri))
                                    {
                                        return false;
                                    }
                                }
                            }

                            return true;
                        };
                    })
                    .AddSource("BBT.Workflow.*"); // Add custom activity sources

                // Add exporters
                if (environmentName == "Development")
                {
                    tracing.AddConsoleExporter();
                }

                tracing.AddOtlpExporter(options =>
                {
                    var endpoint = telemetryOptions.Otlp.Endpoint.TrimEnd('/') + "/v1/traces"; // SDK not automatically append /v1/traces
                    options.Endpoint = new Uri(endpoint);
                    options.Protocol = telemetryOptions.Otlp.Protocol == "grpc"
                        ? OpenTelemetry.Exporter.OtlpExportProtocol.Grpc
                        : OpenTelemetry.Exporter.OtlpExportProtocol.HttpProtobuf;
                });
            })
            .WithMetrics(metrics =>
            {
                metrics
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddRuntimeInstrumentation()
                    .AddProcessInstrumentation()
                    .AddMeter("BBT.Workflow.*"); // Add custom meters

                // Add exporters
                if (environmentName == "Development")
                {
                    metrics.AddConsoleExporter();
                }

                metrics.AddOtlpExporter(options =>
                {
                    var endpoint = telemetryOptions.Otlp.Endpoint.TrimEnd('/') + "/v1/metrics"; // SDK not automatically append /v1/traces
                    options.Endpoint = new Uri(endpoint);
                    options.Protocol = telemetryOptions.Otlp.Protocol == "grpc"
                        ? OpenTelemetry.Exporter.OtlpExportProtocol.Grpc
                        : OpenTelemetry.Exporter.OtlpExportProtocol.HttpProtobuf;
                });
            });
        
        // Configure logging only if enabled
        if (telemetryOptions.Logging.Enabled)
        {
            services.AddLogging(loggingBuilder =>
            {
                loggingBuilder.AddOpenTelemetry(logging =>
                {
                    // Set resource for logging
                    logging.SetResourceBuilder(ResourceBuilder.CreateDefault()
                        .AddService(
                            serviceName: telemetryOptions.ServiceName,
                            serviceVersion: telemetryOptions.ServiceVersion,
                            serviceInstanceId: System.Environment.MachineName)
                        .AddAttributes(new Dictionary<string, object>
                        {
                            ["deployment.environment"] = environmentName,
                            ["service.namespace"] = "vnext"
                        }));

                    // Add vNext log enricher processor with configuration
                    // Use factory pattern to resolve dependencies from DI container
                    logging.AddProcessor(serviceProvider =>
                    {
                        var httpContextAccessor = serviceProvider.GetService<Microsoft.AspNetCore.Http.IHttpContextAccessor>();
                        return new VNextLogEnricherProcessor(telemetryOptions, excludedPathPatterns, httpContextAccessor);
                    });

                    // Configure logging options
                    logging.IncludeFormattedMessage = telemetryOptions.Logging.IncludeFormattedMessage;
                    logging.IncludeScopes = telemetryOptions.Logging.IncludeScopes;
                    logging.ParseStateValues = telemetryOptions.Logging.ParseStateValues;

                    // Add exporters based on configuration
                    if (telemetryOptions.Logging.EnableConsoleExporter)
                    {
                        logging.AddConsoleExporter();
                    }

                    if (telemetryOptions.Logging.EnableOtlpExporter)
                    {
                        logging.AddOtlpExporter(exporterOptions =>
                        {
                            var endpoint = telemetryOptions.Otlp.Endpoint.TrimEnd('/') + "/v1/logs"; // SDK not automatically append /v1/traces
                            exporterOptions.Endpoint = new Uri(endpoint);
                            exporterOptions.Protocol = telemetryOptions.Otlp.Protocol == "grpc"
                                ? OpenTelemetry.Exporter.OtlpExportProtocol.Grpc
                                : OpenTelemetry.Exporter.OtlpExportProtocol.HttpProtobuf;
                        });
                    }
                });
            });
        }

        return services;
    }
}

