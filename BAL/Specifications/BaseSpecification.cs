using DAL.models;
using System.Linq.Expressions;

namespace BLLProject.Specifications
{
    public class BaseSpecification<T> : ISpecification<T> where T : BaseClass
    {
        public Expression<Func<T, bool>> Criteria { get; set; }
        public List<Expression<Func<T, object>>> Includes { get; set; } = new
            List<Expression<Func<T, object>>>();
        public List<Func<IQueryable<T>, IQueryable<T>>> ComplexIncludes { get; set; } = new();
        public Expression<Func<T, object>>? OrderBy { get; set; }
        public Expression<Func<T, object>>? OrderByDescending { get; set; }
        public int? Skip { get; set; }
        public int? Take { get; set; }
        public bool IsPagingEnabled { get; set; }
        public BaseSpecification() {}

        public BaseSpecification(Expression<Func<T, bool>> CriteriaExpression)
        {
            Criteria = CriteriaExpression;
        }

        public BaseSpecification(Expression<Func<T, bool>> criteriaExpression, Func<IQueryable<T>, IQueryable<T>> includeQuery)
        {
            Criteria = criteriaExpression;
            ComplexIncludes.Add(includeQuery);
        }
       
        public void OrderByAsc(Expression<Func<T, object>> orderBy)
        {
            OrderBy = orderBy;
        }
        public void OrderByDesc(Expression<Func<T, object>> orderByDesc)
        {
            OrderByDescending = orderByDesc;
        }

        public void ApplyPaging(int skip, int take)
        {
            Skip = skip;
            Take = take;
            IsPagingEnabled = true;
        }
    }

}
