using SupportDesk.Application.Contracts.Tickets;
using SupportDesk.Application.Features.Tickets.Commands.EscalateTicket;

namespace SupportDesk.UnitTests.Application.Features.Tickets.Commands;

public class EscalateTicketRequestValidatorTests
{
    private static readonly EscalateTicketRequestValidator Validator = new();

    private static bool IsValid(string? reason) =>
        Validator.Validate(new EscalateTicketRequest(reason!)).IsValid;

    [Fact]
    public void AValidRequest_Passes() =>
        Assert.True(IsValid("Customer called twice and needs an answer."));

    [Theory]
    [InlineData(5)]
    [InlineData(500)]
    public void AReasonAtTheEdgesOfTheAllowedLength_Passes(int length) =>
        Assert.True(IsValid(new string('a', length)));

    [Theory]
    [InlineData(4)]
    [InlineData(501)]
    public void AReasonJustOutsideTheAllowedLength_Fails(int length) =>
        Assert.False(IsValid(new string('a', length)));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("     ")]
    public void AMissingOrBlankReason_Fails(string? reason) =>
        Assert.False(IsValid(reason));

    [Fact]
    public void TheReasonIsCountedAfterTrimming()
    {
        Assert.False(IsValid("   abc   "));
        Assert.True(IsValid("  abcde  "));
    }

    [Fact]
    public void TheFailureIsReportedAgainstTheReasonField()
    {
        var result = Validator.Validate(new EscalateTicketRequest("abc"));

        Assert.Contains(result.Errors, error => error.PropertyName == nameof(EscalateTicketRequest.Reason));
    }
}
