using System.ComponentModel.DataAnnotations;
using BBT.Aether.Validation;

namespace BBT.Workflow.Definitions.Validators;

public class WorkflowValidationResult: IHasValidationErrors
{
    public IList<ValidationResult> ValidationErrors { get; } = new List<ValidationResult>();
    
    public bool IsValid => !ValidationErrors.Any();

    public void AddError(ValidationResult validationResult)
    {
        ValidationErrors.Add(validationResult);
    }
}