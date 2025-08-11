using BBT.Aether.AspNetCore.Dapr;
using BBT.Aether.AspNetCore.Threads;
using Dapr.Client;
using Dapr.Extensions.Configuration;

ThreadPoolHelper.ConfigureThreadPool();

var builder = WebApplication.CreateBuilder(args);
builder.Configuration.SetBasePath(Directory.GetCurrentDirectory());
// // Dapr Optional
// var daprClient = new DaprClientBuilder()
//     .Build();
//
// await DaprCheckForSidecarHelper.CheckAsync(daprClient);
// builder.Configuration.AddDaprSecretStore(builder.Configuration["DAPR_SECRET_STORE_NAME"] ?? "vnext-secret", daprClient);

builder.WebHost.ConfigureKestrel(option => option.AddServerHeader = false);

builder.Services.AddOrchestrationApiModule();

var app = builder.Build();
app.UseOrchestrationApiModule();
await app.RunAsync();