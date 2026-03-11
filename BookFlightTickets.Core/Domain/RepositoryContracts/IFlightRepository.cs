using BookFlightTickets.Core.Domain.Entities;

namespace BookFlightTickets.Core.Domain.RepositoryContracts
{
    public interface IFlightRepository : IGenericRepository<Flight>
    {
        Task<Dictionary<string, int>> GetFlightCountByAirlineAsync();
    }
}
