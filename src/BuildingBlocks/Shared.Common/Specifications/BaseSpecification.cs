using System.Linq.Expressions;

namespace Shared.Common.Specifications
{
    /// <summary>
    /// Base implementation of ISpecification that stores a predicate expression.
    /// Supports composition via And, Or, Not operators.
    /// </summary>
    public abstract class BaseSpecification<T> : ISpecification<T>
    {
        protected Expression<Func<T, bool>>? Criteria;

        public virtual Expression<Func<T, bool>> ToExpression()
        {
            return Criteria ?? (_ => true);
        }

        public bool IsSatisfiedBy(T entity)
        {
            return ToExpression().Compile()(entity);
        }

        public ISpecification<T> And(ISpecification<T> other)
        {
            return new AndSpecification<T>(this, other);
        }

        public ISpecification<T> Or(ISpecification<T> other)
        {
            return new OrSpecification<T>(this, other);
        }

        public ISpecification<T> Not()
        {
            return new NotSpecification<T>(this);
        }
    }

    internal sealed class AndSpecification<T> : BaseSpecification<T>
    {
        private readonly ISpecification<T> _left;
        private readonly ISpecification<T> _right;

        public AndSpecification(ISpecification<T> left, ISpecification<T> right)
        {
            _left = left;
            _right = right;
            var leftExpr = left.ToExpression();
            var rightExpr = right.ToExpression();
            var param = Expression.Parameter(typeof(T), "x");
            var body = Expression.AndAlso(
                Expression.Invoke(leftExpr, param),
                Expression.Invoke(rightExpr, param));
            Criteria = Expression.Lambda<Func<T, bool>>(body, param);
        }
    }

    internal sealed class OrSpecification<T> : BaseSpecification<T>
    {
        private readonly ISpecification<T> _left;
        private readonly ISpecification<T> _right;

        public OrSpecification(ISpecification<T> left, ISpecification<T> right)
        {
            _left = left;
            _right = right;
            var leftExpr = left.ToExpression();
            var rightExpr = right.ToExpression();
            var param = Expression.Parameter(typeof(T), "x");
            var body = Expression.OrElse(
                Expression.Invoke(leftExpr, param),
                Expression.Invoke(rightExpr, param));
            Criteria = Expression.Lambda<Func<T, bool>>(body, param);
        }
    }

    internal sealed class NotSpecification<T> : BaseSpecification<T>
    {
        private readonly ISpecification<T> _inner;

        public NotSpecification(ISpecification<T> inner)
        {
            _inner = inner;
            var innerExpr = inner.ToExpression();
            var param = Expression.Parameter(typeof(T), "x");
            var body = Expression.Not(Expression.Invoke(innerExpr, param));
            Criteria = Expression.Lambda<Func<T, bool>>(body, param);
        }
    }

    /// <summary>
    /// Convenience factory for creating specifications from lambda expressions.
    /// </summary>
    public static class Specification<T>
    {
        public static ISpecification<T> Where(Expression<Func<T, bool>> expression)
        {
            return new DirectSpecification<T>(expression);
        }
    }

    internal sealed class DirectSpecification<T> : BaseSpecification<T>
    {
        public DirectSpecification(Expression<Func<T, bool>> expression)
        {
            Criteria = expression;
        }
    }
}
