using SupportDesk.Domain.Aggregates.Tickets;

namespace SupportDesk.Domain.Triage;

/// <summary>What priority a new ticket gets, and why.</summary>
/// <param name="Priority">The priority to apply.</param>
/// <param name="ForcedByCategory">The category forces Critical, whatever was asked for.</param>
/// <param name="WasDefaulted">Nothing was requested, so the default was used.</param>
public readonly record struct PriorityDecision(TicketPriority Priority, bool ForcedByCategory, bool WasDefaulted);

/// <summary>
/// Decides the priority of a new ticket: what was asked for, Medium when nothing was, and always
/// Critical when the category says so. It only sees the category's flag, never its name.
/// </summary>
public static class PriorityResolver
{
    public const TicketPriority DefaultPriority = TicketPriority.Medium;

    public static PriorityDecision Resolve(TicketPriority? requested, bool categoryForcesCritical)
    {
        if (categoryForcesCritical)
        {
            return new PriorityDecision(TicketPriority.Critical, ForcedByCategory: true, WasDefaulted: false);
        }

        return requested is { } priority
            ? new PriorityDecision(priority, ForcedByCategory: false, WasDefaulted: false)
            : new PriorityDecision(DefaultPriority, ForcedByCategory: false, WasDefaulted: true);
    }
}
