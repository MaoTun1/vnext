# Transition Pipeline Architecture

Bu dokümantasyon, BBT.Workflow projesinde gerçekleştirilen yeni **Transition Pipeline Architecture** refactoring'ini açıklamaktadır.

## Genel Bakış

Yeni mimari, transition yürütmeyi **okunabilir, test edilebilir, genişletilebilir** hale getirmek için tasarlanmıştır. `StateMachineExecutor` üzerindeki karmaşıklığı düşürür ve Auto/Schedule gibi **re-entry** senaryolarını ilk sınıf vatandaş olarak tanımlar.

## Temel İlkeler

- **SRP & Ayrık Sorumluluk:** Sync/Async mod seçimi ≠ Trigger tipi yönetimi ≠ Lifecycle adımlarının yürütülmesi
- **Deterministik Lifecycle:** Belirli ve dokümante edilmiş sıra
- **Context Rehydrate:** Auto/Schedule re-entry'de Context taşınmaz; yeni DI scope'ta yeniden kurulur
- **Service Locator Yok:** Servisler Context içinde değil, adımlara/handler'lara DI ile verilir
- **Idempotency & Lock:** Instance bazlı kilit ve idempotency ana akış özelliğidir

## Mimari Bileşenleri

### 1. Çekirdek Arayüzler

#### TriggerType Enum
```csharp
public enum TriggerType
{
    Manual = 0,     // Kullanıcı tarafından tetiklenen
    Automatic = 1,  // Sistem tarafından otomatik tetiklenen
    Scheduled = 2,  // Zamanlayıcı ile tetiklenen
    Event = 3       // Harici olay ile tetiklenen
}
```

#### ITransitionStep
```csharp
public interface ITransitionStep
{
    int Order { get; }
    Task ExecuteAsync(TransitionExecutionContext context, CancellationToken cancellationToken);
}
```

#### ITransitionHandler
```csharp
public interface ITransitionHandler
{
    bool CanHandle(TriggerType triggerType);
    Task PreHandleAsync(TransitionExecutionContext context, CancellationToken cancellationToken);
    Task PostHandleAsync(TransitionExecutionContext context, CancellationToken cancellationToken);
}
```

### 2. Pipeline Adımları (Lifecycle Order)

1. **CreateTransitionRecordStep** (Order: 10) - Transition kaydı oluşturur
2. **RunOnExecuteTasksStep** (Order: 20) - Transition'ın OnExecute task'larını çalıştırır
3. **RunOnExitTasksStep** (Order: 30) - Mevcut state'in OnExit task'larını çalıştırır
4. **ChangeStateStep** (Order: 40) - State değişikliğini gerçekleştirir
5. **RunOnEntryTasksStep** (Order: 50) - Hedef state'in OnEntry task'larını çalıştırır
6. **HandleSubFlowOrFinishStep** (Order: 60) - SubFlow veya workflow bitişini yönetir
7. **ScheduleTransitionsStep** (Order: 70) - Zamanlı transition'ları planlar
8. **RunAutomaticTransitionsStep** (Order: 80) - Otomatik transition'ları değerlendirir ve tetikler
9. **FinalizeTransitionStep** (Order: 90) - Transition'ı sonlandırır ve temizlik yapar

### 3. Trigger Handler'ları

#### ManualTransitionHandler
- Policy/HMAC/Auth/Schema validation
- Kullanıcı yetkilendirmesi
- Audit logging

#### AutomaticTransitionHandler
- Condition re-validation
- Chain depth kontrolü
- Execution metrics

#### ScheduledTransitionHandler
- Timing validation
- Schedule constraints
- Recurring schedule management

#### EventTransitionHandler
- Event source validation
- Event correlation
- Payload validation

### 4. Re-entry System

#### ReentryCommand
```csharp
public sealed record ReentryCommand(
    Guid InstanceId,
    string Domain,
    string WorkflowKey,
    string TransitionKey,
    TriggerType TriggerType,
    string? Actor = null,
    string? ExecutionChainId = null,
    int ChainDepth = 0,
    bool PreferInline = false,
    IReadOnlyDictionary<string,string>? Headers = null);
```

#### IReentryDispatcher
- `DispatchAutoAsync`: Otomatik transition'ları yönetir (inline veya background job)
- `DispatchScheduledAsync`: Zamanlı transition'ları background job olarak kuyruğa alır

