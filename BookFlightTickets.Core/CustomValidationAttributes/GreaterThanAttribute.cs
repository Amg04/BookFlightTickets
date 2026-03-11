using System.ComponentModel.DataAnnotations;
namespace BookFlightTickets.Core.CustomValidationAttributes
{
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
    public class GreaterThanAttribute : ValidationAttribute
    {
        private readonly string _otherPropertyName;
        public GreaterThanAttribute(string otherPropertyName)
        {
            _otherPropertyName = otherPropertyName;
        }

        protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
        {
            var otherPropertyInfo = validationContext.ObjectType.GetProperty(_otherPropertyName);

            if (otherPropertyInfo == null)
                return new ValidationResult($"Unknown property: {_otherPropertyName}");

            var otherPropertyValue = otherPropertyInfo.GetValue(validationContext.ObjectInstance);

            if (value == null || otherPropertyValue == null)
                return ValidationResult.Success;

            if (value is IComparable currentValue && otherPropertyValue is IComparable otherValue)
            {
                if (currentValue.CompareTo(otherValue) <= 0) // currentValue <= otherValue
                {
                    return new ValidationResult(ErrorMessage ?? $"{validationContext.DisplayName} must be greater than {_otherPropertyName}.");
                }
            }
            else
            {
                return new ValidationResult($"Properties {_otherPropertyName} and {validationContext.DisplayName} must be IComparable.");
            }

            return ValidationResult.Success;
        }
    }
    
}
