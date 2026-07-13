using System.Text.RegularExpressions;

namespace ArcadeLink.Users.Api.Validation;

public static partial class UserValidation
{
    public static bool IsValidEmail(string email) =>
        !string.IsNullOrWhiteSpace(email) && EmailRegex().IsMatch(email);

    public static bool IsValidPassword(string password) =>
        !string.IsNullOrEmpty(password)
        && password.Length >= 8
        && password.Any(char.IsDigit)
        && password.Any(char.IsLetter);

    [GeneratedRegex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$")]
    private static partial Regex EmailRegex();
}