### 5. TransitionExecutionContext

Minimal, servis-içermeyen context yapısı:

```csharp
public sealed class TransitionExecutionContext
{
    // Identity (immutable)
    public string Domain { get; init; }
    public Guid InstanceId { get; init; }
    public string WorkflowKey { get; init; }
    public string TransitionKey { get; init; }
    public TriggerType Trigger { get; init; }
    
    // Definitions (rehydrated)
    public Definitions.Workflow Workflow { get; init; }
    public WorkflowState Current { get; set; }
    public WorkflowTransition Transition { get; init; }
    
    // Instance snapshot
    public InstanceAggregate Instance { get; set; }
    
    // Execution flags
    public bool SkipImmediateExecution { get; set; }
    public bool IsReentry { get; init; }
    
    // Temporary storage
    public IDictionary<string, object?> Items { get; }
    
    // Helper method
    public ScriptContext GetOrBuildScriptContext(Func<ScriptContext> factory);
}
```

## Kullanım

### 1. DI Kayıtları

```csharp
services.AddTransitionPipeline();

// Veya özel konfigürasyon ile:
services.AddTransitionPipeline(options =>
{
    options.MaxAutoHops = 15;
    options.AllowInlineAuto = false;
});
```

### 2. Transition Yürütme

```csharp
var input = new WorkflowExecutionInput
{
    Domain = "example",
    InstanceId = instanceId,
    WorkflowKey = "my-workflow",
    TransitionKey = "approve",
    TriggerType = TriggerType.Manual,
    Mode = ExecMode.Sync
};

await stateMachineExecutor.ExecuteTransitionAsync(input, cancellationToken);
```

### 3. Yeni Pipeline Step Ekleme

```csharp
public sealed class CustomValidationStep : ITransitionStep
{
    public int Order => 15; // CreateTransition (10) ile OnExecute (20) arasında
    
    public async Task ExecuteAsync(TransitionExecutionContext context, CancellationToken cancellationToken)
    {
        // Custom validation logic
        await ValidateBusinessRules(context, cancellationToken);
    }
}

// DI'da kayıt:
services.AddScoped<ITransitionStep, CustomValidationStep>();
```

### 4. Yeni Trigger Handler Ekleme

```csharp
public sealed class WebhookTransitionHandler : TransitionHandlerBase
{
    public override bool CanHandle(TriggerType triggerType) => triggerType == TriggerType.Event;
    
    protected override async Task PreValidateAsync(TransitionExecutionContext context, CancellationToken cancellationToken)
    {
        // Webhook-specific validation
        await ValidateWebhookSignature(context, cancellationToken);
    }
}

// DI'da kayıt:
services.AddScoped<ITransitionHandler, WebhookTransitionHandler>();
```

## Avantajlar

### 1. Ayrık Sorumluluklar
- Her pipeline step'i tek bir sorumluluğa sahip
- Trigger handler'ları sadece kendi trigger tiplerini yönetir
- Execution strategy'leri sadece sync/async farkını yönetir

### 2. Test Edilebilirlik
- Her bileşen bağımsız olarak test edilebilir
- Mock'lama kolay
- Integration testleri daha odaklı

### 3. Genişletilebilirlik
- Yeni pipeline step'leri kolayca eklenebilir
- Yeni trigger handler'ları eklenebilir
- Mevcut davranışlar değiştirilmeden genişletilebilir

### 4. Performans
- Re-entry sistem optimizasyonu
- Distributed locking
- Idempotency kontrolü

### 5. Gözlemlenebilirlik
- Structured logging
- Metrics collection
- Distributed tracing desteği

## Geçiş Planı

1. ✅ Yeni mimari bileşenlerini oluştur
2. ✅ Pipeline step'lerini implement et
3. ✅ Trigger handler'ları oluştur
4. ✅ Re-entry sistemini kur
5. ✅ DI kayıtlarını güncelle
6. 🔄 Mevcut kodu yeni mimariye geçir (aşamalı)
7. 🔄 E2E testleri güncelle
8. 🔄 Monitoring ve metrics'leri güncelle
9. 🔄 Eski kodu kaldır

## Notlar

- Bu refactoring backward compatibility'yi korumaya çalışır
- Mevcut API'ler çalışmaya devam eder
- Yeni özellikler yeni mimari üzerinden geliştirilmelidir
- Performance testleri yapılmalı ve karşılaştırılmalı
