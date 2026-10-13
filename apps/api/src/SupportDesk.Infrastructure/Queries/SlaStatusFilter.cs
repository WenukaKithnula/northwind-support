using System.Linq.Expressions;
using SupportDesk.Domain.Aggregates.Customers;
using SupportDesk.Domain.Aggregates.Tickets;
using static SupportDesk.Infrastructure.Queries.ExpressionPredicates;

namespace SupportDesk.Infrastructure.Queries;

/// <summary>
/// Turns an SLA status into a condition the database can evaluate. The status is derived from the
/// due date, the resolution time and "now", exactly as <see cref="SlaEvaluator"/> derives it for a
/// row, so the filter and the status a row shows cannot disagree (a test checks that they do not).
/// </summary>
/// <remarks>
/// "At risk" depends on the ticket's priority and the customer's tier, because the window does.
/// There are only a handful of combinations, so each cut-off is worked out here, in C#, as a date;
/// the database only compares dates with those constants. No date arithmetic runs in SQL, which
/// would differ between SQL Server and SQLite.
/// </remarks>
internal static class SlaStatusFilter
{
    public static Expression<Func<TicketWithLabels, bool>> For(SlaStatus status, DateTime nowUtc, SlaPolicy policy) =>
        status switch
        {
            SlaStatus.NotApplicable => x => x.Ticket.DueAtUtc == null,

            SlaStatus.Met => x =>
                x.Ticket.DueAtUtc != null
                && x.Ticket.ResolvedAtUtc != null
                && x.Ticket.ResolvedAtUtc <= x.Ticket.DueAtUtc,

            SlaStatus.Breached => x =>
                x.Ticket.DueAtUtc != null
                && ((x.Ticket.ResolvedAtUtc != null && x.Ticket.ResolvedAtUtc > x.Ticket.DueAtUtc)
                    || (x.Ticket.ResolvedAtUtc == null && x.Ticket.DueAtUtc < nowUtc)),

            SlaStatus.AtRisk => And(OpenAndNotPastDue(nowUtc), WithinAtRiskSpan(nowUtc, policy)),

            SlaStatus.WithinSla => And(OpenAndNotPastDue(nowUtc), Not(WithinAtRiskSpan(nowUtc, policy))),

            _ => throw new ArgumentOutOfRangeException(nameof(status), status, "Unknown SLA status.")
        };

    /// <summary>Still open, with a due date, and not yet past it (being exactly on it is not past it).</summary>
    private static Expression<Func<TicketWithLabels, bool>> OpenAndNotPastDue(DateTime nowUtc) =>
        x => x.Ticket.DueAtUtc != null
             && x.Ticket.ResolvedAtUtc == null
             && x.Ticket.DueAtUtc >= nowUtc;

    /// <summary>
    /// The due date is no later than "now" plus the at-risk span for this ticket's priority and
    /// tier. One branch per combination, each against its own constant cut-off.
    /// </summary>
    private static Expression<Func<TicketWithLabels, bool>> WithinAtRiskSpan(DateTime nowUtc, SlaPolicy policy) =>
        (from priority in Enum.GetValues<TicketPriority>()
         from tier in Enum.GetValues<CustomerTier>()
         select ForCombination(priority, tier, nowUtc + policy.AtRiskSpanFor(priority, tier)))
        .Aggregate(Or);

    private static Expression<Func<TicketWithLabels, bool>> ForCombination(
        TicketPriority priority, CustomerTier tier, DateTime latestDueAtUtc) =>
        x => x.Ticket.Priority == priority
             && x.Customer.Tier == tier
             && x.Ticket.DueAtUtc <= latestDueAtUtc;
}
