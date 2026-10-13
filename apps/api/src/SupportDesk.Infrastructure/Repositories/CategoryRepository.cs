using Microsoft.EntityFrameworkCore;
using SupportDesk.Domain.Aggregates.Categories;
using SupportDesk.Domain.Repositories;
using SupportDesk.Infrastructure.Data;

namespace SupportDesk.Infrastructure.Repositories;

public sealed class CategoryRepository(SupportDbContext db) : ICategoryRepository
{
    /// <summary>Not tracked: triage only reads a category's rules, it never changes the category.</summary>
    public Task<Category?> GetByIdAsync(int id, CancellationToken ct) =>
        db.Set<Category>().AsNoTracking().FirstOrDefaultAsync(c => c.Id == id, ct);
}
