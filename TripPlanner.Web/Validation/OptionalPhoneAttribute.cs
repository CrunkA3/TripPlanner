using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;

namespace TripPlanner.Web.Validation;


/// <summary>
/// Validates that a phone number is either empty or matches a basic phone number pattern with optional plus sign,
/// digits, spaces, or hyphens.
/// </summary>
/// <remarks>This attribute allows the decorated property to be left empty or contain a phone number consisting
/// only of an optional leading plus sign, digits, spaces, or hyphens. It does not enforce country code or specific
/// phone number formatting. Use this attribute when a phone number field is optional but should conform to a simple
/// validation if provided.</remarks>
public partial class OptionalPhoneAttribute : ValidationAttribute
{

    protected override ValidationResult? IsValid(object? value, ValidationContext context)
    {
        var str = value as string;

        if (string.IsNullOrWhiteSpace(str))
            return ValidationResult.Success;

        var regex = PhoneNumberRegex();

        return regex.IsMatch(str)
            ? ValidationResult.Success
            : new ValidationResult("Invalid phone number format.");
    }

    /// <summary>
    /// Creates a regular expression that matches phone numbers containing optional leading plus signs, digits, spaces,
    /// or hyphens.
    /// </summary>
    /// <remarks>The returned regular expression enforces that the entire input string consists only of an
    /// optional plus sign followed by digits, spaces, or hyphens. It does not validate country codes or specific phone
    /// number formats.</remarks>
    /// <returns>A <see cref="Regex"/> instance that matches phone numbers with optional plus sign, digits, spaces, or hyphens.</returns>

    [GeneratedRegex(@"^\+?[0-9\s\-]+$")]
    private static partial Regex PhoneNumberRegex();
}
