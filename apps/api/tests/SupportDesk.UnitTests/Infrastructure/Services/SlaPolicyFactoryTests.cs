using Microsoft.Extensions.Configuration;
using SupportDesk.Domain.Aggregates.Customers;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.Infrastructure.Services;

namespace SupportDesk.UnitTests.Infrastructure.Services;

public class SlaPolicyFactoryTests
{
    private static Dictionary<string, string?> ValidSettings() => new()
    {
        ["Sla:BaseWindowHours:Low"] = "72",
        ["Sla:BaseWindowHours:Medium"] = "24",
        ["Sla:BaseWindowHours:High"] = "8",
        ["Sla:BaseWindowHours:Critical"] = "4",
        ["Sla:PremiumCustomerMultiplier"] = "0.5",
        ["Sla:MinimumWindowHours"] = "1",
        ["Sla:AtRiskThresholdPercent"] = "25"
    };

    private static IConfiguration Configure(Dictionary<string, string?> settings) =>
        new ConfigurationBuilder().AddInMemoryCollection(settings).Build();

    [Fact]
    public void Create_ReadsTheWindowsAndTheRatesFromConfiguration()
    {
        var settings = ValidSettings();
        settings["Sla:BaseWindowHours:High"] = "10";
        settings["Sla:PremiumCustomerMultiplier"] = "0.25";
        settings["Sla:AtRiskThresholdPercent"] = "40";

        var policy = SlaPolicyFactory.Create(Configure(settings));

        Assert.Equal(TimeSpan.FromHours(10), policy.WindowFor(TicketPriority.High, CustomerTier.Standard));
        Assert.Equal(TimeSpan.FromHours(2.5), policy.WindowFor(TicketPriority.High, CustomerTier.Premium));
        Assert.Equal(40, policy.AtRiskThresholdPercent);
    }

    [Fact]
    public void Create_WithoutAnSlaSection_ThrowsAndNamesTheSection()
    {
        var exception = Assert.Throws<InvalidOperationException>(
            () => SlaPolicyFactory.Create(Configure([])));

        Assert.Contains("Sla", exception.Message);
    }

    [Theory]
    [InlineData("Sla:BaseWindowHours:Critical")]
    [InlineData("Sla:BaseWindowHours:Low")]
    [InlineData("Sla:PremiumCustomerMultiplier")]
    [InlineData("Sla:MinimumWindowHours")]
    [InlineData("Sla:AtRiskThresholdPercent")]
    public void Create_WithAMissingValue_ThrowsAndNamesIt(string key)
    {
        var settings = ValidSettings();
        settings.Remove(key);

        var exception = Assert.Throws<InvalidOperationException>(
            () => SlaPolicyFactory.Create(Configure(settings)));

        Assert.Contains(key, exception.Message);
    }

    [Fact]
    public void Create_WithAValueThatIsNotANumber_ThrowsAndNamesIt()
    {
        var settings = ValidSettings();
        settings["Sla:MinimumWindowHours"] = "an hour";

        var exception = Assert.Throws<InvalidOperationException>(
            () => SlaPolicyFactory.Create(Configure(settings)));

        Assert.Contains("Sla:MinimumWindowHours", exception.Message);
    }

    [Fact]
    public void Create_ReadsNumbersTheSameWayOnEveryMachine()
    {
        // "0.5" must mean a half whatever the machine's culture is.
        var previous = Thread.CurrentThread.CurrentCulture;
        Thread.CurrentThread.CurrentCulture = new System.Globalization.CultureInfo("de-DE");

        try
        {
            var policy = SlaPolicyFactory.Create(Configure(ValidSettings()));

            Assert.Equal(TimeSpan.FromHours(2), policy.WindowFor(TicketPriority.Critical, CustomerTier.Premium));
        }
        finally
        {
            Thread.CurrentThread.CurrentCulture = previous;
        }
    }

    [Fact]
    public void Create_FromTheCommittedAppSettings_GivesTheNumbersInTheBrief()
    {
        // appsettings.json is copied next to the tests, so this reads the real file.
        var configuration = new ConfigurationBuilder()
            .AddJsonFile(Path.Combine(AppContext.BaseDirectory, "appsettings.json"))
            .Build();

        var policy = SlaPolicyFactory.Create(configuration);

        Assert.Equal(TimeSpan.FromHours(4), policy.WindowFor(TicketPriority.Critical, CustomerTier.Standard));
        Assert.Equal(TimeSpan.FromHours(8), policy.WindowFor(TicketPriority.High, CustomerTier.Standard));
        Assert.Equal(TimeSpan.FromHours(24), policy.WindowFor(TicketPriority.Medium, CustomerTier.Standard));
        Assert.Equal(TimeSpan.FromHours(72), policy.WindowFor(TicketPriority.Low, CustomerTier.Standard));
        Assert.Equal(TimeSpan.FromHours(36), policy.WindowFor(TicketPriority.Low, CustomerTier.Premium));
        Assert.Equal(25, policy.AtRiskThresholdPercent);
    }
}
