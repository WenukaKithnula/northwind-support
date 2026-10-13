using SupportDesk.Application.Contracts.Auth;
using SupportDesk.Application.Features.Auth.Commands.Login;

namespace SupportDesk.UnitTests.Application.Features.Auth;

public class LoginRequestValidatorTests
{
    private static readonly LoginRequestValidator Validator = new();

    private static bool IsValid(string? email, string? password) =>
        Validator.Validate(new LoginRequest(email!, password!)).IsValid;

    [Fact]
    public void AnEmailAndAPassword_Pass() =>
        Assert.True(IsValid("alex.turner@northwind-support.example", "LocalDevOnly-Password-1"));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void AMissingOrBlankEmail_Fails(string? email) =>
        Assert.False(IsValid(email, "a-password"));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void AMissingPassword_Fails(string? password) =>
        Assert.False(IsValid("alex@example.test", password));

    [Fact]
    public void AnEmailLongerThanTheColumn_Fails() =>
        Assert.False(IsValid(new string('a', 257), "a-password"));

    [Fact]
    public void AnAbsurdlyLongPassword_Fails() =>
        Assert.False(IsValid("alex@example.test", new string('a', 201)));
}
