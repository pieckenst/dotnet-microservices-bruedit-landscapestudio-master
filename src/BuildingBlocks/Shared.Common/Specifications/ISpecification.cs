using System.Linq.Expressions;

namespace Shared.Common.Specifications
{
    /// <summary>
    /// Specification pattern interface for building composable query filters.
    /// Allows expressing business rules as reusable, composable specifications.
    /// </summary>
    /// <typeparam name="T">The entity type the specification applies to.</typeparam>
    public interface ISpecification<T>
    {
        /// <summary>Converts the specification to an expression usable with IQueryable.</summary>
        Expression<Func<T, bool>> ToExpression();

        /// <summary>Checks whether a specific entity satisfies the specification.</summary>
        bool IsSatisfiedBy(T entity);
    }
}
