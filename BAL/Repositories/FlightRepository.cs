using BLL.Interfaces;
using BLLProject.Specifications;
using DAL.Data;
using DAL.models;
using Microsoft.EntityFrameworkCore;

namespace BLLProject.Repositories
{
    public class FlightRepository : GenericRepository<Flight>, IFlight
    {
        private readonly BookFilghtsDbContext _dbContext;
        public FlightRepository(BookFilghtsDbContext dbContext) : base(dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<int> CountAsync(ISpecification<Flight> spec)
        {
            return await ApplySpecification(spec, false).CountAsync();
        }

        private IQueryable<Flight> ApplySpecification(ISpecification<Flight> spec, bool applyPaging = true)
        {
            return SpecificationEvalutor<Flight>.GetQuery(_dbContext.Set<Flight>().AsQueryable(), spec, applyPaging);
        }

        public async Task<Dictionary<string, int>> GetFlightCountByAirlineAsync()
        {
            return await _dbContext.Set<Flight>()
                .Where(f => f.Airline != null)
                .GroupBy(f => f.Airline.Name)
                .Select(g => new { Name = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.Name, x => x.Count);
        }

    }
}