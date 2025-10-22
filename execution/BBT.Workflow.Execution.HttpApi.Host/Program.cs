using BBT.Aether.AspNetCore.Dapr;
using BBT.Aether.AspNetCore.Threads;
using Dapr.Client;
using Dapr.Extensions.Configuration;

ThreadPoolHelper.ConfigureThreadPool();

var builder = WebApplication.CreateBuilder(args);
builder.Configuration.SetBasePath(Directory.GetCurrentDirectory());

// Dapr Optional
if(builder.Configuration.GetValue<bool>("Vault:Enabled", false)){
    var daprClient = new DaprClientBuilder()
        .Build();

    await DaprCheckForSidecarHelper.CheckAsync(daprClient);
    builder.Configuration.AddDaprSecretStore(builder.Configuration["DAPR_SECRET_STORE_NAME"] ?? "vnext-secret", daprClient);
}

builder.WebHost.ConfigureKestrel(option => option.AddServerHeader = false);

builder.Services.AddExecutionApiModule();

var app = builder.Build();

// 🔥 TEST: OpenTelemetry Logging Test
var logger = app.Services.GetRequiredService<ILogger<Program>>();
logger.LogInformation("🔥 vNext Execution başlatıldı - OpenTelemetry aktif!");
logger.LogWarning("⚠️ TEST: Bu WARNING log görünmeli");
logger.LogError("❌ TEST: Bu ERROR log görünmeli");
Console.WriteLine("=== CONSOLE TEST: Program başlatıldı ===");

app.UseExecutionApiModule();

logger.LogInformation("✅ Execution API hazır, port 4202");
Console.WriteLine("=== CONSOLE: API hazır ===");

await app.RunAsync();
