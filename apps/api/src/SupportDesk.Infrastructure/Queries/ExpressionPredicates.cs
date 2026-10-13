using System.Linq.Expressions;

namespace SupportDesk.Infrastructure.Queries;

/// <summary>
/// Joins conditions written as separate lambdas into one, so a query can be assembled from small
/// readable pieces and still be translated to a single SQL <c>WHERE</c>.
/// </summary>
internal static class ExpressionPredicates
{
    public static Expression<Func<T, bool>> And<T>(Expression<Func<T, bool>> left, Expression<Func<T, bool>> right) =>
        Join(left, right, Expression.AndAlso);

    public static Expression<Func<T, bool>> Or<T>(Expression<Func<T, bool>> left, Expression<Func<T, bool>> right) =>
        Join(left, right, Expression.OrElse);

    public static Expression<Func<T, bool>> Not<T>(Expression<Func<T, bool>> inner) =>
        Expression.Lambda<Func<T, bool>>(Expression.Not(inner.Body), inner.Parameters);

    private static Expression<Func<T, bool>> Join<T>(
        Expression<Func<T, bool>> left,
        Expression<Func<T, bool>> right,
        Func<Expression, Expression, BinaryExpression> combine)
    {
        // Each lambda has its own parameter (the "x"); point the right one at the left's.
        var parameter = left.Parameters[0];
        var rightBody = new ReplaceParameter(right.Parameters[0], parameter).Visit(right.Body);

        return Expression.Lambda<Func<T, bool>>(combine(left.Body, rightBody), parameter);
    }

    private sealed class ReplaceParameter(ParameterExpression from, ParameterExpression to) : ExpressionVisitor
    {
        protected override Expression VisitParameter(ParameterExpression node) =>
            node == from ? to : base.VisitParameter(node);
    }
}
