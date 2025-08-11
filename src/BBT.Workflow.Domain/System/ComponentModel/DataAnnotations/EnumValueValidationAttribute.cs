namespace System.ComponentModel.DataAnnotations;

public sealed class EnumValueValidationAttribute : ValidationAttribute
{
    private readonly Type _enumType;

    public EnumValueValidationAttribute(Type enumType)
    {
        _enumType = enumType;
        if (!enumType.IsEnum)
        {
            throw new ArgumentException("Provided type must be an enum.");
        }
    }

    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        if (value == null || !Enum.IsDefined(_enumType, value))
        {
            return new ValidationResult(
                $"The value '{value}' is not valid for type {_enumType.Name}. Allowed values are: {string.Join(", ", Enum.GetValues(_enumType))}.");
        }

        return ValidationResult.Success;
    }
}