using System;
using System.Threading.Tasks;
using BBT.Workflow.Scripting;
using BBT.Workflow.Scripting.Functions;

/// <summary>
/// Checks if all subprocesses have completed (approved).
/// Returns true if allSubprocessesCompleted is true AND no document was rejected.
/// </summary>
public class AllSubprocessesCompletedRule : ScriptBase, IConditionMapping
{
    public async Task<bool> Handler(ScriptContext context)
    {
        try
        {
            bool allCompleted = false;
            bool anyRejected = false;
            
            if (HasProperty(context.Instance.Data, "allSubprocessesCompleted"))
            {
                var completedValue = GetPropertyValue(context.Instance.Data, "allSubprocessesCompleted");
                if (completedValue is bool b)
                {
                    allCompleted = b;
                }
            }
            
            if (HasProperty(context.Instance.Data, "anyDocumentRejected"))
            {
                var rejectedValue = GetPropertyValue(context.Instance.Data, "anyDocumentRejected");
                if (rejectedValue is bool b)
                {
                    anyRejected = b;
                }
            }
            
            if (allCompleted && !anyRejected)
            {
                LogInformation("All subprocesses completed successfully - proceeding to finalization");
                return true;
            }
            
            return false;
        }
        catch (Exception ex)
        {
            LogError($"AllSubprocessesCompletedRule error: {ex.Message}");
            return false;
        }
    }
}