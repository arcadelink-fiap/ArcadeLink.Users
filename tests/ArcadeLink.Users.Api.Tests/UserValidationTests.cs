using ArcadeLink.Users.Api.Validation;
using Xunit;

namespace ArcadeLink.Users.Api.Tests;

public class UserValidationTests
{
    [Theory]
    [InlineData("ada@example.com")]
    [InlineData("a.lovelace+tag@example.co")]
    public void IsValidEmail_accepts_well_formed_addresses(string email)
    {
        Assert.True(UserValidation.IsValidEmail(email));
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-an-email")]
    [InlineData("missing-domain@")]
    [InlineData("@missing-local.com")]
    [InlineData(" spaced@example.com")]
    public void IsValidEmail_rejects_malformed_addresses(string email)
    {
        Assert.False(UserValidation.IsValidEmail(email));
    }

    [Theory]
    [InlineData("Senha1234")]
    [InlineData("abcdefg1")]
    public void IsValidPassword_accepts_letters_and_digits_with_min_length(string password)
    {
        Assert.True(UserValidation.IsValidPassword(password));
    }

    [Theory]
    [InlineData("")]
    [InlineData("short1")]
    [InlineData("onlyletters")]
    [InlineData("12345678")]
    public void IsValidPassword_rejects_short_or_single_character_class_passwords(string password)
    {
        Assert.False(UserValidation.IsValidPassword(password));
    }
}
