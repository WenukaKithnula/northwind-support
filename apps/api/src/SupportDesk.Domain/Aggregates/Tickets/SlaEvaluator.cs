namespace SupportDesk.Domain.Aggregates.Tickets;

/// <summary>
/// Works out where a ticket stands against its due date. SLA status is always derived from
/// the ticket's dates and the current time - never stored.
/// </summary>
public static class SlaEvaluator
{
    /// <param name="dueAtUtc">When the ticket is due, or null when no SLA applies.</param>
    /// <param name="resolvedAtUtc">When it was resolved, or null while it is still open.</param>
    /// <param name="nowUtc">The current time. Always passed in, so tests can fix it.</param>
    /// <param name="atRiskSpan">
    /// How much time left, or less, makes an open ticket at risk (see
    /// <see cref="SlaPolicy.AtRiskSpanFor"/>). When null, no ticket is reported as at risk.
    /// </param>
    public static SlaStatus Evaluate(
        DateTime? dueAtUtc,
        DateTime? resolvedAtUtc,
        DateTime nowUtc,
        TimeSpan? atRiskSpan = null)
    {
        if (dueAtUtc is not { } due)
        {
            return SlaStatus.NotApplicable;
        }

        if (resolvedAtUtc is { } resolved)
        {
            return resolved <= due ? SlaStatus.Met : SlaStatus.Breached;
        }

        if (nowUtc > due)
        {
            return SlaStatus.Breached;
        }

        return atRiskSpan is { } span && due - nowUtc <= span
            ? SlaStatus.AtRisk
            : SlaStatus.WithinSla;
    }
}
