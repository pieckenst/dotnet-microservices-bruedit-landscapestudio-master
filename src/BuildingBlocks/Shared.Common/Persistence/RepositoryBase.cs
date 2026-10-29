using Microsoft.EntityFrameworkCore;
using Shared.Common.Common;
using System.Linq.Expressions;

namespace Shared.Common.Persistence
{
    /// <summary>
    /// Generic repository implementation using DbContext generically.
    /// Read operations are no-tracking by default to avoid unnecessary EF change-tracker overhead.
    /// </summary>
    /// <typeparam name="T">Entity type that inherits from EntityBase.</typeparam>
    public class RepositoryBase<T> : IRepositoryBase<T> where T : EntityBase
    {
        protected readonly DbContext _dbContext;

        public RepositoryBase(DbContext dbContext)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        }

        public async Task<IReadOnlyList<T>> GetAllAsync()
        {
            return await _dbContext.Set<T>().AsNoTracking().ToListAsync();
        }

        public async Task<T?> GetAsync(Expression<Func<T, bool>> predicate)
        {
            ArgumentNullException.ThrowIfNull(predicate);
            return await _dbContext.Set<T>().AsNoTracking().Where(predicate).FirstOrDefaultAsync();
        }

        public async Task<IReadOnlyList<T>> GetAsync(
            Expression<Func<T, bool>>? predicate = null,
            Func<IQueryable<T>, IOrderedQueryable<T>>? orderBy = null,
            string? includeString = null,
            bool disableTracking = true)
        {
            IQueryable<T> query = _dbContext.Set<T>();
            if (disableTracking) query = query.AsNoTracking();

            if (!string.IsNullOrWhiteSpace(includeString)) query = query.Include(includeString);
            if (predicate != null) query = query.Where(predicate);

            return orderBy is not null
                ? await orderBy(query).ToListAsync()
                : await query.ToListAsync();
        }

        public async Task<IReadOnlyList<T>> GetAsync(
            Expression<Func<T, bool>>? predicate = null,
            Func<IQueryable<T>, IOrderedQueryable<T>>? orderBy = null,
            List<Expression<Func<T, object>>>? includes = null,
            bool disableTracking = true)
        {
            IQueryable<T> query = _dbContext.Set<T>();
            if (disableTracking) query = query.AsNoTracking();

            if (includes is not null)
                query = includes.Aggregate(query, (current, include) => current.Include(include));

            if (predicate is not null) query = query.Where(predicate);

            return orderBy is not null
                ? await orderBy(query).ToListAsync()
                : await query.ToListAsync();
        }

        public async Task<PagedResult<T>> GetPagedAsync(
            int page,
            int pageSize,
            Expression<Func<T, bool>>? predicate = null,
            Func<IQueryable<T>, IOrderedQueryable<T>>? orderBy = null)
        {
            if (page < 1)
                throw new ArgumentOutOfRangeException(nameof(page), "Page must be at least 1.");
            if (pageSize is < 1 or > 200)
                throw new ArgumentOutOfRangeException(nameof(pageSize), "Page size must be between 1 and 200.");

            IQueryable<T> query = _dbContext.Set<T>().AsNoTracking();
            if (predicate is not null)
                query = query.Where(predicate);

            var totalCount = await query.CountAsync();
            var orderedQuery = orderBy is not null
                ? orderBy(query)
                : query.OrderBy(entity => entity.Id);

            var items = await orderedQuery
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return new PagedResult<T>(items, page, pageSize, totalCount);
        }

        public virtual async Task<T?> GetByIdAsync(Guid id)
        {
            return await _dbContext.Set<T>()
                .AsNoTracking()
                .FirstOrDefaultAsync(entity => entity.Id == id);
        }

        public async Task<T> AddAsync(T entity)
        {
            ArgumentNullException.ThrowIfNull(entity);
            await _dbContext.Set<T>().AddAsync(entity);
            await _dbContext.SaveChangesAsync();
            return entity;
        }

        public async Task UpdateAsync(T entity)
        {
            ArgumentNullException.ThrowIfNull(entity);
            _dbContext.Entry(entity).State = EntityState.Modified;
            await _dbContext.SaveChangesAsync();
        }

        public async Task DeleteAsync(T entity)
        {
            ArgumentNullException.ThrowIfNull(entity);
            _dbContext.Set<T>().Remove(entity);
            await _dbContext.SaveChangesAsync();
        }
    }
}
