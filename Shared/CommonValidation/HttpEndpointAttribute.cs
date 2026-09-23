using System.ComponentModel.DataAnnotations;

namespace CommonValidation;

public sealed class HttpEndpointAttribute : ValidationAttribute
{
    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        if (value is not string str || string.IsNullOrWhiteSpace(str))
        {
            return new ValidationResult("Endpoint is required.");
        }

        if (!Uri.TryCreate(str, UriKind.Absolute, out var uri) || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            return new ValidationResult("Endpoint must be an absolute HTTP or HTTPS URI.");
        }

        return ValidationResult.Success;
    }
}