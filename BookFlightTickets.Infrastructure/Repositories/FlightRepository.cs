using BookFlightTickets.Core.Domain.Entities;
using BookFlightTickets.Core.Domain.RepositoryContracts;
using BookFlightTickets.Infrastructure.Data.DbContext;
using Microsoft.EntityFrameworkCore;

namespace BookFlightTickets.Infrastructure.Repositories
{
    public class FlightRepository : GenericRepository<Flight>, IFlightRepository
    {
        private readonly BookFilghtsDbContext _dbContext;
        public FlightRepository(BookFilghtsDbContext dbContext) : base(dbContext)
        {
            _dbContext = dbContext;
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