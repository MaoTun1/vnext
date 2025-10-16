using System.Text.Json.Serialization;
using BBT.Aether;
using BBT.Aether.Domain.Values;

namespace BBT.Workflow.Definitions;

public class TimerConfig : ValueObject
{
    private TimerConfig()
    {
    }

    [JsonConstructor]
    public TimerConfig(
        string reset,
        string duration)
    {
        Reset = Check.NotNullOrWhiteSpace(reset, nameof(Reset), WorkflowConstants.MaxTimerResetLength);
        Duration = Check.NotNullOrWhiteSpace(duration, nameof(Duration), WorkflowConstants.MaxDurationLength);
    }

    public string Reset { get; private set; }

    /// <summary>
    /// Duration ISO 8601
    /// </summary>
    public string Duration { get; private set; }

    protected override IEnumerable<object> GetAtomicValues()
    {
        yield return Reset;
        yield return Duration;
    }
}