using System.ComponentModel.DataAnnotations;

namespace WindowsSearch.Common.Validation;

public sealed class HttpEndpointAttribute : ValidationAttribute
{
    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        if (value is not string str || string.IsNullOrWhiteSpace(str))
        {
            return new ValidationResult("Endpoint is required.", new[] { validationContext.MemberName! });
        }

        if (!Uri.TryCreate(str, UriKind.Absolute, out var uri) || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            return new ValidationResult("Endpoint must be an absolute HTTP or HTTPS URI.", new[] { validationContext.MemberName! });
        }

        return ValidationResult.Success;
    }
}
