using DAL.models;
using Microsoft.EntityFrameworkCore;

namespace BLLProject.Specifications
{
    public static class SpecificationEvalutor<TEntity> where TEntity : BaseClass
    {
        public static IQueryable<TEntity> GetQuery(IQueryable<TEntity> input, ISpecification<TEntity> spec,bool applyPaging = true)
        {
            var query = input;
            if (spec.Criteria is not null)
                query = query.Where(spec.Criteria);
            if (spec.OrderBy is not null)
                query = query.OrderBy(spec.OrderBy);
            if (spec.OrderByDescending is not null)
                query = query.OrderByDescending(spec.OrderByDescending);
            if (spec.Includes?.Any() == true)
                query = spec.Includes.Aggregate(query, (Current, IncludeExpression) => Current.Include(IncludeExpression));
            if (spec.ComplexIncludes?.Any() == true)
                query = spec.ComplexIncludes.Aggregate(query, (current, includeQuery) => includeQuery(current));
            if (applyPaging && spec.IsPagingEnabled)
            {
                if (spec.Skip.HasValue)
                    query = query.Skip(spec.Skip.Value);
                if (spec.Take.HasValue)
                    query = query.Take(spec.Take.Value);
            }

            return query;
        }
    }

}
