using SupportDesk.Domain.Aggregates.Categories;
using SupportDesk.Domain.Aggregates.Customers;
using SupportDesk.Domain.Triage;

namespace SupportDesk.UnitTests.TestDoubles;

/// <summary>
/// The agents, categories and customers the brief's worked examples are written against, with the
/// ids the seeder gives them.
/// </summary>
public static class SeededData
{
    public static Category General => new Category("General", requiresSpecialist: false, forcesCriticalPriority: false).WithId(1);

    public static Category Billing => new Category("Billing", requiresSpecialist: true, forcesCriticalPriority: false).WithId(2);

    public static Category Technical => new Category("Technical", requiresSpecialist: false, forcesCriticalPriority: false).WithId(3);

    public static Category Outage => new Category("Outage", requiresSpecialist: true, forcesCriticalPriority: true).WithId(4);

    public static Category Security => new Category("Security", requiresSpecialist: true, forcesCriticalPriority: true).WithId(5);

    public static Customer Contoso => NewCustomer(1, "Contoso Ltd", CustomerTier.Premium);

    public static Customer Fabrikam => NewCustomer(2, "Fabrikam Inc", CustomerTier.Standard);

    public static Customer AdventureWorks => NewCustomer(3, "Adventure Works", CustomerTier.Premium);

    public static Customer Tailspin => NewCustomer(5, "Tailspin Toys", CustomerTier.Standard);

    public static Customer WideWorld => NewCustomer(6, "Wide World Importers", CustomerTier.Premium);

    /// <summary>The six seeded agents with their open tickets and specializations.</summary>
    public static IReadOnlyList<AgentCandidate> Agents { get; } =
    [
        Agent(1, "Alex Turner", open: 5, max: 10, isActive: true, 1, 3),
        Agent(2, "Ben Osei", open: 3, max: 8, isActive: true, 3, 4),
        Agent(3, "Priya Nair", open: 4, max: 8, isActive: true, 4, 5),
        Agent(4, "Sara Lindqvist", open: 2, max: 6, isActive: true, 1, 2),
        Agent(5, "Marcus Doyle", open: 6, max: 6, isActive: true, 2, 5),
        Agent(6, "Yuki Tanaka", open: 0, max: 8, isActive: false, 3)
    ];

    private static Customer NewCustomer(int id, string name, CustomerTier tier) =>
        new Customer(name, $"contact{id}@example.test", null, tier, FixedClock.DefaultNow).WithId(id);

    private static AgentCandidate Agent(
        int id, string name, int open, int max, bool isActive, params int[] categories) =>
        new(id, name, isActive, max, open, categories.ToHashSet());
}
