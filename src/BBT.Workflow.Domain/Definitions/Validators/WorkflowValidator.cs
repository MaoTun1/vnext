using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;

namespace BBT.Workflow.Definitions.Validators;

/// <summary>
/// This class aims to provide consistent validation for the workflow domain.
/// </summary>
public class WorkflowValidator
{
    private static readonly Regex KeyRegex = new(@"^[a-z\-]+$", RegexOptions.Compiled);

    public WorkflowValidationResult Validate(Workflow workflow)
    {
        var result = new WorkflowValidationResult();

        var stateKeys = workflow.States.Select(s => s.Key).ToHashSet();

        ValidateKey(workflow, result);
        ValidateTimeoutInStates(workflow, result, stateKeys);
        ValidateStartTransition(workflow, result);
        ValidateTransitionInStates(workflow, result, stateKeys);
        ValidateStateCountAndTypes(workflow, result);
        return result;
    }

    private void ValidateTimeoutInStates(Workflow workflow, WorkflowValidationResult result, HashSet<string> stateKeys)
    {
        if (workflow.Timeout != null)
        {
            if (!stateKeys.Contains(workflow.Timeout.Target))
            {
                result.AddError(new ValidationResult(
                    $"The 'target' value in the feature does not match any state '{workflow.Timeout.Target}'.",
                    [$"{nameof(Workflow)}.{nameof(WorkflowTimeout)}.{nameof(WorkflowTimeout.Target)}"]));
            }
        }
    }

    private void ValidateTransitionInStates(Workflow workflow, WorkflowValidationResult result,
        HashSet<string> stateKeys)
    {
        foreach (var availableState in workflow.StartTransition.AvailableIn)
        {
            if (!stateKeys.Contains(availableState))
            {
                result.AddError(new ValidationResult(
                    $"The 'availableIn' value in the feature does not match any state '{availableState}'.",
                    [
                        $"{nameof(Workflow)}.{nameof(Workflow.StartTransition)}.{nameof(Workflow.StartTransition.Target)}"
                    ]));
            }
        }

        if (!workflow.StartTransition.Target.IsNullOrEmpty())
        {
            if (!stateKeys.Contains(workflow.StartTransition.Target))
            {
                result.AddError(new ValidationResult(
                    $"The 'target' value in the feature does not match any state '{workflow.StartTransition.Target}'.",
                    [$"{nameof(Workflow)}.{nameof(Workflow.StartTransition)}.{nameof(Transition.Target)}"]));
            }
        }

        foreach (var availableState in workflow.SharedTransitions.SelectMany(s => s.AvailableIn))
        {
            if (!stateKeys.Contains(availableState))
            {
                result.AddError(new ValidationResult(
                    $"The 'availableIn' value in the feature does not match any state '{availableState}'.",
                    [$"{nameof(Workflow)}.{nameof(Workflow.SharedTransitions)}.{nameof(Transition.AvailableIn)}"]));
            }
        }

        foreach (var availableState in workflow.SharedTransitions.Where(p => !string.IsNullOrEmpty(p.Target))
                     .Select(s => s.Target))
        {
            if (!stateKeys.Contains(availableState!))
            {
                result.AddError(new ValidationResult(
                    $"The 'target' value in the feature does not match any state '{availableState}'.",
                    [$"{nameof(Workflow)}.{nameof(Workflow.SharedTransitions)}.{nameof(Transition.Target)}"]));
            }
        }
    }

    private void ValidateStartTransition(Workflow workflow, WorkflowValidationResult result)
    {
        if (workflow.StartTransition == null)
        {
            result.AddError(new ValidationResult("Workflow must have at least one start transition.",
                [$"{nameof(Workflow)}.{nameof(Workflow.StartTransition)}"]));
        }
    }

    private void ValidateStateCountAndTypes(Workflow workflow, WorkflowValidationResult result)
    {
        if (workflow.States.Count < 2)
        {
            result.AddError(new ValidationResult("Workflow must contain at least two states.",
                [$"{nameof(Workflow)}.{nameof(Workflow.States)}"]));
            return;
        }

        if (workflow.States.Count(s => s.StateType == StateType.Initial) != 1)
        {
            result.AddError(new ValidationResult("Workflow must contain at least one start state.",
                [$"{nameof(Workflow)}.{nameof(Workflow.States)}"]));
        }
    }

    private void ValidateKey(Workflow workflow, WorkflowValidationResult result)
    {
        if (!KeyRegex.IsMatch(workflow.Key))
        {
            result.AddError(new ValidationResult(
                "The Key field can only contain lowercase alphabetic characters (a-z) and hyphens (-).",
                [$"{nameof(Workflow)}.{nameof(Workflow.Key)}"]));
        }
    }
}