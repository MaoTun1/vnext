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
// builder.Configuration.AddDaprSecretStore(builder.Configuration["DAPR_SECRET_STORE_NAME"] ?? "workflow-secretstore", daprClient);

builder.WebHost.ConfigureKestrel(option => option.AddServerHeader = false);

builder.Services.AddApiHostModule();

var app = builder.Build();
app.UseApiHostModule();
await app.RunAsync();