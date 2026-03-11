using BookFlightTickets.Core.Domain.Entities;
using System.Linq.Expressions;

namespace BookFlightTickets.Core.Domain.Specifications
{
    public interface ISpecification<T> where T : BaseClass
    {
        public Expression<Func<T, bool>> Criteria { get;}
        public List<Expression<Func<T, object>>> Includes { get; }
        public List<Func<IQueryable<T>, IQueryable<T>>> ComplexIncludes { get; }
        public Expression<Func<T, object>>? OrderBy { get; set; }
        public Expression<Func<T, object>>? OrderByDescending { get; set; }
       
        // إضافة خصائص Pagination
        public int? Skip { get; set; }
        public int? Take { get; set; }
        public bool IsPagingEnabled { get; set; }
    }
}
