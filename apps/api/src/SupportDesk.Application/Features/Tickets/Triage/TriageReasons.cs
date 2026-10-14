using System.Globalization;
using SupportDesk.Domain.Aggregates.Categories;
using SupportDesk.Domain.Aggregates.Customers;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.Domain.Triage;

namespace SupportDesk.Application.Features.Tickets.Triage;

/// <summary>
/// Puts a triage decision into plain words. The decisions themselves are made by the rule classes
/// in the domain; this only describes them, so changing a sentence can never change an outcome.
/// </summary>
public static class TriageReasons
{
    public static string Priority(PriorityDecision decision, Category category)
    {
        if (decision.ForcedByCategory)
        {
            return $"Category '{category.Name}' is always treated as Critical";
        }

        return decision.WasDefaulted
            ? $"No priority requested, so the default {decision.Priority} applies"
            : $"Priority {decision.Priority} as requested";
    }

    // Only the wording depends on this: an exact half reads as "halved", anything else as a percentage.
    private const double HalvedWindow = 0.5;

    public static string SlaWindow(SlaPolicy policy, TicketPriority priority, CustomerTier tier)
    {
        var baseHours = policy.BaseWindowFor(priority).TotalHours;
        var window = policy.WindowFor(priority, tier).TotalHours;
        var text = $"{priority} base window {Hours(baseHours)}";

        if (tier != CustomerTier.Premium)
        {
            return text;
        }

        if (window <= baseHours * policy.PremiumCustomerMultiplier)
        {
            return policy.PremiumCustomerMultiplier == HalvedWindow
                ? $"{text}, halved for a Premium customer"
                : $"{text}, reduced to {Number(policy.PremiumCustomerMultiplier * 100)}% for a Premium customer";
        }

        return $"{text}, reduced for a Premium customer but kept at the {Hours(policy.MinimumWindowHours)} minimum";
    }

    public static string Assignment(AgentSelection selection, Category category)
    {
        var who = category.RequiresSpecialist ? $"{category.Name} specialists" : "active agents with room";

        return selection.Outcome switch
        {
            SelectionOutcome.Selected => $"Fewest open tickets among {who}",
            SelectionOutcome.KeptCurrent => $"{selection.Agent!.FullName} still qualifies, so keeps the ticket",
            SelectionOutcome.AllQualifiedAgentsFull => category.RequiresSpecialist
                ? $"Every {category.Name} specialist is at their open-ticket limit"
                : "Every active agent is at their open-ticket limit",
            _ => category.RequiresSpecialist
                ? $"No available agent specialised in {category.Name}"
                : "No active agent is available"
        };
    }

    private static string Hours(double hours) => Number(hours) + "h";

    // Invariant, so the text reads the same on every machine.
    private static string Number(double value) => value.ToString("0.##", CultureInfo.InvariantCulture);
}
