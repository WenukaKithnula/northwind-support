using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.UnitTests.TestDoubles;

namespace SupportDesk.UnitTests.Domain.Aggregates.Tickets;

public class SlaEvaluatorTests
{
    private static readonly DateTime Now = FixedClock.DefaultNow;

    [Fact]
    public void Evaluate_WithoutADueDate_IsNotApplicable() =>
        Assert.Equal(SlaStatus.NotApplicable, SlaEvaluator.Evaluate(null, null, Now));

    [Fact]
    public void Evaluate_ResolvedOnTime_IsMet() =>
        Assert.Equal(SlaStatus.Met, SlaEvaluator.Evaluate(Now, Now, Now.AddDays(1)));

    [Fact]
    public void Evaluate_ResolvedLate_IsBreached() =>
        Assert.Equal(SlaStatus.Breached, SlaEvaluator.Evaluate(Now, Now.AddMinutes(1), Now));

    [Fact]
    public void Evaluate_OpenAndPastDue_IsBreached() =>
        Assert.Equal(SlaStatus.Breached, SlaEvaluator.Evaluate(Now.AddMinutes(-1), null, Now));

    [Fact]
    public void Evaluate_OpenAndBeforeDue_IsWithinSla() =>
        Assert.Equal(SlaStatus.WithinSla, SlaEvaluator.Evaluate(Now.AddHours(1), null, Now));

    // An 8 hour window, so the at-risk span is a quarter of it: 2 hours.
    private static readonly TimeSpan AtRiskSpan = TimeSpan.FromHours(2);

    private static readonly TimeSpan OneTick = TimeSpan.FromTicks(1);

    [Fact]
    public void Evaluate_OpenWithExactlyTheThresholdLeft_IsAtRisk() =>
        Assert.Equal(SlaStatus.AtRisk, SlaEvaluator.Evaluate(Now + AtRiskSpan, null, Now, AtRiskSpan));

    [Fact]
    public void Evaluate_OpenWithOneTickMoreThanTheThresholdLeft_IsWithinSla() =>
        Assert.Equal(
            SlaStatus.WithinSla,
            SlaEvaluator.Evaluate(Now + AtRiskSpan + OneTick, null, Now, AtRiskSpan));

    [Fact]
    public void Evaluate_OpenExactlyOnTheDueDate_IsAtRiskAndNotYetBreached() =>
        Assert.Equal(SlaStatus.AtRisk, SlaEvaluator.Evaluate(Now, null, Now, AtRiskSpan));

    [Fact]
    public void Evaluate_OpenOneTickAfterTheDueDate_IsBreached() =>
        Assert.Equal(SlaStatus.Breached, SlaEvaluator.Evaluate(Now - OneTick, null, Now, AtRiskSpan));

    [Fact]
    public void Evaluate_ResolvedExactlyOnTheDueDate_IsMet() =>
        Assert.Equal(SlaStatus.Met, SlaEvaluator.Evaluate(Now, Now, Now.AddDays(1), AtRiskSpan));

    [Fact]
    public void Evaluate_ResolvedOneTickAfterTheDueDate_IsBreached() =>
        Assert.Equal(SlaStatus.Breached, SlaEvaluator.Evaluate(Now, Now + OneTick, Now.AddDays(1), AtRiskSpan));

    [Fact]
    public void Evaluate_ResolvedInsideTheThreshold_IsMetNotAtRisk() =>
        Assert.Equal(
            SlaStatus.Met,
            SlaEvaluator.Evaluate(Now + AtRiskSpan, Now, Now, AtRiskSpan));

    [Fact]
    public void Evaluate_WithoutAnAtRiskSpan_NeverReportsAtRisk() =>
        Assert.Equal(SlaStatus.WithinSla, SlaEvaluator.Evaluate(Now.AddMinutes(1), null, Now));
}
