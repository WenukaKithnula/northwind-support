using SupportDesk.Domain.Common;

namespace SupportDesk.UnitTests.TestDoubles;

public static class TestEntities
{
    /// <summary>
    /// Gives an entity the id the database would have assigned it. Ids are protected on purpose,
    /// so only a test that stands in for the database should ever set one.
    /// </summary>
    public static T WithId<T>(this T entity, int id) where T : Entity
    {
        typeof(Entity).GetProperty(nameof(Entity.Id))!.SetValue(entity, id);

        return entity;
    }
}
